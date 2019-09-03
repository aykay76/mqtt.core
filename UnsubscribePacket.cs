using System.Collections.Generic;

namespace mqtt
{
    public class UnsubscribePacket : Packet
    {
        // TODO: need to be stored in session state
        public List<Pair<string, string>> UserProperties { get; set; }
        public List<string> Topics { get; set; }

        public UnsubscribePacket()
        {
            UserProperties = new List<Pair<string, string>>();
            Topics = new List<string>();
        }
    }
}