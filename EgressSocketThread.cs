using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace udp_mux
{
    internal class EgressSocketThread
    {
        private BlockingCollection<byte[]> PacketQueue = new BlockingCollection<byte[]>();
        private Socket Socket;
        private IPEndPoint Endpoint;
        private CancellationTokenSource CancelTokenSource = new CancellationTokenSource();
        private Thread? RunningThread = null;

        public EgressSocketThread(Socket socket, IPEndPoint endpoint)
        {
            this.Socket = socket;
            this.Endpoint = endpoint;

        }
        public void Enqueue(byte[] buffer)
        {
            PacketQueue.Add(buffer);
        }

        public void Start()
        {
            if (this.RunningThread != null)
            {
                return;
            }

            this.RunningThread = new Thread(new ThreadStart(Run));
            this.RunningThread.IsBackground = true;
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
            byte[] buffer;
            while (PacketQueue.TryTake(out buffer, -1, CancelTokenSource.Token)) {
                this.Socket.SendTo(buffer, this.Endpoint);
            }
        }
    }
}
