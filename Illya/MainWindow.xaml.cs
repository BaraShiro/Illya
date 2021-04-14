/*
    File:       MainWindow.xaml.cs
    Version:    0.5.0
    Author:     Robert Rosborg
 
 */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Forms;
using System.Drawing;
using System.Windows.Interop;
using Microsoft.Win32;

namespace Illya
{
    /// <summary>
    /// An enum representing the four corners of the screen.
    /// </summary>
    internal enum Corner
    {
        None,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
        Custom
    }
    
    /// <summary>
    /// Class for extensions.
    /// </summary>
    internal static class Extensions
    {
        /// <summary>
        /// Extension for iterating over a collection with indices.
        /// https://stackoverflow.com/questions/43021/how-do-you-get-the-index-of-the-current-iteration-of-a-foreach-loop#comment93800836_39997157
        /// </summary>
        /// <param name="self">The collection to iterate over.</param>
        /// <typeparam name="T">The type of the objects contained in the collection.</typeparam>
        /// <returns>If self is not null, an IEnumerable with tuples that contain the elements from self as its
        /// first element, and the elements index as its second. Otherwise a new empty list of type (T, int).</returns>
        public static IEnumerable<(T item, int index)> WithIndex<T>(this IEnumerable<T> self) => 
            self?.Select((item, index) => (item, index)) ?? new List<(T, int)>();
        
        /// <summary>
        /// Extension for strings to put spaces between camel cased words.
        /// https://stackoverflow.com/a/155486/9852711
        /// </summary>
        /// <param name="input">The string to process.</param>
        /// <returns>A new string with spaces inserted between camel cased words.</returns>
        public static string SpaceCamelCase(this String input)
        {
            return new string(Enumerable.Concat(
                input.Take(1), // No space before initial cap
                InsertSpacesBeforeCaps(input.Skip(1))
            ).ToArray());
        }
        
        private static IEnumerable<char> InsertSpacesBeforeCaps(IEnumerable<char> input)
        {
            foreach (char c in input)
            {
                if (char.IsUpper(c)) 
                { 
                    yield return ' '; 
                }

                yield return c;
            }
        }
    }
    
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string IllyaRegistryKey = @"SOFTWARE\Illya";
        private const string KeyValueCurrentScreenInt = "currentScreenInt";
        private const string KeyValueCurrentCornerInt = "curentCornerInt";
        private const string KeyValueCustomPosXDouble = "customPosXDouble";
        private const string KeyValueCustomPosYDouble = "customPosYDouble";
        private const string KeyValueCustomPosScreenInt = "customPosScreenInt";
        private const string KeyValueAlwaysOnTopBool = "alwaysOnTopBool";

        private const int DefaultCurrentScreenIndex = 0;
        private const Corner DefaultCurrentCorner = Corner.None;
        private const double DefaultCustomPosX = 0D;
        private const double DefaultCustomPosY = 0D;
        private const int DefaultCustomPosScreenIndex = 0;
        private const bool DefaultAlwaysOnTop = true;
        
        private readonly NotifyIcon _notifyIcon;
        
        private Screen _currentScreen;
        private Corner _currentCorner;
        private (double x, double y, Screen screen) _customPosition;
        private bool _alwaysOnTop;

        
        public MainWindow()
        {
            InitializeComponent();

            _notifyIcon = new NotifyIcon
            {
                Icon = System.Drawing.Icon.FromHandle(Illya.Resources.IllyaIcon.Handle),
                Visible = true,
                Text = "Illya"
            };

            try
            {
                // Read settings from registry, if registry key not found create new with default values.
                ReadSettingsFromRegistry();
            }
            catch (Exception e)
            {
                // Can't access registry, using default values.
                // TODO: print e.Message
                VideoNameTextBlock.Text = e.Message;
                _currentScreen = Screen.AllScreens[DefaultCurrentScreenIndex];
                _currentCorner = DefaultCurrentCorner;
                _customPosition = (DefaultCustomPosX, DefaultCustomPosY, 
                                   Screen.AllScreens[DefaultCustomPosScreenIndex]);
                _alwaysOnTop = DefaultAlwaysOnTop;
            }

            Topmost = _alwaysOnTop;
            
            UpdateWindowPosition();

            CreateNotifyIconContextMenu();
        }

