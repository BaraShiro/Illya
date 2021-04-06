/*
    File:       MainWindow.xaml.cs
    Version:    0.3.1
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
        BottomRight
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
        private NotifyIcon _notifyIcon;
        private Screen _currentScreen;
        private Corner _currentCorner;

        
        public MainWindow()
        {
            InitializeComponent();

            _notifyIcon = new NotifyIcon
            {
                Icon = System.Drawing.Icon.FromHandle(Illya.Resources.IllyaIcon.Handle),
                Visible = true,
                Text = "Illya"
            };
            
            //TODO: Read from saved settings
            _currentScreen = Screen.PrimaryScreen;
            _currentCorner = Corner.None;
            // TODO: always on top settings should be saved and loaded
            
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
            ToolStripMenuItem nameMenuItem = new ToolStripMenuItem {Text = "Illya v0.3.1", Enabled = false};
            notifyIconContextMenu.Items.Add(nameMenuItem);
            
            // Settings submenu
            ToolStripMenuItem settingsMenuItem = new ToolStripMenuItem {Text = "Settings"};
            notifyIconContextMenu.Items.Add(settingsMenuItem);

            // Settings -> Always on top
            ToolStripMenuItem alwaysOnTop = new ToolStripMenuItem
            {
                Text = "Always on top", Checked = Topmost, CheckOnClick = true
            };
            alwaysOnTop.Click += ContextMenuAlwaysOnTop;
            settingsMenuItem.DropDownItems.Add(alwaysOnTop);


            settingsMenuItem.DropDownItems.Add("-");

            // Settings -> Screens
            foreach ((Screen screen, int index) in Screen.AllScreens.WithIndex())
            {
                ToolStripMenuItem menuItem = new ToolStripMenuItem {Text = $"Show on display {index}"};
                menuItem.Click += (sender, args) => { UpdateWindowPosition(screen); };
                settingsMenuItem.DropDownItems.Add(menuItem);
            }
            
            settingsMenuItem.DropDownItems.Add("-");

            // Settings -> Corners
            foreach (Corner corner in Enum.GetValues<Corner>())
            {
                if (corner == Corner.None) continue;
                ToolStripMenuItem menuItem = new ToolStripMenuItem
                {
                    Text = $"Place in {Enum.GetName(corner).SpaceCamelCase().ToLower()} corner"
                };
                menuItem.Click += (sender, args) => { UpdateWindowPosition(corner); };
                settingsMenuItem.DropDownItems.Add(menuItem);
            }
            
            // Exit
            ToolStripMenuItem exitMenuItem = new ToolStripMenuItem {Text = "Exit"};
            exitMenuItem.Click += ContextMenuExit;
            notifyIconContextMenu.Items.Add(exitMenuItem);
            
            _notifyIcon.ContextMenuStrip = notifyIconContextMenu;
        }

        /// <summary>
        /// EventHandler for clicking the Exit menu item in the notify icons context menu.
        /// </summary>
        private void ContextMenuExit(object sender, EventArgs e)
        {
            Close();
        }
        
        /// <summary>
        /// EventHandler for the always on top context menu item.
        /// </summary>
        private void ContextMenuAlwaysOnTop(object sender, EventArgs e)
        {
            Topmost = !Topmost;
        }
        
        /// <summary>
        /// EventHandler for clicking left clicking to drag the main window.
        /// </summary>
        private void MoveWindow(object sender, MouseButtonEventArgs e)
        {
            DragMove();
            // TODO: Set _currentCorner to None if window moved.
            // TODO: Set _currentScreen to new screen if moved to a new screen.
        }

        /// <summary>
        /// EventHandler for closing the main window.
        /// </summary>
        private void MainWindowOnClosing(object sender, CancelEventArgs e)
        {
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
        /// Updates the window position to _currentCorner on _currentScreen.
        /// </summary>
        private void UpdateWindowPosition()
        {
            (Top, Left) = _currentCorner switch
            {
                Corner.TopLeft => (_currentScreen.Bounds.Top, _currentScreen.Bounds.Left),
                Corner.TopRight => (_currentScreen.Bounds.Top, _currentScreen.Bounds.Right - Width),
                Corner.BottomLeft => (_currentScreen.Bounds.Bottom - Height,_currentScreen.Bounds.Left),
                Corner.BottomRight => (_currentScreen.Bounds.Bottom - Height,_currentScreen.Bounds.Right - Width),
                _ => ((_currentScreen.Bounds.Height / 2) - (Height / 2), (_currentScreen.Bounds.Width / 2) - (Width / 2))
            };
        }
    }
}