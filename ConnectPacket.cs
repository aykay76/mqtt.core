using System;
using System.Collections.Generic;

namespace mqtt
{
    public class ConnectPacket : Packet 
    {
        public bool UsernameFlag { get; set; }
        public bool PasswordFlag { get; set; }
        public bool WillRetainFlag { get; set; }
        public int WillQoS { get; set; }
        public bool WillFlag { get; set; }
        public bool CleanStart { get; set; }

        public ushort KeepAlive { get; set; }

        public string ClientID { get; set; }

        public UInt32 SessionExpiry { get; set; }
        public UInt16 ReceiveMaximum { get; set; }
        public UInt32 MaximumPacketSize { get; set; }
        public UInt16 TopicAliasMaximum { get; set; }
        public byte RequestResponse { get; set; }
        public byte RequestProblem { get; set; }
        public List<Pair<string, string>> UserProperties { get; set; }

        public UInt32 WillDelayInterval { get; set; }
        public byte Format { get; set; }
        public UInt32 MessageExpiry { get; set; }
        public string ContentType = string.Empty;
        public string ResponseTopic = string.Empty;
        public byte[] Correlation = null;
        public List<Pair<string, string>> WillProperties { get; set; }
        public string WillTopic { get; set; }

        public string Username { get; set; }
        public byte[] Password { get; set; }

        public ConnectPacket()
        {
            SessionExpiry = 0;
            ReceiveMaximum = 65535;
            TopicAliasMaximum = 0;
            UserProperties = new List<Pair<string, string>>();

            WillDelayInterval = 0;
            WillProperties = new List<Pair<string, string>>();
        }
    }
}