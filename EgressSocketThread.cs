using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace udp_mux
{
    internal class EgressSocketThread
    {
        private BlockingCollection<Packet> PacketQueue = new BlockingCollection<Packet>();
        private Socket Socket;
        private IPEndPoint Endpoint;
        private CancellationTokenSource CancelTokenSource = new CancellationTokenSource();
        private Thread? RunningThread = null;

        public EgressSocketThread(Socket socket, IPEndPoint endpoint)
        {
            this.Socket = socket;
            this.Endpoint = endpoint;

        }
        public void Enqueue(Packet packet)
        {
            PacketQueue.Add(packet);
        }

        public void Start()
        {
            if (this.RunningThread != null)
            {
                return;
            }

            this.RunningThread = new Thread(new ThreadStart(Run));
            this.RunningThread.IsBackground = true;
            this.RunningThread.Name = "EgressSocketThread";
            this.RunningThread.Start();
        }

        public void Stop()
        {
            if (this.RunningThread == null)
            {
                return;
            }

            this.CancelTokenSource.Cancel();
            this.RunningThread.Join();
        }

        public void Run()
        {
            Packet packet;
            try
            {
                while (PacketQueue.TryTake(out packet, -1, CancelTokenSource.Token))
                {
                    this.Socket.SendTo(packet.Data, packet.Size, SocketFlags.None, this.Endpoint);
                }
            }
            catch (OperationCanceledException)
            {
                // just let the function exit
                Debug.WriteLine("egress exited " + this.Socket.LocalEndPoint.ToString());
            }
            catch (Exception ex)
            {
                MessageBox.Show("Egress encoutered an exception: " + ex.ToString() + "(" + ex.Message + ")");
            }
        }
    }
}
