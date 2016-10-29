using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shadowsocks.Model
{
    class ServerEx :Server
    {
        public override string ToString()
        {
            return new StringBuilder().Append("Server:\t").Append(this.server).Append("\t")
                 .Append("Port:\t").Append(this.server_port).Append("\t")
                 .Append("Pwd:\t").Append(this.password).Append("\t")
                 .Append("Method:\t").Append(this.method)
                 .ToString();
        }
    }
}
