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
using Windows.UI;
using System.Text.Json;
using Greenhouse_Control.Models;
using Greenhouse_Control.Services;
using Greenhouse_Control.ViewModels;
using System.Threading.Tasks;

namespace Greenhouse_Control
{
    public sealed partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel = new();
        private readonly GreenhouseClient _client = new();
        private bool _suppressOutgoing;
        private bool _pendingLightChange;
        private bool _pendingLightValue;
        private bool _pendingAutoWaterChange;
        private bool _pendingAutoWaterValue;
        private bool _pendingFrequencyChange;
        private int _pendingFrequencyValue;
        private bool _pendingDispenseChange;
        private int _pendingDispenseValue;
        private static readonly Color ConnectedAccentBorderColor = Color.FromArgb(0xFF, 0x2F, 0x6F, 0x61);
        private static readonly Color DisconnectedAccentBorderColor = Color.FromArgb(0xFF, 0x8D, 0x4A, 0x57);
        private const int DefaultWindowWidth = 590;
        private const int DefaultWindowHeight = 539;
        private const double BackgroundTransitionMilliseconds = 350;
        private Microsoft.UI.Dispatching.DispatcherQueueTimer? _backgroundTransitionTimer;
        private DateTimeOffset _backgroundTransitionStartedAt;
        private double _connectedBackgroundStartOpacity;
        private double _connectedBackgroundEndOpacity;
        private double _disconnectedBackgroundStartOpacity;
        private double _disconnectedBackgroundEndOpacity;
        private Color _accentBorderStartColor;
        private Color _accentBorderEndColor;

        public MainWindow()
        {
            InitializeComponent();
            ExtendsContentIntoTitleBar = true;

            _client.LineReceived += OnLineReceived;
            _client.Disconnected += OnDisconnected;

            if (Content is FrameworkElement root)
            {
                root.DataContext = _viewModel;
                root.Loaded += OnRootLoaded;
                root.SizeChanged += OnRootSizeChanged;
            }

            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            UpdateBackgroundState(_viewModel.IsConnected, true);
            SetWindowIcon();
            DisableWindowResize();
            SetDefaultWindowSize();
            UpdateConnectionButton();
        }

