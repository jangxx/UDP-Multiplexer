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

namespace udp_mux
{
    public class AddressTuple
    {
        public string? Address { get; set; }
        public UInt16? Port { get; set; }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private ObservableCollection<AddressTuple> inputAddresses = new ObservableCollection<AddressTuple>() { new AddressTuple() };
        private ObservableCollection<AddressTuple> outputAddresses = new ObservableCollection<AddressTuple>() { new AddressTuple() };
        private CancellationTokenSource MainThreadCancellationSource = new CancellationTokenSource();
        private Thread? MainThread = null;

        public MainWindow()
        {
            InitializeComponent();

            icInputAddresses.ItemsSource = inputAddresses;
            icOutputAddresses.ItemsSource = outputAddresses;
        }

        public void Run()
        {
            // create input and output threads
            List<IngressSocketThread> ingressSockets = new List<IngressSocketThread>();
            List<EgressSocketThread> egressSockets = new List<EgressSocketThread>();

            BlockingCollection<byte[]> packetQueue = new BlockingCollection<byte[]>();

            foreach (var address in inputAddresses)
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

            foreach (var address in outputAddresses)
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
            byte[] packet;
            while (packetQueue.TryTake(out packet, -1, this.MainThreadCancellationSource.Token))
            {
                // send the packet to all egress threads
                foreach (var sock in egressSockets)
                {
                    sock.Enqueue(packet);
                }
            }
        }

        private void Btn_addInput(object sender, RoutedEventArgs e)
        {
            inputAddresses.Add(new AddressTuple());
        }

        private void Btn_removeInput(object sender, RoutedEventArgs e)
        {
            inputAddresses.Add(new AddressTuple());
        }

        private void Btn_addOutput(object sender, RoutedEventArgs e)
        {
            outputAddresses.Add(new AddressTuple());
        }

        private void Btn_removeOutput(object sender, RoutedEventArgs e)
        {
            inputAddresses.Add(new AddressTuple());
        }

        private void Btn_start(object sender, RoutedEventArgs e)
        {
            //MessageBox.Show("Inputs: " + String.Join(", ", inputAddresses.ToList().ConvertAll(new Converter<AddressTuple, String>(addr => addr.Address + ":" + addr.Port))));
            this.MainThread = new Thread(new ThreadStart(Run));
            this.MainThread.IsBackground = true;
            this.MainThread.Start();
        }

        private void Btn_saveConfig(object sender, RoutedEventArgs e)
        {
        }
    }
}
