using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace udp_mux
{
    internal class IngressThread
    {
        private List<Socket> sockets = new List<Socket>();

        public IngressThread(List<AddressTuple> listenAddresses)
        {
            //Dns.GetHostEntry()
        }
    }
}
