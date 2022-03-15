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
    internal class IngressSocketThread
    {
        private BlockingCollection<Packet> PacketQueue;
        private Socket Socket;
        private Thread? RunningThread = null;

        public IngressSocketThread(Socket socket, BlockingCollection<Packet> outputQueue)
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
            this.Socket.Dispose();
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
            this.RunningThread.Name = "IngressSocketThread";
            this.RunningThread.Start();
        }

        public void Run()
        {
            // use a ringbuffer of 10 packets
            byte[][] buffer = new byte[10][];
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = new byte[4096];
            }

            int select_buffer = 0;
            try
            {
                while (true)
                {
                    IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                    EndPoint senderRemote = (EndPoint)sender;
                    int bytes_received = this.Socket.ReceiveFrom(buffer[select_buffer], ref senderRemote);

                    Debug.WriteLine("Received " + bytes_received + " bytes on " + this.Socket.ToString());

                    PacketQueue.Add(new Packet { Data = buffer[select_buffer], Size = bytes_received }); // send to the other threads

                    select_buffer = (select_buffer + 1) % 10;
                }
            }
            catch(SocketException ex)
            {
                if (ex.SocketErrorCode == SocketError.Interrupted)
                {
                    // just end peacefully
                    Debug.WriteLine("ingress exited");
                }
                else
                {
                    throw ex; // rethrow
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ingress encoutered an exception: " + ex.ToString() + "(" + ex.Message + ")");
            }
        }
    }
}
