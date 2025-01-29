using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace RouteForwarder
{
    class IPForwardRow

    {

        /// <summary>

        /// destination IP address.

        /// </summary>

        public IPAddress Dest { get; set; }

        /// <summary>

        /// Subnet mask

        /// </summary>

        public IPAddress Mask { get; set; }

        /// <summary>

        /// conditions for multi-path route. Unused, specify 0.

        /// </summary>

        public uint Policy { get; set; }

        /// <summary>

        /// IP address of the next hop. Own address?

        /// </summary>

        public IPAddress NextHop { get; set; }

        /// <summary>

        /// index of interface

        /// </summary>

        public uint IfIndex { get; set; }

        /// <summary>

        /// route type

        /// </summary>

        public RouteTableManager.MIB_IPFORWARD_TYPE Type { get; set; }

        /// <summary>

        /// routing protocol.

        /// </summary>

        public RouteTableManager.MIB_IPPROTO Proto { get; set; }

        /// <summary>

        /// age of route.

        /// </summary>

        public uint Age { get; set; }

        /// <summary>

        /// autonomous system number. 0 if not relevant

        /// </summary>

        public uint NextHopAS { get; set; }

        /// <summary>

        /// -1 if not used (goes for all metrics)

        /// </summary>

        public int Metric { get; set; }



        public IPForwardRow()

        {



        }



        public IPForwardRow(RouteTableManager.MIB_IPFORWARDROW baseStruct)

        {

            Dest = RouteTableManager.UintToIp(baseStruct.dwForwardDest);

            Mask = RouteTableManager.UintToIp(baseStruct.dwForwardMask);

            Policy = baseStruct.dwForwardPolicy;

            NextHop = RouteTableManager.UintToIp(baseStruct.dwForwardNextHop);

            IfIndex = baseStruct.dwForwardIfIndex;

            Type = baseStruct.dwForwardType;

            Proto = baseStruct.dwForwardProto;

            Age = baseStruct.dwForwardAge;

            NextHopAS = baseStruct.dwForwardNextHopAS;

            Metric = baseStruct.dwForwardMetric1;

        }



        public RouteTableManager.MIB_IPFORWARDROW GetBaseStruct()

        {

            return new RouteTableManager.MIB_IPFORWARDROW()

            {

                dwForwardDest = RouteTableManager.IpToUint(Dest),

                dwForwardMask = RouteTableManager.IpToUint(Mask),

                dwForwardPolicy = Policy,

                dwForwardNextHop = RouteTableManager.IpToUint(NextHop),

                dwForwardIfIndex = IfIndex,

                dwForwardType = Type,

                dwForwardProto = Proto,

                dwForwardAge = Age,

                dwForwardNextHopAS = NextHopAS,

                dwForwardMetric1 = Metric,

                dwForwardMetric2 = -1,

                dwForwardMetric3 = -1,

                dwForwardMetric4 = -1,

                dwForwardMetric5 = -1

            };

        }

    }
}