﻿/*
    File:       MainWindow.xaml.cs
    Version:    0.1.0
    Author:     Robert Rosborg
 
 */

using System;
using System.Collections.Generic;
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
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private NotifyIcon notifyIcon;
        private System.Windows.Forms.ContextMenuStrip notifyIconContextMenu;

        
        public MainWindow()
        {
            InitializeComponent();

            notifyIcon = new NotifyIcon
            {
                Icon = System.Drawing.Icon.FromHandle(Illya.Resources.IllyaIcon.Handle),
                Visible = true,
                Text = "Illya"
            };

            notifyIconContextMenu = new ContextMenuStrip();

            ToolStripMenuItem nameMenuItem = new ToolStripMenuItem {Text = "Illya v0.1.0", Enabled = false};
            
            ToolStripMenuItem exitMenuItem = new ToolStripMenuItem {Text = "E&xit"};
            exitMenuItem.Click += ContextMenuExit;

            notifyIconContextMenu.Items.Add(nameMenuItem);
            notifyIconContextMenu.Items.Add(exitMenuItem);
            notifyIcon.ContextMenuStrip = notifyIconContextMenu;
        }
        
        /// <summary>
        /// EventHandler for clicking the Exit menu item in the notify icons context menu.
        /// </summary>
        private void ContextMenuExit(object sender, EventArgs e)
        {
            notifyIcon.Visible = false;
            Close();
        }
        
        /// <summary>
        /// EventHandler for clicking left clicking to drag the main window.
        /// </summary>
        private void MoveWindow(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }
    }
}