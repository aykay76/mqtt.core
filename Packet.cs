using System;

namespace mqtt
{
    public class Packet
    {
        public PacketType Type { get; set; }
        public UInt16 ID { get; set; }

        public byte[] Payload { get; set; }
    }
}