        /// <summary>
        /// Sets up the context menu for the notify icon.
        /// </summary>
        private void CreateNotifyIconContextMenu()
        {
            ContextMenuStrip notifyIconContextMenu = new ContextMenuStrip();

            // Name and version
            ToolStripMenuItem nameMenuItem = new ToolStripMenuItem {Text = "Illya v0.5.0", Enabled = false};
            notifyIconContextMenu.Items.Add(nameMenuItem);
            
            // Settings submenu
            ToolStripMenuItem settingsMenuItem = new ToolStripMenuItem {Text = "Settings"};
            notifyIconContextMenu.Items.Add(settingsMenuItem);

            // Settings -> Always on top
            ToolStripMenuItem alwaysOnTop = new ToolStripMenuItem
            {
                Text = "Always on top", Checked = _alwaysOnTop, CheckOnClick = true
            };
            alwaysOnTop.Click += ContextMenuAlwaysOnTop;
            settingsMenuItem.DropDownItems.Add(alwaysOnTop);


            settingsMenuItem.DropDownItems.Add("-");

            // Settings -> Screens
            foreach ((Screen screen, int index) in Screen.AllScreens.WithIndex())
            {
                ToolStripMenuItem menuItem = new ToolStripMenuItem {Text = $"Move to display {index}"};
                menuItem.Click += (sender, args) => { UpdateWindowPosition(screen); };
                settingsMenuItem.DropDownItems.Add(menuItem);
            }
            
            settingsMenuItem.DropDownItems.Add("-");

            // Settings -> Corners
            foreach (Corner corner in Enum.GetValues<Corner>())
            {
                if (corner is Corner.None or Corner.Custom) continue;
                ToolStripMenuItem menuItem = new ToolStripMenuItem
                {
                    Text = $"Place in {Enum.GetName(corner).SpaceCamelCase().ToLower()} corner"
                };
                menuItem.Click += (sender, args) => { UpdateWindowPosition(corner); };
                settingsMenuItem.DropDownItems.Add(menuItem);
            }
            
            settingsMenuItem.DropDownItems.Add("-");

            // Settings -> Custom Position
            ToolStripMenuItem saveCustomMenuItem = new ToolStripMenuItem { Text = "Save custom position"};
            saveCustomMenuItem.Click += ContextMenuSaveCustomPosition;
            settingsMenuItem.DropDownItems.Add(saveCustomMenuItem);

            ToolStripMenuItem loadCustomMenuItem = new ToolStripMenuItem { Text = "Move to custom position"};
            loadCustomMenuItem.Click += ContextMenuLoadCustomPosition;
            settingsMenuItem.DropDownItems.Add(loadCustomMenuItem);
            
            // Exit
            ToolStripMenuItem exitMenuItem = new ToolStripMenuItem {Text = "Exit"};
            exitMenuItem.Click += ContextMenuExit;
            notifyIconContextMenu.Items.Add(exitMenuItem);
            
            _notifyIcon.ContextMenuStrip = notifyIconContextMenu;
        }

        /// <summary>
        /// EventHandler for clicking the Exit menu item in the notify icons context menu.
        /// Exits the application.
        /// </summary>
        private void ContextMenuExit(object sender, EventArgs e)
        {
            Close();
        }
        
        /// <summary>
        /// EventHandler for the always on top context menu item.
        /// Toggles MainWindow.Topmost.
        /// </summary>
        private void ContextMenuAlwaysOnTop(object sender, EventArgs e)
        {
            _alwaysOnTop = !_alwaysOnTop;
            Topmost = _alwaysOnTop;
        }
        