        private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.IsConnected))
            {
                UpdateBackgroundState(_viewModel.IsConnected, false);
            }
        }

        private void UpdateBackgroundState(bool isConnected, bool immediate)
        {
            if (ConnectedBackground is null || DisconnectedBackground is null)
            {
                return;
            }

            var connectedOpacity = isConnected ? 1.0 : 0.0;
            var disconnectedOpacity = isConnected ? 0.0 : 1.0;
            var accentBorderColor = isConnected ? ConnectedAccentBorderColor : DisconnectedAccentBorderColor;

            if (immediate)
            {
                _backgroundTransitionTimer?.Stop();
                ConnectedBackground.Opacity = connectedOpacity;
                DisconnectedBackground.Opacity = disconnectedOpacity;
                if (GetConnectionAccentBorderBrush() is { } immediateAccentBorderBrush)
                {
                    immediateAccentBorderBrush.Color = accentBorderColor;
                }

                return;
            }

            StartBackgroundTransition(connectedOpacity, disconnectedOpacity, accentBorderColor);
        }

        private void StartBackgroundTransition(double connectedOpacity, double disconnectedOpacity, Color accentBorderColor)
        {
            _backgroundTransitionTimer?.Stop();

            _connectedBackgroundStartOpacity = ConnectedBackground.Opacity;
            _connectedBackgroundEndOpacity = connectedOpacity;
            _disconnectedBackgroundStartOpacity = DisconnectedBackground.Opacity;
            _disconnectedBackgroundEndOpacity = disconnectedOpacity;
            _accentBorderStartColor = GetConnectionAccentBorderBrush()?.Color ?? accentBorderColor;
            _accentBorderEndColor = accentBorderColor;
            _backgroundTransitionStartedAt = DateTimeOffset.Now;

            _backgroundTransitionTimer ??= DispatcherQueue.CreateTimer();
            _backgroundTransitionTimer.Interval = TimeSpan.FromMilliseconds(16);
            _backgroundTransitionTimer.Tick -= OnBackgroundTransitionTick;
            _backgroundTransitionTimer.Tick += OnBackgroundTransitionTick;
            _backgroundTransitionTimer.Start();
        }

        private void OnBackgroundTransitionTick(Microsoft.UI.Dispatching.DispatcherQueueTimer sender, object args)
        {
            var elapsed = (DateTimeOffset.Now - _backgroundTransitionStartedAt).TotalMilliseconds;
            var progress = Math.Clamp(elapsed / BackgroundTransitionMilliseconds, 0.0, 1.0);
            var easedProgress = progress * progress * (3 - 2 * progress);

            ConnectedBackground.Opacity = Lerp(_connectedBackgroundStartOpacity, _connectedBackgroundEndOpacity, easedProgress);
            DisconnectedBackground.Opacity = Lerp(_disconnectedBackgroundStartOpacity, _disconnectedBackgroundEndOpacity, easedProgress);

            if (GetConnectionAccentBorderBrush() is { } accentBorderBrush)
            {
                accentBorderBrush.Color = LerpColor(_accentBorderStartColor, _accentBorderEndColor, easedProgress);
            }

            if (progress >= 1.0)
            {
                sender.Stop();
            }
        }

        private static double Lerp(double start, double end, double progress)
        {
            return start + ((end - start) * progress);
        }

        private static Color LerpColor(Color start, Color end, double progress)
        {
            return Color.FromArgb(
                (byte)Math.Round(Lerp(start.A, end.A, progress)),
                (byte)Math.Round(Lerp(start.R, end.R, progress)),
                (byte)Math.Round(Lerp(start.G, end.G, progress)),
                (byte)Math.Round(Lerp(start.B, end.B, progress)));
        }

        private SolidColorBrush? GetConnectionAccentBorderBrush()
        {
            return Content is FrameworkElement root &&
                   root.Resources.TryGetValue("ConnectionAccentBorderBrush", out var brush) &&
                   brush is SolidColorBrush solidColorBrush
                ? solidColorBrush
                : null;
        }

        private void DisableWindowResize()
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);

            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsResizable = false;
                presenter.IsMaximizable = false;
                presenter.IsMinimizable = true;
            }
        }

        private void SetWindowIcon()
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            appWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "icon.ico"));
        }

        private void SetDefaultWindowSize()
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            appWindow.Resize(new SizeInt32(DefaultWindowWidth, DefaultWindowHeight));
        }

        private void OnRootLoaded(object sender, RoutedEventArgs e)
        {
            ResizeToContent();
        }

        private void OnRootSizeChanged(object sender, SizeChangedEventArgs e)
        {
            ResizeToContent();
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.IsConnected)
            {
                await _client.DisconnectAsync();
                _viewModel.IsConnected = false;
                _viewModel.IsReady = false;
                _viewModel.StatusText = "Disconnected.";
                UpdateConnectionButton();
                return;
            }

            if (string.IsNullOrWhiteSpace(_viewModel.IpAddress))
            {
                _viewModel.StatusText = "Enter a valid IP address.";
                return;
            }

            if (!_viewModel.TryGetPort(out var port))
            {
                _viewModel.StatusText = "Enter a valid port number.";
                return;
            }

            _viewModel.StatusText = "Connecting...";

            try
            {
                await _client.ConnectAsync(_viewModel.IpAddress, port);
                _viewModel.IsConnected = true;
                _viewModel.IsReady = false;
                _viewModel.StatusText = "Connected.";
                UpdateConnectionButton();
            }
            catch (Exception ex)
            {
                _viewModel.StatusText = $"Connection failed: {ex.Message}";
            }
        }

        private async void LightingToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_suppressOutgoing || !_viewModel.IsConnected)
            {
                return;
            }

            var isOn = (sender as ToggleSwitch)?.IsOn ?? _viewModel.IsLightingOn;
            _pendingLightChange = true;
            _pendingLightValue = isOn;
            var command = isOn ? "LIGHT ON" : "LIGHT OFF";
            await SendCommandAsync(command);
        }

        private async void AutoWaterToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_suppressOutgoing || !_viewModel.IsConnected)
            {
                return;
            }

            var isOn = (sender as ToggleSwitch)?.IsOn ?? _viewModel.IsAutoWaterOn;
            _pendingAutoWaterChange = true;
            _pendingAutoWaterValue = isOn;
            var command = isOn ? "AUTOWATER ON" : "AUTOWATER OFF";
            await SendCommandAsync(command);
        }

        private async void DispenseSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (_suppressOutgoing || !_viewModel.IsConnected)
            {
                return;
            }

            _pendingDispenseChange = true;
            _pendingDispenseValue = (int)Math.Round(e.NewValue);
        }

        private async void FrequencySlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (_suppressOutgoing || !_viewModel.IsConnected)
            {
                return;
            }

            _pendingFrequencyChange = true;
            _pendingFrequencyValue = (int)Math.Round(e.NewValue);
        }

        private async void DispenseSlider_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            await SendDispenseIfPendingAsync(sender as Slider);
        }

        private async void DispenseSlider_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            await SendDispenseIfPendingAsync(sender as Slider);
        }

        private async void DispenseSlider_LostFocus(object sender, RoutedEventArgs e)
        {
            await SendDispenseIfPendingAsync(sender as Slider);
        }

        private async void FrequencySlider_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            await SendFrequencyIfPendingAsync(sender as Slider);
        }

        private async void FrequencySlider_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            await SendFrequencyIfPendingAsync(sender as Slider);
        }

        private async void FrequencySlider_LostFocus(object sender, RoutedEventArgs e)
        {
            await SendFrequencyIfPendingAsync(sender as Slider);
        }

        private async Task SendDispenseIfPendingAsync(Slider? slider)
        {
            if (!_viewModel.IsConnected || !_pendingDispenseChange)
            {
                return;
            }

            var value = slider is null ? _pendingDispenseValue : (int)Math.Round(slider.Value);
            _pendingDispenseValue = value;
            _pendingDispenseChange = false;
            await SendCommandAsync($"DISPENSE {value}");
        }

        private async Task SendFrequencyIfPendingAsync(Slider? slider)
        {
            if (!_viewModel.IsConnected || !_pendingFrequencyChange)
            {
                return;
            }

            var value = slider is null ? _pendingFrequencyValue : (int)Math.Round(slider.Value);
            _pendingFrequencyValue = value;
            _pendingFrequencyChange = false;
            await SendCommandAsync($"FREQUENCY {value}");
        }

        private async void ShutdownButton_Click(object sender, RoutedEventArgs e)
        {
            await SendCommandAsync("SHUTDOWN");
        }

        private async void RestartButton_Click(object sender, RoutedEventArgs e)
        {
            await SendCommandAsync("RESTART");
        }

        private async void PumpToggle_Checked(object sender, RoutedEventArgs e)
        {
            if (!_viewModel.IsConnected)
            {
                return;
            }

            if (sender is ToggleButton toggle)
            {
                toggle.Content = "Stop";
            }

            await SendCommandAsync("PUMP ON");
        }

        private async void PumpToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            if (!_viewModel.IsConnected)
            {
                return;
            }

            if (sender is ToggleButton toggle)
            {
                toggle.Content = "Start";
            }

            await SendCommandAsync("PUMP OFF");
        }

        private void OnLineReceived(string line)
        {
            _ = DispatcherQueue.TryEnqueue(() => ProcessIncomingLine(line));
        }

        private void OnDisconnected()
        {
            _ = DispatcherQueue.TryEnqueue(() =>
            {
                _viewModel.IsConnected = false;
                _viewModel.IsReady = false;
                _viewModel.StatusText = "Disconnected.";
                ResetPendingFlags();
                UpdateConnectionButton();
            });
        }

        private void ProcessIncomingLine(string line)
        {
            try
            {
                if (line.StartsWith("SETTINGS ", StringComparison.OrdinalIgnoreCase))
                {
                    var payload = line.Substring("SETTINGS ".Length);
                    var settings = JsonSerializer.Deserialize<ServerSettings>(payload);
                    if (settings is null)
                    {
                        _viewModel.StatusText = "Received empty settings from server.";
                        return;
                    }

                    _suppressOutgoing = true;

                    if (!_pendingLightChange || settings.LightOn == _pendingLightValue)
                    {
                        _viewModel.IsLightingOn = settings.LightOn;
                        _pendingLightChange = false;
                    }

                    if (!_pendingAutoWaterChange || settings.AutoWaterOn == _pendingAutoWaterValue)
                    {
                        _viewModel.IsAutoWaterOn = settings.AutoWaterOn;
                        _pendingAutoWaterChange = false;
                    }

                    if (!_pendingFrequencyChange || settings.FrequencyHours == _pendingFrequencyValue)
                    {
                        _viewModel.FrequencyHours = settings.FrequencyHours;
                        _pendingFrequencyChange = false;
                    }

                    if (!_pendingDispenseChange || settings.DispenseSeconds == _pendingDispenseValue)
                    {
                        _viewModel.DispenseSeconds = settings.DispenseSeconds;
                        _pendingDispenseChange = false;
                    }

                    _viewModel.IsReady = true;
                    _viewModel.StatusText = "Connected.";
                    _suppressOutgoing = false;
                }
            }
            catch (JsonException ex)
            {
                _suppressOutgoing = false;
                _viewModel.StatusText = $"Bad settings from server: {ex.Message}";
            }
            catch (Exception ex)
            {
                _suppressOutgoing = false;
                _viewModel.StatusText = $"Update failed: {ex.Message}";
            }
        }

        private void ResetPendingFlags()
        {
            _pendingLightChange = false;
            _pendingAutoWaterChange = false;
            _pendingFrequencyChange = false;
            _pendingDispenseChange = false;
        }

        private async Task SendCommandAsync(string command)
        {
            if (!_viewModel.IsConnected)
            {
                return;
            }

            try
            {
                await _client.SendLineAsync(command);
            }
            catch (Exception ex)
            {
                _viewModel.StatusText = $"Send failed: {ex.Message}";
            }
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

            var targetSize = new SizeInt32(
                Math.Max(DefaultWindowWidth, desiredWidth + chromeWidth),
                Math.Max(DefaultWindowHeight, desiredHeight + chromeHeight));
            if (appWindow.Size.Width != targetSize.Width || appWindow.Size.Height != targetSize.Height)
            {
                appWindow.Resize(targetSize);
            }
        }

        private void UpdateConnectionButton()
        {
            if (ConnectButton is null)
            {
                return;
            }

            ConnectButton.Content = _viewModel.IsConnected ? "Disconnect" : "Connect";
        }
    }
}
