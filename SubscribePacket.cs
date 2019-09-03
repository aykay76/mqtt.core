using System;
using System.Collections.Generic;

namespace mqtt
{
    public class SubscribePacket : Packet
    {
        public int SubscriptionID { get; set; }
        
        public List<Pair<string, string>> UserProperties { get; set; }

        public Dictionary<string, byte> Subscriptions { get; set; }

        // TODO: remember that the QoS for delivering a message can be different to the QoS for sending one
        // therefore the application message needs to be duplicated for each client it is to be forwarded to

        public SubscribePacket()
        {
            UserProperties = new List<Pair<string, string>>();
            Subscriptions = new Dictionary<string, byte>();
        }
    }
}