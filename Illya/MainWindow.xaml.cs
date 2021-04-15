/*
    File:       MainWindow.xaml.cs
    Version:    0.5.2
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
using System.Reflection;
using System.Windows.Interop;
using Microsoft.Win32;

namespace Illya
{
    /// <summary>
    /// Represents a position on the screen,
    /// either one of the four corners of the screen or a position that is not in a corner.
    /// </summary>
    internal enum Corner
    {
        /// <summary>Represents a default value that is not a position on a screen.</summary>
        None,
        /// <summary>Represents the top left corner of a screen.</summary>
        TopLeft,
        /// <summary>Represents the top right corner of a screen.</summary>
        TopRight,
        /// <summary>Represents the bottom left corner of a screen.</summary>
        BottomLeft,
        /// <summary>Represents the bottom right corner of a screen.</summary>
        BottomRight,
        /// <summary>Represents a position on a screen other than one of the four corners.</summary>
        Custom
    }
    
    /// <summary>
    /// Class for extensions.
    /// </summary>
    internal static class Extensions
    {
        /// <summary>
        /// Extension for iterating over a collection with indices.
        /// </summary>
        /// <param name="self">The collection to iterate over.</param>
        /// <typeparam name="T">The type of the objects contained in <paramref name="self"/>.</typeparam>
        /// <returns>An <see cref="IEnumerable{T}">IEnumerable</see> containing the elements from <paramref name="self"/>
        /// paired with their indices, or a new empty list if <paramref name="self"/> is null.</returns>
        /// <remarks><a href="https://stackoverflow.com/questions/43021/how-do-you-get-the-index-of-the-current-iteration-of-a-foreach-loop#comment93800836_39997157">Copied from stackoverflow</a></remarks>
        public static IEnumerable<(T item, int index)> WithIndex<T>(this IEnumerable<T> self) => 
            self?.Select((item, index) => (item, index)) ?? new List<(T, int)>();
        
        /// <summary>
        /// Extension for strings to put spaces between CamelCased words.
        /// </summary>
        /// <param name="input">The string to process.</param>
        /// <returns>A new string containing <paramref name="input"/> with spaces inserted between CamelCased words.</returns>
        /// <remarks><a href="https://stackoverflow.com/a/155486/9852711">Copied from stackoverflow.</a></remarks>
        public static string SpaceCamelCase(this String input)
        {
            return new string(Enumerable.Concat(
                input.Take(1), // No space before initial cap
                InsertSpacesBeforeCaps(input.Skip(1))
            ).ToArray());
        }
        
        /// <summary>
        /// A method for inserting spaces before capital letters in a collection of chars.
        /// </summary>
        /// <param name="input">An <see cref="IEnumerable{T}">IEnumerable</see>
        /// containing the collection to process.</param>
        /// <returns>An <see cref="IEnumerable{T}">IEnumerable</see> containing the characters in input
        /// with spaces inserted before capital letters.</returns>
        /// <remarks><a href="https://stackoverflow.com/a/155486/9852711">Copied from stackoverflow.</a></remarks>
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
        /// <summary>The path to the registry key to store settings in.</summary>
        private const string IllyaRegistryKey = @"SOFTWARE\Illya";
        /// <summary>The name of the name/value pair storing the current screen setting.</summary>
        private const string KeyValueCurrentScreenInt = "currentScreenInt";
        /// <summary>The name of the name/value pair storing the current corner setting.</summary>
        private const string KeyValueCurrentCornerInt = "curentCornerInt";
        /// <summary>The name of the name/value pair storing the x component of the custom position setting.</summary>
        private const string KeyValueCustomPosXDouble = "customPosXDouble";
        /// <summary>The name of the name/value pair storing the y component of the custom position setting.</summary>
        private const string KeyValueCustomPosYDouble = "customPosYDouble";
        /// <summary>The name of the name/value pair storing the screen component of the custom position setting.</summary>
        private const string KeyValueCustomPosScreenInt = "customPosScreenInt";
        /// <summary>The name of the name/value pair storing the always on top setting.</summary>
        private const string KeyValueAlwaysOnTopBool = "alwaysOnTopBool";

        /// <summary>The default value of the current screen setting.</summary>
        private const int DefaultCurrentScreenIndex = 0;
        /// <summary>The default value of the current corner setting.</summary>
        private const Corner DefaultCurrentCorner = Corner.None;
        /// <summary>The default value of the x component of the custom position setting.</summary>
        private const double DefaultCustomPosX = 0D;
        /// <summary>The default value of the y component of the custom position setting.</summary>
        private const double DefaultCustomPosY = 0D;
        /// <summary>The default value of the screen component of the custom position setting.</summary>
        private const int DefaultCustomPosScreenIndex = 0;
        /// <summary>The default value of the always on top setting.</summary>
        private const bool DefaultAlwaysOnTop = true;
        
        /// <summary>The version number of the application.</summary>
        private readonly string _version = "";
        /// <summary>The name of the application.</summary>
        private readonly string _name = "Illya";
        
        /// <summary>The applications notify icon.</summary>
        private readonly NotifyIcon _notifyIcon;
        
        /// <summary>The screen the main window is currently on.</summary>
        private Screen _currentScreen;
        /// <summary>The corner the main window is currently in.</summary>
        private Corner _currentCorner;
        /// <summary>A position that is not in a corner of a screen.</summary>
        private (double x, double y, Screen screen) _customPosition;
        /// <summary>Indicating if the main window appears in the topmost z-order.</summary>
        private bool _alwaysOnTop;
        
        /// <summary>
        /// The constructor initialises the main window, gets the name and version from the assembly,
        /// creates the notify icon, loads the settings from the registry, sets the position of the window,
        /// and creates a context menu for the notify icon.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            
            Assembly assembly = Assembly.GetEntryAssembly();
            if (assembly != null)
            {
                _version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                               ?.InformationalVersion ?? _version;
                _name = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? _name;
            }

            _notifyIcon = new NotifyIcon
            {
                Icon = System.Drawing.Icon.FromHandle(Illya.Resources.IllyaIcon.Handle),
                Visible = true,
                Text = _name
            };

            try
            {
                // Read settings from registry, if registry key is not found create a new with default settings values.
                ReadSettingsFromRegistry();
            }
            catch (Exception e)
            {
                // Can't access registry, using default values.
                // TODO: Show popup
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
            ToolStripMenuItem nameMenuItem = new ToolStripMenuItem {Text = $"{_name} {_version}", Enabled = false};
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
        /// <see cref="EventHandler">EventHandler</see> for clicking the Exit menu item in the notify icons context menu.
        /// <para>Exits the application by closing the main window.</para>
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An object that contains no event data.</param>
        private void ContextMenuExit(object sender, EventArgs e)
        {
            Close();
        }
        
        /// <summary>
        /// <see cref="EventHandler">EventHandler</see> for the always on top context menu item.
        /// <para>Toggles <see cref="MainWindow.Topmost"/>.</para>
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An object that contains no event data.</param>
        private void ContextMenuAlwaysOnTop(object sender, EventArgs e)
        {
            _alwaysOnTop = !_alwaysOnTop;
            Topmost = _alwaysOnTop;
        }
        
        /// <summary>
        /// <see cref="EventHandler">EventHandler</see> for the save custom position context menu item.
        /// <para>Saves the current window position and screen to <see cref="_customPosition"/>.</para>
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An object that contains no event data.</param>
        private void ContextMenuSaveCustomPosition(object sender, EventArgs e)
        {
            _customPosition = (Left, Top, _currentScreen);
        }

        /// <summary>
        /// <see cref="EventHandler">EventHandler</see> for the move to custom position context menu item.
        /// <para>Loads the window position and screen from <see cref="_customPosition"/>.</para>
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An object that contains no event data.</param>
        private void ContextMenuLoadCustomPosition(object sender, EventArgs e)
        {
            UpdateWindowPosition(_customPosition.screen, Corner.Custom);
        }
        
        /// <summary>
        /// <see cref="MouseButtonEventHandler">EventHandler</see> for left clicking to drag the main window.
        /// <para>If the window position after the move is not equal to the position before the move,
        /// sets <see cref="_currentCorner"/> to <see cref="Corner.Custom"/>,
        /// and sets <see cref="_currentScreen"/> to the screen that contains the largest portion of the window.</para>
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An object that contains data for mouse button events.</param>
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
        /// <see cref="CancelEventHandler">EventHandler</see> for closing the main window.
        /// <para>Tries to save settings to registry, and disposes of the notify icon.</para>
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An object that contains data for a cancelable event.</param>
        /// <remarks>If settings cannot be saved to registry it will fail quietly.</remarks>
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
        /// Updates the window position to <see cref="_currentCorner"/> on <paramref name="newScreen"/>.
        /// </summary>
        /// <param name="newScreen">The new screen to place the window on.</param>
        private void UpdateWindowPosition(Screen newScreen)
        {
            _currentScreen = newScreen;
            _currentCorner = _currentCorner == Corner.Custom ? Corner.None : _currentCorner;
            UpdateWindowPosition();
        }
        
        /// <summary>
        /// Updates the window position to <paramref name="newCorner"/> on <see cref="_currentScreen"/>.
        /// </summary>
        /// <param name="newCorner">The new corner to place the window in.</param>
        private void UpdateWindowPosition(Corner newCorner)
        {
            _currentCorner = newCorner;
            UpdateWindowPosition();   
        }
        
        /// <summary>
        /// Updates the window position to <paramref name="newCorner"/> on <paramref name="newScreen"/>.
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
        /// Updates the window position.
        /// <para>
        ///     If <see cref="_currentCorner"/> is <see cref="Corner.TopLeft"/>, <see cref="Corner.TopRight"/>,
        ///     <see cref="Corner.BottomLeft"/>, or <see cref="Corner.BottomRight"/> sets the window position to
        ///     <see cref="_currentCorner"/> on <see cref="_currentScreen"/>.
        /// </para>
        /// <para>
        ///     Else if <see cref="_currentCorner"/> is <see cref="Corner.Custom"/> sets the window position
        ///     to <see cref="_customPosition"/>.
        /// </para>
        /// <para>Else sets the window position to the centre of <see cref="_currentScreen"/>.</para>
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
        /// Reads settings from registry.
        /// <para>If registry key is not found a new key with default values will be created.
        /// If a settings value is unreadable or invalid the default value is used.</para>
        /// </summary>
        private void ReadSettingsFromRegistry()
        {
            using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(IllyaRegistryKey) 
                                            ?? CreateRegistryKeyWithDefaultValues();

            try
            {
                int currentScreenIndex = (int) registryKey.GetValue(KeyValueCurrentScreenInt);
                _currentScreen = Screen.AllScreens[currentScreenIndex];
            }
            catch
            {
                _currentScreen = Screen.AllScreens[DefaultCurrentScreenIndex];
            }

            try
            {
                _currentCorner = (Corner) registryKey.GetValue(KeyValueCurrentCornerInt);
                _currentCorner = Enum.IsDefined(typeof(Corner), _currentCorner) ? _currentCorner : DefaultCurrentCorner;
            }
            catch
            {
                _currentCorner = DefaultCurrentCorner;
            }
            
            try
            {
                int customPosScreenIndex = (int) registryKey.GetValue(KeyValueCustomPosScreenInt);
                double x = double.Parse(registryKey.GetValue(KeyValueCustomPosXDouble).ToString());
                double y = double.Parse(registryKey.GetValue(KeyValueCustomPosYDouble).ToString());
                _customPosition = (x, y, Screen.AllScreens[customPosScreenIndex]);
            }
            catch
            {
                _customPosition = (DefaultCustomPosX, DefaultCustomPosY, Screen.AllScreens[DefaultCustomPosScreenIndex]);
            }

            try
            {
                _alwaysOnTop = bool.Parse(registryKey.GetValue(KeyValueAlwaysOnTopBool).ToString());
            }
            catch
            {
                _alwaysOnTop = DefaultAlwaysOnTop;
            }
            
            registryKey.Close();
        }

        /// <summary>
        /// Creates a new settings subkey and sets all settings to the default values.
        /// </summary>
        /// <returns>A new subkey with write access, with settings set to default values.</returns>
        /// <exception cref="NullReferenceException"><see cref="RegistryKey.CreateSubKey(String)">CreateSubKey</see>
        /// returned null without throwing an exception.</exception>
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
        /// Write settings to the registry.
        /// <para>If registry key is not found, a new key with settings at default values will be created.</para>
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