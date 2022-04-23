using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using Windows.Devices.Enumeration;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;
using Windows.UI.Core;
namespace GTServerUI
{
    internal class BluetoothDeviceManager
    {
        public static Action OnDeviceConnected;

        public static Action OnDeviceDisconnected;

        static string[] requestedProperties = { "System.Devices.Aep.DeviceAddress", "System.Devices.Aep.IsConnected" };

        public static GTServerDevice Current;
        private static BluetoothLEDevice CurrentDevice;

        private static DeviceWatcher watcher;

        public static void Init()
        {
            Debug.WriteLine("Hello from the GATT backend!");
            watcher = DeviceInformation.CreateWatcher(
                    BluetoothLEDevice.GetDeviceSelectorFromPairingState(false),
                    requestedProperties,
                    DeviceInformationKind.AssociationEndpoint);
            watcher.Added += DeviceFound;

            Debug.WriteLine("Starting BLE Watcher");
            watcher.Start();
        }

        private static void DeviceFound(DeviceWatcher sender, DeviceInformation args)
        {
            Debug.WriteLine($"Found {args.Name} ({args.Id})!");

            if (args.Name == "GT101")
            {
                var window = (App.Current as App)?.currentWindow;

                window.DispatcherQueue.TryEnqueue(() =>
                {
                    window.CreateDeviceListItem(args.Name, args.Id);
                });
            }
        }

        public static async void Connect(string deviceId)
        {
            // Connect to watch and stop watcher service
            var watch = await BluetoothLEDevice.FromIdAsync(deviceId);
            
            // Create New Device
            GTServerDevice device = new GTServerDevice();
            device.Name = watch.DeviceInformation.Name;
            device.MacAddress = watch.DeviceInformation.Id.ToString();
            Debug.WriteLine(device.MacAddress);

            // Get Device Info
            GattDeviceServicesResult GetGattServices = await watch.GetGattServicesAsync();

            if (GetGattServices.Status == GattCommunicationStatus.Success)
            {
                foreach (var service in GetGattServices.Services)
                {
                    GattCharacteristicsResult GetGattCharacteristics = await service.GetCharacteristicsAsync();

                    if (GetGattCharacteristics.Status == GattCommunicationStatus.Success)
                    {
                        var characteristics = GetGattCharacteristics.Characteristics;
                        foreach (var characteristic in characteristics)
                        {
                            if (characteristic.Uuid.ToString().ToLower() == device.UART_TX_ID.ToLower())
                            {
                                device.UART_TX_CHARACTERISTIC = characteristic;
                            }

                            if (characteristic.Uuid.ToString().ToLower() == device.UART_RX_ID.ToLower())
                            {
                                device.UART_RX_CHARACTERISTIC = characteristic;
                            }
                        }
                    }
                }

                Current = device;
                CurrentDevice = watch;

                if (OnDeviceConnected != null)
                {
                    OnDeviceConnected.Invoke();
                }

                GattCommunicationStatus status = await device.UART_TX_CHARACTERISTIC.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);
                if (status == GattCommunicationStatus.Success)
                {
                    device.UART_TX_CHARACTERISTIC.ValueChanged += ValueChanged;
                }

                watcher.Stop();
            }
        }

        private static void ValueChanged(GattCharacteristic o, GattValueChangedEventArgs e)
        {
            var reader = DataReader.FromBuffer(e.CharacteristicValue);
            byte[] bytes = new byte[e.CharacteristicValue.Length];

            reader.ReadBytes(bytes);

            Current.OnDataRecieved.Invoke(bytes);
            reader.Dispose();
        }

        public static async void Disconnect()
        {
            GattDeviceServicesResult GetGattServices = await CurrentDevice.GetGattServicesAsync();

            if (GetGattServices.Status == GattCommunicationStatus.Success)
            {
                foreach (var service in GetGattServices.Services)
                {
                    service.Dispose();
                }
            }

            Current.UART_TX_CHARACTERISTIC.ValueChanged -= ValueChanged;
            CurrentDevice.Dispose();
            CurrentDevice = null;
            Current = null;
            GC.Collect();

            if (OnDeviceDisconnected != null)
            {
                OnDeviceDisconnected.Invoke();
            }

            watcher.Start();
        }
    }

    public class GTServerDevice
    {
        public byte[] CommandOpenHRM = new byte[] { 0xAB, 0x00, 0x04, 0xFF, 0x84, 0x80, 0x01 };
        public byte[] CommandShakeWatch = new byte[] { 0xAB, 0x00, 0x03, 0xFF, 0x71, 0x80 };
        public byte[] CommandGetBatteryStatus = new byte[] { 0xAB, 0x00, 0x04, 0xFF, 0x91, 0x80, 0x01 };

        public string MacAddress;
        public string Name;

        public string UART_RX_ID = "6E400002-B5A3-F393-E0A9-E50E24DCCA9E";
        public string UART_TX_ID = "6E400003-B5A3-F393-E0A9-E50E24DCCA9E";

        public GattCharacteristic UART_RX_CHARACTERISTIC = null;
        public GattCharacteristic UART_TX_CHARACTERISTIC = null;

        public Action<byte[]> OnDataRecieved;   

        public async void SendCommand(byte[] command)
        {
            var writer = new DataWriter();

            writer.WriteBytes(command);

            try
            {
                GattCommunicationStatus writeResult = await UART_RX_CHARACTERISTIC.WriteValueAsync(writer.DetachBuffer());
            }
            catch(Exception) 
            {

            }


            writer.Dispose();
        }
    }
}