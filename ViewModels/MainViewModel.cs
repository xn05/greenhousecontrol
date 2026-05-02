using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Greenhouse_Control.ViewModels
{
    public sealed class MainViewModel : INotifyPropertyChanged
    {
        private string _ipAddress = "";
        private string _portText = "12345";
        private bool _isConnected;
        private string _statusText = "Not connected.";
        private bool _isLightingOn;
        private bool _isAutoWaterOn;
        private int _frequencyHours = 12;
        private int _dispenseSeconds = 10;
        private bool _isReady;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string IpAddress
        {
            get => _ipAddress;
            set => SetProperty(ref _ipAddress, value);
        }

        public string PortText
        {
            get => _portText;
            set => SetProperty(ref _portText, value);
        }

        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                if (!SetProperty(ref _isConnected, value))
                {
                    return;
                }

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDisconnected)));
            }
        }

        public bool IsDisconnected => !_isConnected;

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public bool IsLightingOn
        {
            get => _isLightingOn;
            set => SetProperty(ref _isLightingOn, value);
        }

        public bool IsAutoWaterOn
        {
            get => _isAutoWaterOn;
            set => SetProperty(ref _isAutoWaterOn, value);
        }

        public int FrequencyHours
        {
            get => _frequencyHours;
            set => SetProperty(ref _frequencyHours, Clamp(value, 1, 48));
        }

        public int DispenseSeconds
        {
            get => _dispenseSeconds;
            set => SetProperty(ref _dispenseSeconds, Clamp(value, 1, 60));
        }

        public bool IsReady
        {
            get => _isReady;
            set => SetProperty(ref _isReady, value);
        }

        public bool TryGetPort(out int port)
        {
            return int.TryParse(PortText, out port) && port is > 0 and < 65536;
        }

        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value))
            {
                return false;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }

        private static int Clamp(int value, int min, int max)
        {
            return Math.Min(Math.Max(value, min), max);
        }
    }
}
