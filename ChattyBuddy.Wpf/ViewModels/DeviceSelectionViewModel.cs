using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ChattyBuddy.Wpf.Infrastructure;
using ChattyBuddy.Wpf.Models;
using ChattyBuddy.Wpf.Services;

namespace ChattyBuddy.Wpf.ViewModels
{
    public class DeviceSelectionViewModel : BaseViewModel
    {
        private readonly TailscaleService tailscaleService;
        private readonly SelectedDeviceStore selectedDeviceStore;

        private bool isBusy;
        private string statusMessage;
        private TailscaleDevice selectedDevice;

        public ObservableCollection<TailscaleDevice> Devices { get; } = new ObservableCollection<TailscaleDevice>();

        public TailscaleDevice SelectedDevice
        {
            get => selectedDevice;
            set
            {
                if (SetField(ref selectedDevice, value))
                    RaiseCommandCanExecuteChanged();
            }
        }

        public bool IsBusy
        {
            get => isBusy;
            set
            {
                if (SetField(ref isBusy, value))
                    RaiseCommandCanExecuteChanged();
            }
        }

        public string StatusMessage
        {
            get => statusMessage;
            set => SetField(ref statusMessage, value);
        }

        public ICommand RefreshCommand { get; }
        public ICommand ConnectCommand { get; }

        public event Action<TailscaleDevice> Connected;

        public DeviceSelectionViewModel(TailscaleService tailscaleService, SelectedDeviceStore selectedDeviceStore)
        {
            this.tailscaleService = tailscaleService;
            this.selectedDeviceStore = selectedDeviceStore;
            RefreshCommand = new RelayCommand(async _ => await LoadDevicesAsync(), _ => !IsBusy);
            ConnectCommand = new RelayCommand(async _ => await ConnectAsync(), _ => !IsBusy && SelectedDevice != null);
        }

        public async Task InitializeAsync(bool autoConnectToSavedDevice)
        {
            await LoadDevicesAsync();
            if (!autoConnectToSavedDevice)
                return;

            var savedId = await selectedDeviceStore.LoadSelectedDeviceIdAsync();
            if (!string.IsNullOrWhiteSpace(savedId))
            {
                var device = Devices.FirstOrDefault(d => string.Equals(d.Id, savedId, StringComparison.OrdinalIgnoreCase));
                if (device != null)
                {
                    SelectedDevice = device;
                    await ConnectAsync();
                }
            }
        }

        private async Task LoadDevicesAsync()
        {
            IsBusy = true;
            StatusMessage = "Loading devices...";
            Devices.Clear();
            try
            {
                var devices = await tailscaleService.GetOnlineDevicesAsync();
                foreach (var device in devices)
                    Devices.Add(device);

                if (Devices.Count == 0)
                    StatusMessage = "No Tailscale devices found.";
                else
                    StatusMessage = string.Empty;
            }
            catch
            {
                StatusMessage = "Failed to load devices.";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ConnectAsync()
        {
            if (SelectedDevice == null)
                return;

            IsBusy = true;
            StatusMessage = "Connecting...";
            try
            {
                await selectedDeviceStore.SaveSelectedDeviceIdAsync(SelectedDevice.Id);
                var handler = Connected;
                if (handler != null)
                    handler(SelectedDevice);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void RaiseCommandCanExecuteChanged()
        {
            if (RefreshCommand is RelayCommand refreshRelay)
                refreshRelay.RaiseCanExecuteChanged();
            if (ConnectCommand is RelayCommand connectRelay)
                connectRelay.RaiseCanExecuteChanged();
        }
    }
}
