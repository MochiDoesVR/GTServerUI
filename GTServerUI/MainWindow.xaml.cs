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
using Windows.UI.Core;
using CoreOSC;
using CoreOSC.IO;
using System;
using System.Net.Sockets;
using System.Threading.Tasks;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GTServerUI
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public List<(string, Grid)> Grids;

        private bool WindowStarted;

        int DefaultOSCSendPort = 9000;
        string DefaultOSCAddress = "127.0.0.1";
        string DefaultOSCBatteryParameterRemap = "/parameters/HRMBattery";
        string DefaultOSCHeartrateParameterRemap = "/parameters/Heartrate";

        int OSCSendPort = 9000;
        string OSCAddress = "127.0.0.1";
        string OSCBatteryParameterRemap = "/parameters/HRMBattery";
        string OSCHeartrateParameterRemap = "/parameters/Heartrate";

        public MainWindow()
        {
            this.InitializeComponent();

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowid = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appw = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowid);

            appw.Resize(new Windows.Graphics.SizeInt32(700, 600));
            appw.TitleBar.ExtendsContentIntoTitleBar = true;
            appw.TitleBar.BackgroundColor = Microsoft.UI.Colors.Transparent;
            appw.TitleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
            appw.TitleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;

            Dashboard.SelectionChanged += Dashboard_SelectionChanged;

            Activated += delegate
            {
                if ((App.Current as App)?.currentWindow != null && !WindowStarted)
                {
                    Grids = new List<(string, Grid)>();
                    BluetoothDeviceManager.Init();
                    WindowStarted = true;
                }
            };

            BatteryOverrideInputBox.TextChanged += delegate
            {
                var input = BatteryOverrideInputBox;
                if (input.Text.Length > 0)
                {
                    if (input.Text[0] != '/')
                    {
                        input.Text = "";
                        OSCBatteryParameterRemap = DefaultOSCBatteryParameterRemap;
                    }
                    else
                    {
                        input.Text = System.Text.RegularExpressions.Regex.Replace(input.Text, @"[^\u0000-\u007F]+", string.Empty);
                        OSCBatteryParameterRemap = input.Text;
                    }
                }
                else
                {
                    OSCBatteryParameterRemap = BatteryOverrideInputBox.PlaceholderText;
                }
            };

            HeartrateOverrideInputBox.TextChanged += delegate
            {
                var input = HeartrateOverrideInputBox;
                if (input.Text.Length > 0)
                {
                    if (input.Text.Length > 0 && input.Text[0] != '/')
                    {
                        input.Text = "";
                        OSCHeartrateParameterRemap = DefaultOSCHeartrateParameterRemap;
                    }
                    else
                    {
                        input.Text = System.Text.RegularExpressions.Regex.Replace(input.Text, @"[^\u0000-\u007F]+", string.Empty);
                        OSCHeartrateParameterRemap = input.Text;
                    }
                }
                else
                {
                    OSCHeartrateParameterRemap = HeartrateOverrideInputBox.PlaceholderText;
                }
            };

            AddressInputBox.TextChanged += delegate
            {
                var unparsedAddress = System.Text.RegularExpressions.Regex.Replace(AddressInputBox.Text, @"[^\u0000-\u007F]+", string.Empty);

                if (ValidateIPv4(unparsedAddress))
                {
                    System.Diagnostics.Debug.WriteLine("Parsed IP!");
                    OSCAddress = unparsedAddress;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Failed to parse IP!");
                    OSCAddress = DefaultOSCAddress;
                }
            };

            SendPortInputBox.ValueChanged += delegate
            {
                OSCSendPort = (int)SendPortInputBox.Value;

                if (OSCSendPort < SendPortInputBox.Minimum || SendPortInputBox.Value < SendPortInputBox.Minimum)
                {
                    OSCSendPort = DefaultOSCSendPort;
                }
                else
                {
                    OSCSendPort = (int)SendPortInputBox.Value;
                }
            };
        }

        public void CreateDeviceListItem(string name, string mac)
        {
            // Create all the needed user interface elements
            Grid backgroundGrid = new Grid()
            {
                Background = (AcrylicBrush)Application.Current.Resources["AcrylicInAppFillColorDefaultBrush"],
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                CornerRadius = new CornerRadius(5),
                Width = 400,
                Height = 100
            };
            Grids.Add(new (mac, backgroundGrid));

            Grid contentGrid = new Grid()
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(16,16,16,16)
            };

            Grid textGrid = new Grid()
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(16, 16, 16, 16)
            };

            TextBlock deviceName = new TextBlock() { Style = (Style)Application.Current.Resources["BaseTextBlockStyle"], Text = name };
            TextBlock deviceMac = new TextBlock() { Style = (Style)Application.Current.Resources["BodyTextBlockStyle"], Text = mac[(mac.Length-17)..mac.Length] };
            Button connectButton = new Button() { Content = "Connect", HorizontalAlignment = HorizontalAlignment.Right, IsHitTestVisible = true };
            Button disconnectButton = new Button() { Content = "Disconnect", HorizontalAlignment = HorizontalAlignment.Right, Visibility = Visibility.Collapsed, IsHitTestVisible = true };

            // Parent all the objects
            textGrid.Children.Add(deviceName);
            textGrid.Children.Add(deviceMac);
            
            contentGrid.Children.Add(textGrid);
            contentGrid.Children.Add(connectButton);
            contentGrid.Children.Add(disconnectButton);

            backgroundGrid.Children.Add(contentGrid);

            deviceList.Children.Add(backgroundGrid);

            // Create row definitions for everything
            contentGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
            textGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
            textGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
            deviceList.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });

            // Properly assign rows to everything
            Grid.SetRow(deviceName, 0);
            Grid.SetRow(deviceMac, 1);
            Grid.SetRow(backgroundGrid, deviceList.RowDefinitions.Count-1);

            // Set button action
            connectButton.Click += (o,e) =>
            {
                BluetoothDeviceManager.Connect(mac);

                BluetoothDeviceManager.OnDeviceConnected += delegate
                {
                    BluetoothDeviceManager.Current.SendCommand(BluetoothDeviceManager.Current.CommandOpenHRM);

                    BluetoothDeviceManager.Current.OnDataRecieved += (d) =>
                    {
                        DataReceived(d);
                    };

                    disconnectButton.Click += (o, e) =>
                    {
                        pollCancelTokenSource.Cancel();
                        pollCancelTokenSource.Dispose();

                        BluetoothDeviceManager.Disconnect();

                        DeviceDisconnected();

                        for (int i = 0; i < Grids.Count; i++)
                        {
                            deviceList.Children.Remove(Grids[i].Item2);
                            Grids.Remove(Grids[i]);
                        }
                    };

                    ConnectButton_Clicked(o, e, disconnectButton);
                };

                for (int i = 0; i < Grids.Count; i++)
                {
                    if (Grids[i].Item1 != mac)
                    {
                        deviceList.Children.Remove(Grids[i].Item2);
                        Grids.Remove(Grids[i]);
                    }
                }
            };
        }

        private void ConnectButton_Clicked(object sender, RoutedEventArgs e, Button disconnectButton)
        {            
            DeviceConnected();

            var connectButton = (Button)e.OriginalSource;

            connectButton.Visibility = Visibility.Collapsed;
            disconnectButton.Visibility = Visibility.Visible;
        }

        private void DataReceived(byte[] data)
        {
            byte[] responseBatteryStatus = new byte[] { 0xAB, 0x00, 0x05, 0xFF, 0x91, 0x80 };
            byte[] responseHeartrate = new byte[] { 0xAB, 0x00, 0x04, 0xFF, 0x84, 0x80 };

            // Jump to UI thread, or WinUI will be a little bitch and throw an exception.
            (App.Current as App)?.currentWindow.DispatcherQueue.TryEnqueue(() =>
            {
                if (StringFromByteArray(data).StartsWith(StringFromByteArray(responseHeartrate)))
                {
                    var heartrate = data[data.Length - 1];

                    if (heartrate == 0)
                    {
                        HeartrateIcon.Glyph = "\xEB51";
                        HeartrateTicker.Text = "---";
                    }
                    else
                    {
                        HeartrateIcon.Glyph = "\xEB52";
                        HeartrateTicker.Text = heartrate.ToString();
                    }


                    using (
                    
                    var udpClient = new UdpClient(OSCAddress, OSCSendPort))
                    {
                        var message = new OscMessage(new Address(OSCHeartrateParameterRemap), new object[]
                        {
                            (int)heartrate
                        });

                        udpClient.SendMessageAsync(message);
                    }
                }

                if (StringFromByteArray(data).StartsWith(StringFromByteArray(responseBatteryStatus)))
                {
                    bool charging = (data[data.Length - 2] == 0x03 || data[data.Length - 2] == 0x01);
                    int level = data[data.Length - 1];

                    BatteryTicker.Text = level.ToString();
                    BatteryIcon.Glyph = GetBatteryIcon(charging, level);

                    using (var udpClient = new UdpClient(OSCAddress, OSCSendPort))
                    {
                        var message = new OscMessage(new Address(OSCBatteryParameterRemap), new object[]
                        {
                            (int)level
                        });

                        udpClient.SendMessageAsync(message);
                    }
                }
            });
        }

        System.Threading.CancellationTokenSource pollCancelTokenSource;
        public void DeviceConnected()
        {
            // Jump to UI thread, or WinUI will be a little bitch and throw an exception.
            (App.Current as App)?.currentWindow.DispatcherQueue.TryEnqueue(() =>
            {
                OscStatusLabel.Text = "OSC Status: Sending";
                WatchStatusLabel.Text = "Watch Status: Connected";
                ConnectionIndefiniteIndicator.Visibility = Visibility.Collapsed;
            });

            pollCancelTokenSource = new System.Threading.CancellationTokenSource();
            var poll = PollBatteryInterval(new TimeSpan(0, 0, 5), pollCancelTokenSource.Token);
        }

        public async Task PollBatteryInterval(TimeSpan interval, System.Threading.CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                BluetoothDeviceManager.Current.SendCommand(BluetoothDeviceManager.Current.CommandGetBatteryStatus);
                await Task.Delay(interval, cancellationToken);
            }
        }

        public void DeviceDisconnected()
        {
            // Jump to UI thread, or WinUI will be a little bitch and throw an exception.
            (App.Current as App)?.currentWindow.DispatcherQueue.TryEnqueue(() =>
            {
                OscStatusLabel.Text = "OSC Status: Idle";
                WatchStatusLabel.Text = "Watch Status: Disconnected";
                ConnectionIndefiniteIndicator.Visibility = Visibility.Visible;

                HeartrateIcon.Glyph = "\xEB51";
                BatteryIcon.Glyph = "\xEBA0";
                
                HeartrateTicker.Text = "---";
                BatteryTicker.Text = "---";
            });
        }

        public string StringFromByteArray(byte[] bytes)
        {
            return BitConverter.ToString(bytes);
        }


        private void Dashboard_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            switch(args.SelectedItemContainer.Tag.ToString())
            {
                case "tabDashboard":
                    paneDashboard.Visibility = Visibility.Visible;
                    paneDevice.Visibility = Visibility.Collapsed;
                    paneOsc.Visibility = Visibility.Collapsed;
                    paneAbout.Visibility = Visibility.Collapsed; 
                    break;
                case "tabDevice":
                    paneDashboard.Visibility = Visibility.Collapsed;
                    paneDevice.Visibility = Visibility.Visible;
                    paneOsc.Visibility = Visibility.Collapsed;
                    paneAbout.Visibility = Visibility.Collapsed; 
                    break;
                case "tabOsc":
                    paneDashboard.Visibility = Visibility.Collapsed;
                    paneDevice.Visibility = Visibility.Collapsed;
                    paneOsc.Visibility = Visibility.Visible;
                    paneAbout.Visibility = Visibility.Collapsed;
                    break;
                case "tabAbout":
                    paneDashboard.Visibility = Visibility.Collapsed;
                    paneDevice.Visibility = Visibility.Collapsed;
                    paneOsc.Visibility = Visibility.Collapsed;
                    paneAbout.Visibility = Visibility.Visible;
                    break;
            }
        }

        private string GetBatteryIcon(bool charging, int level)
        {
            if (charging)
            {
                switch (level)
                {
                    case 0:
                        return "\xEBAB";
                    case 20:
                        return "\xEBAD";
                    case 40:
                        return "\xEBAF";
                    case 60:
                        return "\xEBB1";
                    case 80:
                        return "\xEBB3";
                    case 100:
                        return "\xEBB5";
                }
            }
            else
            {
                switch (level)
                {
                    case 0:
                        return "\xEBA0";
                    case 20:
                        return "\xEBA2";
                    case 40:
                        return "\xEBA4";
                    case 60:
                        return "\xEBA6";
                    case 80:
                        return "\xEBA8";
                    case 100:
                        return "\xEBAA";
                }
            }
            return "\xEC02;";
        }

        public bool ValidateIPv4(string ipString)
        {
            if (String.IsNullOrWhiteSpace(ipString))
            {
                return false;
            }

            string[] splitValues = ipString.Split('.');
            if (splitValues.Length != 4)
            {
                return false;
            }

            byte tempForParsing;

            return splitValues.All(r => byte.TryParse(r, out tempForParsing));
        }
    }
}
