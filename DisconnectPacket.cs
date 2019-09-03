using System;
using System.Collections.Generic;

namespace mqtt
{
    public class DisconnectPacket : Packet
    {
        public byte ReasonCode { get; set; }

        public UInt32 SessionExpiry { get; set; }
        public string ReasonString { get; set; }
        public List<Pair<string, string>> UserProperties { get; set; }
        public string ServerReference { get; set; }

        public DisconnectPacket()
        {
            UserProperties = new List<Pair<string, string>>();
        }
    }
}