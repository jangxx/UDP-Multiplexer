using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
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
using System.Diagnostics;
using Microsoft.Win32;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.IO;

namespace udp_mux
{
    public enum AddressInputDataType
    {
        INPUT, OUTPUT
    }

    public class AddressInputData : INotifyPropertyChanged
    { // https://stackoverflow.com/a/1316417/1342618
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private string address;
        public string Address
        {
            get { return address; }
            set { SetField(ref address, value); }
        }

        private UInt16 port;
        public UInt16 Port
        {
            get { return port; }
            set { SetField(ref port, value); }
        }

        private bool canRemove;
        public bool CanRemove
        {
            get { return canRemove; }
            set { SetField(ref canRemove, value); }
        }

        public AddressInputDataType Type;
        public IPHostEntry? HostEntry;
    }

    public class MainWindowProperties : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private bool allInputsEnabled;
        public bool AllInputsEnabled
        {
            get { return allInputsEnabled; }
            set { SetField(ref allInputsEnabled, value); }
        }

        private long packetsForwardedCount;
        public long PacketsForwardedCount
        {
            get { return packetsForwardedCount; }
            set { SetField(ref packetsForwardedCount, value); }
        }
    }

    public class Packet
    {
        public byte[] Data { get; set; }
        public int Size { get; set; }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private ObservableCollection<AddressInputData> InputAddresses = new ObservableCollection<AddressInputData>() { new AddressInputData { CanRemove = false, Type=AddressInputDataType.INPUT } };
        private ObservableCollection<AddressInputData> OutputAddresses = new ObservableCollection<AddressInputData>() { new AddressInputData { CanRemove = false, Type = AddressInputDataType.OUTPUT } };
        private bool SettingAutostart = false;
        private CancellationTokenSource MainThreadCancellationSource;
        private Thread? MainThread = null;
        public MainWindowProperties DisplayProperties { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = this;

            icInputAddresses.ItemsSource = InputAddresses;
            icOutputAddresses.ItemsSource = OutputAddresses;

            DisplayProperties = new MainWindowProperties();
            DisplayProperties.AllInputsEnabled = true;
            DisplayProperties.PacketsForwardedCount = 0L;

            UpdateSettingsControls();
        }

        public void ProcessStartupConfig()
        {
            if (SettingAutostart)
            {
                StartMain();
            }
        }

        private void UpdateSettingsControls()
        {
            cbAutostart.IsChecked = SettingAutostart;
        }

        private void UpdateSettingsFromControls()
        {
            SettingAutostart = cbAutostart.IsChecked == true;
        }

        public void Run()
        {
            // create input and output threads
            List<IngressSocketThread> ingressSockets = new List<IngressSocketThread>();
            List<EgressSocketThread> egressSockets = new List<EgressSocketThread>();

            BlockingCollection<Packet> packetQueue = new BlockingCollection<Packet>();

            // try resolving all addresses
            foreach (var address in InputAddresses.Concat(OutputAddresses))
            {
                try
                {
                    address.HostEntry = Dns.GetHostEntry(hostNameOrAddress: address.Address);
                }
                catch (SocketException ex)
                {
                    if (ex.SocketErrorCode == SocketError.HostNotFound)
                    {
                        MessageBox.Show("Could not resolve host '" + address.Address + "'", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    else
                    {
                        MessageBox.Show("Unexpected error occured while resolving address'" + address.Address + "': " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }

                    this.MainThread = null;
                    Application.Current.Dispatcher.Invoke(new Action(() => StopMain()));
                    return;
                }
            }

            foreach (var address in InputAddresses)
            {   
                foreach (IPAddress addr in address.HostEntry!.AddressList)
                {
                    var endpoint = new IPEndPoint(addr, (int)address.Port);

                    Socket socket = new Socket(addr.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
                    socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    socket.Bind(endpoint);

                    Debug.WriteLine("Started input thread on " + endpoint.ToString());
                    ingressSockets.Add(new IngressSocketThread(socket, packetQueue, this));
                }
            }

            foreach (var address in OutputAddresses)
            {
                foreach (IPAddress addr in address.HostEntry!.AddressList)
                {
                    var endpoint = new IPEndPoint(addr, (int)address.Port);

                    Socket socket = new Socket(addr.AddressFamily, SocketType.Dgram, ProtocolType.Udp);

                    Debug.WriteLine("Started output thread for " + endpoint.ToString());
                    egressSockets.Add(new EgressSocketThread(socket, endpoint, this));
                }
            }

            // start all of the threads
            foreach (var t in ingressSockets)
            {
                t.Start();
            }

            foreach (var t in egressSockets)
            {
                t.Start();
            }

            // start the distribution loop
            Packet packet;
            try
            {
                while (packetQueue.TryTake(out packet, -1, this.MainThreadCancellationSource.Token))
                {
                    // send the packet to all egress threads
                    foreach (var sock in egressSockets)
                    {
                        sock.Enqueue(packet);
                    }

                    Application.Current.Dispatcher.Invoke(new Action(() =>
                    {
                        DisplayProperties.PacketsForwardedCount += 1;
                    }));
                }
            } 
            catch(OperationCanceledException)
            {
                // quit the ingress and egress threads
                foreach (var t in ingressSockets)
                {
                    t.Stop();
                }

                foreach (var t in egressSockets)
                {
                    t.Stop();
                }
                Debug.WriteLine("main exited");
            }
        }

        public void StopMain()
        {
            if (this.MainThread != null)
            {
                this.MainThreadCancellationSource.Cancel();
                this.MainThread.Join();
                this.MainThread = null;
            }
            startButton.Content = "Start";
            DisplayProperties.AllInputsEnabled = true;
        }

        private void Btn_addInput(object sender, RoutedEventArgs e)
        {
            InputAddresses.Add(new AddressInputData { CanRemove = true, Type = AddressInputDataType.INPUT });
            foreach (var i in InputAddresses) {
                i.CanRemove = true; // if we have added another address, all of them become deletable
            }
        }

        private void Btn_addOutput(object sender, RoutedEventArgs e)
        {
            OutputAddresses.Add(new AddressInputData { CanRemove = true, Type = AddressInputDataType.OUTPUT });
            foreach (var i in OutputAddresses)
            {
                i.CanRemove = true; // if we have added another address, all of them become deletable
            }
        }

        private void StartMain()
        {
            DisplayProperties.AllInputsEnabled = false;
            DisplayProperties.PacketsForwardedCount = 0;

            // check addresses and ports
            foreach (var ia in InputAddresses)
            {
                if (ia.Address == null || ia.Port == 0)
                {
                    MessageBox.Show("One of the inputs is missing an address", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
            foreach (var ia in OutputAddresses)
            {
                if (ia.Address == null || ia.Port == 0)
                {
                    MessageBox.Show("One of the outputs is missing an address", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            this.MainThreadCancellationSource = new CancellationTokenSource();

            this.MainThread = new Thread(new ThreadStart(Run));
            this.MainThread.IsBackground = true;
            this.MainThread.Name = "Main Worker Thread";
            this.MainThread.Start();
            startButton.Content = "Stop";
        }

        private void Btn_start(object sender, RoutedEventArgs e)
        {
            if (this.MainThread == null)
            {
                StartMain();
            } 
            else
            {
                StopMain();
            }
        }

        private void Btn_removeAddress(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var addressData = (AddressInputData)button.DataContext;

            if (addressData.Type == AddressInputDataType.INPUT)
            {
                InputAddresses.Remove(addressData);
                if (InputAddresses.Count == 1)
                {
                    InputAddresses[0].CanRemove = false;
                }
            }
            else
            {
                OutputAddresses.Remove(addressData);
                if (OutputAddresses.Count == 1)
                {
                    OutputAddresses[0].CanRemove = false;
                }
            }
        }

        private void Btn_saveConfig(object sender, RoutedEventArgs e)
        {
            UpdateSettingsFromControls();

            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "JSON file|*.json";
            if (saveFileDialog.ShowDialog() == true)
            {
                var configFile = new ConfigFile();
                configFile.Inputs = InputAddresses
                    .ToList()
                    .ConvertAll<ConfigAddressTuple>(addr => new ConfigAddressTuple { Address = addr.Address, Port = addr.Port });
                configFile.Outputs = OutputAddresses
                    .ToList()
                    .ConvertAll<ConfigAddressTuple>(addr => new ConfigAddressTuple { Address = addr.Address, Port = addr.Port });
                configFile.Autostart = SettingAutostart;

                string jsonString = JsonSerializer.Serialize(configFile, new JsonSerializerOptions() { WriteIndented = true});
                File.WriteAllText(saveFileDialog.FileName, jsonString);
            }
        }

        public void LoadConfig(String path)
        {
            string jsonString = File.ReadAllText(path);

            if (jsonString == null) return;

            ConfigFile configFile;
            try
            {
                configFile = JsonSerializer.Deserialize<ConfigFile>(jsonString);
            }
            catch(JsonException ex)
            {
                MessageBox.Show("Could not parse config file: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (configFile == null) return;

            InputAddresses.Clear();
            foreach (var addr in configFile.Inputs)
            {
                InputAddresses.Add(new AddressInputData { CanRemove=configFile.Inputs.Count > 1, Address = addr.Address, Port = (UInt16)addr.Port, Type = AddressInputDataType.INPUT });
            }

            OutputAddresses.Clear();
            foreach (var addr in configFile.Outputs)
            {
                OutputAddresses.Add(new AddressInputData { CanRemove = configFile.Outputs.Count > 1, Address = addr.Address, Port = (UInt16)addr.Port, Type = AddressInputDataType.OUTPUT });
            }

            SettingAutostart = configFile.Autostart;

            UpdateSettingsControls();
        }

        private void Btn_openConfig(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "JSON file|*.json";
            if (openFileDialog.ShowDialog() == true)
            {
                LoadConfig(openFileDialog.FileName);
            }
        }
    }
}