        /// <summary>
        /// EventHandler for the save custom position context menu item.
        /// Saves the current window position and screen to _customPosition.
        /// Sets _customPosition.x to Left, _customPosition.y to Top,
        /// and customPosition.screen to _currentScreen.
        /// </summary>
        private void ContextMenuSaveCustomPosition(object sender, EventArgs e)
        {
            _customPosition = (Left, Top, _currentScreen);
        }

        /// <summary>
        /// EventHandler for the move to custom position context menu item.
        /// Loads the window position and screen from _customPosition.
        /// Sets _currentScreen to _customPosition.screen, _currentCorner to Corner.None,
        /// Left to _customPosition.x, and Top _customPosition.y
        /// </summary>
        private void ContextMenuLoadCustomPosition(object sender, EventArgs e)
        {
            UpdateWindowPosition(_customPosition.screen, Corner.Custom);
        }
        
        /// <summary>
        /// EventHandler for clicking left clicking to drag the main window.
        /// If the window position after the move is not equal to the position before the move
        /// sets _currentCorner to Corner.None, and sets _currentScreen to the screen that contains
        /// the largest portion of the window.
        /// </summary>
        private void MoveWindow(object sender, MouseButtonEventArgs e)
        {
            PlaytimeTextBlock.Text = "Move start";
            (double, double) startPos = (Left, Top);
            DragMove();
            (double, double) stopPos = (Left, Top);
            if (startPos != stopPos)
            {
                _currentCorner = Corner.Custom;
                _currentScreen = Screen.FromRectangle(
                    new System.Drawing.Rectangle((int)Left, (int)Top, (int)Width, (int)Height));
            }
        }

        /// <summary>
        /// EventHandler for closing the main window. Tries to save settings to registry, fails quietly.
        /// Disposes of the notify icon.
        /// </summary>
        private void MainWindowOnClosing(object sender, CancelEventArgs e)
        {
            try
            {
                WriteSettingsToRegistry();
            }
            catch
            {
                // ignored
            }

            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }

        /// <summary>
        /// Sets _currentScreen to newScreen.
        /// Updates the window position to _currentCorner on _currentScreen.
        /// </summary>
        /// <param name="newScreen">The new screen to place the window on.</param>
        private void UpdateWindowPosition(Screen newScreen)
        {
            _currentScreen = newScreen;
            _currentCorner = _currentCorner == Corner.Custom ? Corner.None : _currentCorner;
            UpdateWindowPosition();
        }
        
        /// <summary>
        /// Sets _currentCorner to newCorner.
        /// Updates the window position to _currentCorner on _currentScreen.
        /// </summary>
        /// <param name="newCorner">The new corner to place the window in.</param>
        private void UpdateWindowPosition(Corner newCorner)
        {
            _currentCorner = newCorner;
            UpdateWindowPosition();   
        }
        
        /// <summary>
        /// Sets _currentScreen to newScreen and _currentCorner to newCorner.
        /// Updates the window position to _currentCorner on _currentScreen.
        /// </summary>
        /// <param name="newScreen">The new screen to place the window on.</param>
        /// <param name="newCorner">The new corner to place the window in.</param>
        private void UpdateWindowPosition(Screen newScreen, Corner newCorner)
        {
            _currentScreen = newScreen;
            _currentCorner = newCorner;
            UpdateWindowPosition();
        }
        
        /// <summary>
        /// If _currentCorner is TopLeft, TopRight, BottomLeft, or BottomRight sets the window position
        /// to _currentCorner on _currentScreen,
        /// else if _currentCorner is Custom sets the window position to _customPosition,
        /// else sets the window position to the centre of _currentScreen. 
        /// </summary>
        private void UpdateWindowPosition()
        {
            (Left, Top) = _currentCorner switch
            {
                Corner.Custom => (_customPosition.x, _customPosition.y),
                Corner.TopLeft => ( _currentScreen.Bounds.Left, _currentScreen.Bounds.Top),
                Corner.TopRight => (_currentScreen.Bounds.Right - Width, _currentScreen.Bounds.Top),
                Corner.BottomLeft => (_currentScreen.Bounds.Left, _currentScreen.Bounds.Bottom - Height),
                Corner.BottomRight => (_currentScreen.Bounds.Right - Width, _currentScreen.Bounds.Bottom - Height),
                _ => ((_currentScreen.Bounds.Right) - (_currentScreen.Bounds.Width / 2) - (Width / 2), 
                      (_currentScreen.Bounds.Bottom) - (_currentScreen.Bounds.Height / 2)- (Height / 2))
            };
        }

