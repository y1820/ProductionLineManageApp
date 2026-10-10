using ProductionLineManage.Core.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProductionLineManage.Line.RLD19145.Profile
{
    public class Rld19145RequestCodes : IRequestCodes
    {
        public int Handshake => (int)PLCRequestCode.Handshake;
        public int FlowCodeVerify => (int)PLCRequestCode.FlowCodeVerify;
        public int MaterialVerify => (int)PLCRequestCode.MaterialVerify;
    }
}