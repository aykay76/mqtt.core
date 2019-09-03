using System;
using System.Collections.Generic;

namespace mqtt
{
    // if QoS is 0, send this packet and remove
    // if QoS is 1, send this packet and wait for PUBACK
    // if QoS is 2, send this packet, wait for PUBACK, send PUBREL then wait for PUBCOMP
    public class PublishPacket : Packet
    {
        public bool Duplicate { get; set; }
        public int QoS { get; set; }
        public bool Retain { get; set; }
        public string TopicName { get; set; }

        public byte Format { get; set; }
        public uint MessageExpiry { get; set; }
        public UInt16 TopicAlias { get; set; }
        public string ResponseTopic { get; set; }
        public byte[] Correlation { get; set; }
        public List<Pair<string, string>> UserProperties { get; set; }
        public int SubscriptionID { get; set; }
        public string ContentType { get; set; }

        public byte[] ApplicationMessage { get; set; }

        public bool Acknowledged { get; set; }
        public bool Received { get; set; }

        // TODO: add v5 properties

        public PublishPacket()
        {
            UserProperties = new List<Pair<string, string>>();
            Acknowledged = false;
            Received = false;
        }
    }
}