        /// <summary>
        /// Reads settings from registry. If registry key is not found a new key with default values will be created.
        /// </summary>
        private void ReadSettingsFromRegistry()
        {
            using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(IllyaRegistryKey) 
                                            ?? CreateRegistryKeyWithDefaultValues();
            
            // TODO: check for faulty values
            int currentScreenIndex = (int) registryKey.GetValue(KeyValueCurrentScreenInt);
            int customPosScreenIndex = (int) registryKey.GetValue(KeyValueCustomPosScreenInt);
            _currentScreen = Screen.AllScreens[currentScreenIndex];
            _currentCorner = (Corner) registryKey.GetValue(KeyValueCurrentCornerInt);
            _customPosition = 
                (double.Parse(registryKey.GetValue(KeyValueCustomPosXDouble).ToString()), 
                 double.Parse(registryKey.GetValue(KeyValueCustomPosYDouble).ToString()),
                 Screen.AllScreens[customPosScreenIndex]);
            _alwaysOnTop = bool.Parse(registryKey.GetValue(KeyValueAlwaysOnTopBool).ToString());
            
            registryKey.Close();
        }

        /// <summary>
        /// Creates a new settings subkey and sets all values to the default values.
        /// </summary>
        /// <returns>A new subkey with write access, with default values.</returns>
        /// <exception cref="NullReferenceException">CreateSubKey returned null without throwing an exception.</exception>
        private RegistryKey CreateRegistryKeyWithDefaultValues()
        {
            RegistryKey registryKey = Registry.CurrentUser.CreateSubKey(IllyaRegistryKey);

            if (registryKey == null) throw new NullReferenceException();

            registryKey.SetValue(KeyValueCurrentScreenInt, DefaultCurrentScreenIndex);
            registryKey.SetValue(KeyValueCurrentCornerInt, (int)DefaultCurrentCorner);
            registryKey.SetValue(KeyValueCustomPosXDouble, DefaultCustomPosX);
            registryKey.SetValue(KeyValueCustomPosYDouble, DefaultCustomPosY);
            registryKey.SetValue(KeyValueCustomPosScreenInt, DefaultCustomPosScreenIndex);
            registryKey.SetValue(KeyValueAlwaysOnTopBool, DefaultAlwaysOnTop);
            
            return registryKey;
        }

        /// <summary>
        /// Write settings to the registry. If registry key is not found a new key with default values will be created.
        /// </summary>
        private void WriteSettingsToRegistry()
        {
            using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(IllyaRegistryKey, true) ??
                                            CreateRegistryKeyWithDefaultValues();
            
            int currentScreen = Array.IndexOf(Screen.AllScreens, _currentScreen);
            int customScreen = Array.IndexOf(Screen.AllScreens, _customPosition.screen);

            registryKey.SetValue(KeyValueCurrentScreenInt, currentScreen < 0 ? 0 : currentScreen);
            registryKey.SetValue(KeyValueCurrentCornerInt, (int) _currentCorner);
            registryKey.SetValue(KeyValueCustomPosXDouble, _customPosition.x);
            registryKey.SetValue(KeyValueCustomPosYDouble, _customPosition.y);
            registryKey.SetValue(KeyValueCustomPosScreenInt, customScreen < 0 ? 0 : customScreen);
            registryKey.SetValue(KeyValueAlwaysOnTopBool, _alwaysOnTop);
                
            registryKey.Close();
        }
    }
}