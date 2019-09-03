using System;
using System.Collections.Generic;

namespace mqtt
{
    public class SessionState
    {
        public DateTime StartTime { get; set; }

        public Dictionary<string, byte> Subscriptions { get; set; }

        public LinkedList<Packet> PendingPackets { get; set; }

        public int SendQuota { get; set; }

        public SessionState()
        {
            StartTime = DateTime.Now;
            Subscriptions = new Dictionary<string, byte>();
            PendingPackets = new LinkedList<Packet>();
        }
    }
}