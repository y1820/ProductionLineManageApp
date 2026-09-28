using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ProductionLineManage.Core.Abstractions;

namespace ProductionLineManage.Line.RLD19145.Profile
{
    public sealed class Rld19145LineProfile : ILineProfile
    {
        public string LineKey => "RLD19145";

        public string DisplayName => "RLD19145 协议";
    }
}
