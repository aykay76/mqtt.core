using System;
using System.Collections.Generic;

namespace mqtt
{
    public class PubrelPacket : Packet
    {
        public string ReasonString { get; set; }
        public List<Pair<string, string>> UserProperties { get; set; }

        public PubrelPacket()
        {
            UserProperties = new List<Pair<string, string>>();
        }
    }
}