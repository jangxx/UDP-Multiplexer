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
    internal class IngressSocketThread
    {
        private BlockingCollection<byte[]> PacketQueue;
        private Socket Socket;
        private Thread? RunningThread = null;

        public IngressSocketThread(Socket socket, BlockingCollection<byte[]> outputQueue)
        {
            this.Socket = socket;
            this.PacketQueue = outputQueue;
        }

        public void Stop()
        {
            if (this.RunningThread == null)
            {
                return;
            }

            this.Socket.Close();
            this.RunningThread.Join();
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

        public void Run()
        {
            byte[] buffer = new byte[4096];
            while (true)
            {
                IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                EndPoint senderRemote = (EndPoint)sender;
                this.Socket.ReceiveFrom(buffer, ref senderRemote);

                PacketQueue.Add(buffer); // send to the other threads
            }
        }
    }
}
