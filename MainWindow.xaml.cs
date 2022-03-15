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

        public MainWindow()
        {
            InitializeComponent();

            icInputAddresses.ItemsSource = InputAddresses;
            icOutputAddresses.ItemsSource = OutputAddresses;
            cbAutostart.IsChecked = SettingAutostart;
            
        }

        public void Run()
        {
            // create input and output threads
            List<IngressSocketThread> ingressSockets = new List<IngressSocketThread>();
            List<EgressSocketThread> egressSockets = new List<EgressSocketThread>();

            BlockingCollection<Packet> packetQueue = new BlockingCollection<Packet>();

            foreach (var address in InputAddresses)
            {
                var hostEntry = Dns.GetHostEntry(hostNameOrAddress: address.Address);

                foreach (IPAddress addr in hostEntry.AddressList)
                {
                    var endpoint = new IPEndPoint(addr, (int)address.Port);

                    Socket socket = new Socket(addr.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
                    socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    socket.Bind(endpoint);

                    ingressSockets.Add(new IngressSocketThread(socket, packetQueue));
                }
            }

            foreach (var address in OutputAddresses)
            {
                var hostEntry = Dns.GetHostEntry(hostNameOrAddress: address.Address);

                foreach(IPAddress addr in hostEntry.AddressList)
                {
                    var endpoint = new IPEndPoint(addr, (int)address.Port);

                    Socket socket = new Socket(addr.AddressFamily, SocketType.Dgram, ProtocolType.Udp);

                    egressSockets.Add(new EgressSocketThread(socket, endpoint));
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


        private void Btn_start(object sender, RoutedEventArgs e)
        {
            if (this.MainThread == null)
            {
                this.MainThreadCancellationSource = new CancellationTokenSource();

                this.MainThread = new Thread(new ThreadStart(Run));
                this.MainThread.IsBackground = true;
                this.MainThread.Name = "Main Thread";
                this.MainThread.Start();
                startButton.Content = "Stop";
            } 
            else
            {
                this.MainThreadCancellationSource.Cancel();
                this.MainThread.Join();
                this.MainThread = null;
                startButton.Content = "Start";
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
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "JSON file|*.json";
            if (saveFileDialog.ShowDialog() == true)
            {

            }
        }

        private void Btn_openConfig(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "JSON file|*.json";
            if (openFileDialog.ShowDialog() == true)
            {

            }
        }
    }
}
