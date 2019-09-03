using System;
using System.Net;

namespace mqtt
{
    public class ServerSettings
    {
        // TODO: make this serialisable (Newtonsoft.Json probably)
        public IPAddress ListenAddress { get; set; }
        public int Port { get; set; }
        public UInt32 SessionExpiry { get; set; }
        public UInt16 ReceiveLimit { get; set; }
        public byte QoS { get; set; }
        public byte RetainSupport { get; set; }
        public UInt32 MaxPacketSize { get; set; }
        public UInt16 TopicAliasMax { get; set; }
        public UInt16 ServerKeepAlive { get; set; }
        public string ServerRef { get; set; }

        public ServerSettings()
        {
            ListenAddress = IPAddress.Any;
            Port = 1883;
            // set some defaults
            SessionExpiry = 0;
            ReceiveLimit = 65535;
            QoS = 1;
            RetainSupport = 0;
            MaxPacketSize = 1048576;
            TopicAliasMax = 16;
            ServerKeepAlive = 180;
            ServerRef = "s10247";
        }
    }
}
