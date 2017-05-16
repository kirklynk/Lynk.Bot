using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Networking;

namespace Robot.V1
{
   public sealed class RemoteConnection
    {
        public HostName RemoteAddress { get; set; }
        public string RemotePort { get; set; }
    }
}
