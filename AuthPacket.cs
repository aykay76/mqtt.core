using System.Collections.Generic;

namespace mqtt
{
    public class AuthPacket : Packet
    {
        public byte ReasonCode { get; set; }

        public string AuthMethod { get; set; }
        public byte[] AuthData { get; set; }
        public string ReasonString { get; set; }
        public List<Pair<string, string>> UserProperties { get; set; }

        public AuthPacket()
        {
            UserProperties = new List<Pair<string, string>>();
        }
    }
}