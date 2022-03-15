using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace udp_mux
{
    internal class ConfigFile
    {
        List<ConfigAddressTuple> Inputs { get; set; }
        List<ConfigAddressTuple> Outputs { get; set; }
        bool Autostart { get; set; }
    }

    internal class ConfigAddressTuple
    {
        public string Address { get; set; }
        public int Port { get; set; }
    }
}
