using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Windowing;
using Windows.Graphics;

namespace Greenhouse_Control
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            ExtendsContentIntoTitleBar = true;

            if (Content is FrameworkElement root)
            {
                root.Loaded += OnRootLoaded;
                root.SizeChanged += OnRootSizeChanged;
            }
        }

        private void OnRootLoaded(object sender, RoutedEventArgs e)
        {
            ResizeToContent();
        }

        private void OnRootSizeChanged(object sender, SizeChangedEventArgs e)
        {
            ResizeToContent();
        }

        private void ResizeToContent()
        {
            if (Content is not FrameworkElement root || root.XamlRoot is null)
            {
                return;
            }

            root.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            var scale = root.XamlRoot.RasterizationScale;
            var desiredWidth = (int)Math.Ceiling(root.DesiredSize.Width * scale);
            var desiredHeight = (int)Math.Ceiling(root.DesiredSize.Height * scale);

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);

            var chromeWidth = appWindow.Size.Width - appWindow.ClientSize.Width;
            var chromeHeight = appWindow.Size.Height - appWindow.ClientSize.Height;

            var targetSize = new SizeInt32(desiredWidth + chromeWidth, desiredHeight + chromeHeight);
            if (appWindow.Size.Width != targetSize.Width || appWindow.Size.Height != targetSize.Height)
            {
                appWindow.Resize(targetSize);
            }
        }
    }
}
