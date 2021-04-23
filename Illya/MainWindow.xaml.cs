/*
    File:       MainWindow.xaml.cs
    Version:    0.7.0
    Author:     Robert Rosborg
 
 */

#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Windows.Input;
using System.Windows.Forms;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Win32;
using static Illya.RegistryReader;

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
        public static IEnumerable<(T item, int index)> WithIndex<T>([AllowNull]this IEnumerable<T> self) => 
            self?.Select((item, index) => (item, index)) ?? new List<(T, int)>();
        
        /// <summary>
        /// Extension for strings to put spaces between CamelCased words.
        /// </summary>
        /// <param name="input">The string to process.</param>
        /// <returns>A new string containing <paramref name="input"/> with spaces inserted between CamelCased words.</returns>
        /// <remarks><a href="https://stackoverflow.com/a/155486/9852711">Copied from stackoverflow.</a></remarks>
        public static string SpaceCamelCase(this String input)
        {
            return new(Enumerable.Concat(
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
        
        /// <summary>
        /// Extension for retrieving a substring from between two other substrings.
        /// </summary>
        /// <param name="input">The string to search.</param>
        /// <param name="start">The first substring.</param>
        /// <param name="stop">The second substring.</param>
        /// <returns>The substring between <paramref name="start"/> and <paramref name="stop"/> in
        /// <paramref name="input"/>, or the empty string if no such substring is found.</returns>
        /// <remarks>If <paramref name="input"/>, <paramref name="start"/>, or <paramref name="stop"/> is null
        /// the empty string will be used instead.</remarks>
        public static string GetBetween(this string? input, string? start, string? stop)
        {
            input ??= string.Empty;
            start ??= string.Empty;
            stop ??= string.Empty;
            
            if (input.Contains(start) && input.Contains(stop)) // TODO: Optimize
            {
                int startIndex = input.IndexOf(start, 0, StringComparison.Ordinal) + start.Length;
                int stopIndex = input.IndexOf(stop, startIndex, StringComparison.Ordinal);
                return input.Substring(startIndex, stopIndex - startIndex);
            }

            return string.Empty;
        }
    }
    
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        /// <summary>The path to the registry key to store settings in.</summary>
        private const string IllyaRegistryKey = @"SOFTWARE\Illya";
        /// <summary>The name of the name/value pair storing the current screen setting.</summary>
        private const string KeyNameCurrentScreenInt = "currentScreenInt";
        /// <summary>The name of the name/value pair storing the current corner setting.</summary>
        private const string KeyNameCurrentCornerInt = "curentCornerInt";
        /// <summary>The name of the name/value pair storing the x component of the custom position setting.</summary>
        private const string KeyNameCustomPosXDouble = "customPosXDouble";
        /// <summary>The name of the name/value pair storing the y component of the custom position setting.</summary>
        private const string KeyNameCustomPosYDouble = "customPosYDouble";
        /// <summary>The name of the name/value pair storing the screen component of the custom position setting.</summary>
        private const string KeyNameCustomPosScreenInt = "customPosScreenInt";
        /// <summary>The name of the name/value pair storing the always on top setting.</summary>
        private const string KeyNameAlwaysOnTopBool = "alwaysOnTopBool";

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
        private Screen _currentScreen = Screen.PrimaryScreen;
        /// <summary>The corner the main window is currently in.</summary>
        private Corner _currentCorner = Corner.None;
        /// <summary>A position that is not in a corner of a screen.</summary>
        private (double x, double y, Screen screen) _customPosition = (0D, 0D, Screen.PrimaryScreen);
        /// <summary>Indicating if the main window appears in the topmost z-order.</summary>
        private bool _alwaysOnTop = true;

        /// <summary>The web address to MPC-HC's web interface.</summary>
        private string webInterfaceAddress = "http://127.0.0.1";
        /// <summary>The port MPC-HC's web interface is listening on.</summary>
        private int port = 13579;

        /// <summary>The <see cref="Updater"/> responsible for updating the UI with the now playing
        /// variables from MPC-HC.</summary>
        private Updater _updater;
        /// <summary>The <see cref="Task"/> that runs the loop for updating the UI with the now playing
        /// variables from MPC-HC.</summary>
        private Task _updateTask;
        
        /// <summary>
        /// The constructor initialises the main window, gets the name and version from the assembly,
        /// creates the notify icon, loads the settings from the registry, sets the position of the window,
        /// and creates a context menu for the notify icon.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            
            Assembly? assembly = Assembly.GetEntryAssembly();
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
            catch (RegistryErrorException e)
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

            _updater = new Updater(TimeTextBlock, VideoNameTextBlock, PlaytimeTextBlock, PlaytimeProgressBar,
                new Uri($"{webInterfaceAddress}:{port}/"), TimeSpan.FromSeconds(0.5));
            // TODO: Set address and port from loaded settings
            _updateTask = Task.Run(_updater.StartUpdateLoop);
        }

        /// <summary>
        /// Sets up the context menu for the notify icon.
        /// </summary>
        private void CreateNotifyIconContextMenu()
        {
            ContextMenuStrip notifyIconContextMenu = new();

            // Name and version
            ToolStripMenuItem nameMenuItem = new() {Text = $"{_name} {_version}", Enabled = false};
            notifyIconContextMenu.Items.Add(nameMenuItem);
            
            // Settings submenu
            ToolStripMenuItem settingsMenuItem = new() {Text = "Settings"};
            notifyIconContextMenu.Items.Add(settingsMenuItem);

            // Settings -> Always on top
            ToolStripMenuItem alwaysOnTop = new()
            {
                Text = "Always on top", Checked = _alwaysOnTop, CheckOnClick = true
            };
            alwaysOnTop.Click += ContextMenuAlwaysOnTop;
            settingsMenuItem.DropDownItems.Add(alwaysOnTop);


            settingsMenuItem.DropDownItems.Add("-");

            // Settings -> Screens
            foreach ((Screen screen, int index) in Screen.AllScreens.WithIndex())
            {
                ToolStripMenuItem menuItem = new() {Text = $"Move to display {index}"};
                menuItem.Click += (sender, args) => { UpdateWindowPosition(screen); };
                settingsMenuItem.DropDownItems.Add(menuItem);
            }
            
            settingsMenuItem.DropDownItems.Add("-");

            // Settings -> Corners
            foreach (Corner corner in Enum.GetValues<Corner>())
            {
                if (corner is Corner.None or Corner.Custom) continue;
                ToolStripMenuItem menuItem = new()
                {
                    Text = $"Place in {Enum.GetName(corner)?.SpaceCamelCase().ToLower()} corner"
                };
                menuItem.Click += (sender, args) => { UpdateWindowPosition(corner); };
                settingsMenuItem.DropDownItems.Add(menuItem);
            }
            
            settingsMenuItem.DropDownItems.Add("-");

            // Settings -> Custom Position
            ToolStripMenuItem saveCustomMenuItem = new() { Text = "Save custom position"};
            saveCustomMenuItem.Click += ContextMenuSaveCustomPosition;
            settingsMenuItem.DropDownItems.Add(saveCustomMenuItem);

            ToolStripMenuItem loadCustomMenuItem = new() { Text = "Move to custom position"};
            loadCustomMenuItem.Click += ContextMenuLoadCustomPosition;
            settingsMenuItem.DropDownItems.Add(loadCustomMenuItem);
            
            // Exit
            ToolStripMenuItem exitMenuItem = new() {Text = "Exit"};
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
        private void ContextMenuExit(object? sender, EventArgs e)
        {
            Close();
        }
        
        /// <summary>
        /// <see cref="EventHandler">EventHandler</see> for the always on top context menu item.
        /// <para>Toggles <see cref="MainWindow.Topmost"/>.</para>
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An object that contains no event data.</param>
        private void ContextMenuAlwaysOnTop(object? sender, EventArgs e)
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
        private void ContextMenuSaveCustomPosition(object? sender, EventArgs e)
        {
            _customPosition = (Left, Top, _currentScreen);
        }

        /// <summary>
        /// <see cref="EventHandler">EventHandler</see> for the move to custom position context menu item.
        /// <para>Loads the window position and screen from <see cref="_customPosition"/>.</para>
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An object that contains no event data.</param>
        private void ContextMenuLoadCustomPosition(object? sender, EventArgs e)
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
        private void MoveWindow(object? sender, MouseButtonEventArgs e)
        {
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
        private void MainWindowOnClosing(object? sender, CancelEventArgs e)
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
        /// <exception cref="RegistryErrorException">Unable to open or create new subkey.</exception>
        private void ReadSettingsFromRegistry()
        {
            RegistryKey registryKey;
            try
            {
                registryKey = Registry.CurrentUser.OpenSubKey(IllyaRegistryKey) ?? CreateRegistryKeyWithDefaultValues();
            }
            catch (Exception e) when 
            (e is System.Security.SecurityException 
                or ObjectDisposedException 
                or System.IO.IOException 
                or UnauthorizedAccessException
                or RegistryErrorException)
            {
                throw new RegistryErrorException("An exception was thrown while accessing the registry.", e);
            }

            // Current screen
            int currentScreenIndex = ReadIntFromRegistry(registryKey, KeyNameCurrentScreenInt, DefaultCurrentScreenIndex);
            _currentScreen = currentScreenIndex >= 0 && currentScreenIndex < Screen.AllScreens.Length
                ? Screen.AllScreens[currentScreenIndex]
                : Screen.AllScreens[DefaultCurrentScreenIndex];
            
            // Current corner
            Corner currentCorner = (Corner) ReadIntFromRegistry(registryKey, KeyNameCurrentCornerInt, (int)DefaultCurrentCorner);
            _currentCorner = Enum.IsDefined(typeof(Corner), currentCorner) ? currentCorner : DefaultCurrentCorner;
            
            // Custom position
            int customPosScreenIndex = ReadIntFromRegistry(registryKey, KeyNameCustomPosScreenInt, DefaultCustomPosScreenIndex);
            double x = ReadDoubleFromRegistry(registryKey, KeyNameCustomPosXDouble, DefaultCustomPosX);
            double y = ReadDoubleFromRegistry(registryKey, KeyNameCustomPosYDouble, DefaultCustomPosY);
            _customPosition = customPosScreenIndex >= 0 && customPosScreenIndex < Screen.AllScreens.Length
                ? _customPosition = (x, y, Screen.AllScreens[customPosScreenIndex])
                : _customPosition = (DefaultCustomPosX, DefaultCustomPosY,
                    Screen.AllScreens[DefaultCustomPosScreenIndex]);
            
            // Always on top
            _alwaysOnTop = ReadBoolFromRegistry(registryKey, KeyNameAlwaysOnTopBool, DefaultAlwaysOnTop);
            
            registryKey.Close();
            registryKey.Dispose();
        }

        /// <summary>
        /// Creates a new settings subkey and sets all settings to the default values.
        /// </summary>
        /// <returns>A new subkey with write access, with settings set to default values.</returns>
        /// <exception cref="RegistryErrorException"><see cref="RegistryKey.CreateSubKey(string)">CreateSubKey</see>
        /// returned null.</exception>
        private RegistryKey CreateRegistryKeyWithDefaultValues()
        {
            RegistryKey registryKey = Registry.CurrentUser.CreateSubKey(IllyaRegistryKey) ??
                                      throw new RegistryErrorException("Failed to create new subkey.");
            
            WriteValueToRegistry(registryKey, KeyNameCurrentScreenInt, DefaultCurrentScreenIndex);
            WriteValueToRegistry(registryKey, KeyNameCurrentCornerInt, (int)DefaultCurrentCorner);
            WriteValueToRegistry(registryKey, KeyNameCustomPosXDouble, DefaultCustomPosX);
            WriteValueToRegistry(registryKey, KeyNameCustomPosYDouble, DefaultCustomPosY);
            WriteValueToRegistry(registryKey, KeyNameCustomPosScreenInt, DefaultCustomPosScreenIndex);
            WriteValueToRegistry(registryKey, KeyNameAlwaysOnTopBool, DefaultAlwaysOnTop);
            
            return registryKey;
        }

        /// <summary>
        /// Write settings to the registry.
        /// <para>If registry key is not found, a new key with settings at default values will be created.</para>
        /// </summary>
        /// <exception cref="ArgumentNullException"><see cref="RegistryKey.OpenSubKey(string, bool)">OpenSubKey</see>
        /// was called with a null argument.</exception>
        /// <exception cref="RegistryErrorException">Unable to open subkey, write to subkey,
        /// or create new subkey.</exception>
        private void WriteSettingsToRegistry()
        {
            RegistryKey registryKey;
            try
            {
                registryKey = Registry.CurrentUser.OpenSubKey(IllyaRegistryKey, true) ??
                              CreateRegistryKeyWithDefaultValues();
            }
            catch (Exception e) when 
            (e is System.Security.SecurityException 
                or ObjectDisposedException 
                or System.IO.IOException 
                or UnauthorizedAccessException
                or RegistryErrorException)
            {
                throw new RegistryErrorException("An exception was thrown while accessing the registry.", e);
            }

            int currentScreen = Array.IndexOf(Screen.AllScreens, _currentScreen);
            int customScreen = Array.IndexOf(Screen.AllScreens, _customPosition.screen);

            WriteValueToRegistry(registryKey, KeyNameCurrentScreenInt, currentScreen < 0 ? 0 : currentScreen);
            WriteValueToRegistry(registryKey, KeyNameCurrentCornerInt, (int) _currentCorner);
            WriteValueToRegistry(registryKey, KeyNameCustomPosXDouble, _customPosition.x);
            WriteValueToRegistry(registryKey, KeyNameCustomPosYDouble, _customPosition.y);
            WriteValueToRegistry(registryKey, KeyNameCustomPosScreenInt, customScreen < 0 ? 0 : customScreen);
            WriteValueToRegistry(registryKey, KeyNameAlwaysOnTopBool, _alwaysOnTop);
            
            registryKey.Close();
            registryKey.Dispose();
        }
    }
}