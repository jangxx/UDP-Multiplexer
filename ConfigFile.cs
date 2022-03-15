using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace udp_mux
{
    internal class ConfigFile
    {
        public List<ConfigAddressTuple> Inputs { get; set; }
        public List<ConfigAddressTuple> Outputs { get; set; }
        public bool Autostart { get; set; }

        public ConfigFile()
        {
            Inputs = new List<ConfigAddressTuple>();
            Outputs = new List<ConfigAddressTuple>();
        }
    }

    internal class ConfigAddressTuple
    {
        public string Address { get; set; }
        public int Port { get; set; }
    }
}
