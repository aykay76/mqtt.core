using System;
using System.Collections.Generic;
using System.IO;

namespace mqtt
{
    public class MQTTClient
    {
        public string ID { get; set; }
        public SessionState Session { get; set; }
        public byte ProtocolVersion { get; set; }
        public DateTime LastSeen { get; set; }

        int pos = 0;
        byte fixedHeader = 0;
        int remainingLength = 0;
        NetworkChannel channel;

        public MQTTClient(NetworkChannel channel)
        {
            this.channel = channel;
        }

        public Packet GetPacket()
        {
            Packet packet = null;

            if (FullPacket())
            {
                LastSeen = DateTime.Now;
                packet = DecodePacket();
            }

            return packet;
        }

        private bool FullPacket()
        {
            if (channel.HasData())
            {
                if (channel.Buffer.Length < 2) return false;

                PacketType packetType = (PacketType)(channel.Buffer[0] >> 4);
                // TODO: process flags in the lower nibble
                Console.WriteLine($"Potential packet type {packetType}");

                // skip first byte (fixed header)
                int pos = 1;

                // check variable header ensuring remaining length is not more than buffer length
                int multiplier = 1;
                int value = 0;
                byte encodedByte = 0;

                do
                {
                    encodedByte = channel.Buffer[pos++];
                    if (pos > channel.Buffer.Length) return false;

                    value += (encodedByte & 0x7f) * multiplier;
                    if (multiplier > 128*128*128)
                    {
                        return false;
                    }
                    multiplier *= 128;
                } while ((encodedByte & 0x80) != 0);

                if (channel.Buffer.Length == value + pos)
                {
                    return true;
                }
            }

            return false;
        }

        public Packet DecodePacket()
        {
            Packet packet = null;

            pos = 0;

            fixedHeader = channel.Buffer[pos++];

            PacketType packetType = (PacketType)(fixedHeader >> 4);
            // TODO: process flags in the lower nibble

            remainingLength = DecodeVariableByteInteger(channel.Buffer, ref pos);

            switch (packetType)
            {
                // packets that a server could receive from a client
                case PacketType.CONNECT:
                    packet = DecodeConnect();
                    break;
                case PacketType.PUBLISH:
                    packet = DecodePublish();
                    break;
                case PacketType.PUBREL:
                    packet = DecodePubrel();
                    break;
                case PacketType.PINGREQ:
                    packet = DecodePingreq();
                    break;
                case PacketType.DISCONNECT:
                    packet = DecodeDisconnect();
                    break;
                case PacketType.SUBSCRIBE:
                    packet = DecodeSubscribe();
                    break;
                case PacketType.UNSUBSCRIBE:
                    packet = DecodeUnsubscribe();
                    break;
                case PacketType.AUTH:
                    packet = DecodeAuth();
                    break;
                // TODO: packets that a client could receive from a server
            }

            // move the buffer along some
            channel.MoveBuffer(pos);

            return packet;
        }

        private AuthPacket DecodeAuth()
        {
            AuthPacket packet = new AuthPacket();

            packet.Type = PacketType.AUTH;

            packet.ReasonCode = channel.Buffer[pos++];

            if (remainingLength > 0)
            {
                int propertiesLength = DecodeVariableByteInteger(channel.Buffer, ref pos);
                int propertiesPos = pos;

                while (pos < propertiesPos + propertiesLength)
                {
                    byte id = channel.Buffer[pos++];

                    switch (id)
                    {
                        case 0x15:
                            packet.AuthMethod = DecodeUTF8String(channel.Buffer, ref pos);
                            break;
                        case 0x16:
                            packet.AuthData = DecodeBinaryData(channel.Buffer, ref pos);
                            break;
                        case 0x1f:
                            packet.ReasonString = DecodeUTF8String(channel.Buffer, ref pos);
                            break;
                        case 0x26:
                            string key = DecodeUTF8String(channel.Buffer, ref pos);
                            string value = DecodeUTF8String(channel.Buffer, ref pos);
                            packet.UserProperties.Add(new Pair<string, string>(key, value));
                            break;
                    }
                }
            }

            return packet;
        }

        private UnsubscribePacket DecodeUnsubscribe()
        {
            UnsubscribePacket packet = new UnsubscribePacket();

            packet.Type = PacketType.UNSUBSCRIBE;
            packet.ID = DecodeTwoByteInteger(channel.Buffer, ref pos);

            if (ProtocolVersion > 4)
            {
                int propertiesLength = DecodeVariableByteInteger(channel.Buffer, ref pos);
                int propertiesPos = pos;

                while (pos < propertiesPos + propertiesLength)
                {
                    byte id = channel.Buffer[pos++];

                    switch (id)
                    {
                        case 0x26:
                            string key = DecodeUTF8String(channel.Buffer, ref pos);
                            string value = DecodeUTF8String(channel.Buffer, ref pos);
                            packet.UserProperties.Add(new Pair<string, string>(key, value));
                            break;
                    }
                }
            }

            while (pos < channel.Buffer.Length)
            {
                string topicFilter = DecodeUTF8String(channel.Buffer, ref pos);
                packet.Topics.Add(topicFilter);
            }

            return packet;
        }

        private SubscribePacket DecodeSubscribe()
        {
            SubscribePacket packet = new SubscribePacket();

            packet.Type = PacketType.SUBSCRIBE;
            packet.ID = DecodeTwoByteInteger(channel.Buffer, ref pos);

            if (ProtocolVersion > 4)
            {
                int propertiesLength = DecodeVariableByteInteger(channel.Buffer, ref pos);
                int propertiesPos = pos;

                while (pos < propertiesPos + propertiesLength)
                {
                    byte id = channel.Buffer[pos++];

                    // TODO: need to be stored in session state

                    switch (id)
                    {
                        case 0x0b:
                            packet.SubscriptionID = DecodeVariableByteInteger(channel.Buffer, ref pos);
                            break;
                        case 0x26:
                            string key = DecodeUTF8String(channel.Buffer, ref pos);
                            string value = DecodeUTF8String(channel.Buffer, ref pos);
                            packet.UserProperties.Add(new Pair<string, string>(key, value));
                            break;
                    }
                }
            }

            int payloadLength = remainingLength - pos + 2;
            while (pos < channel.Buffer.Length)
            {
                string topic = DecodeUTF8String(channel.Buffer, ref pos);
                byte qos = channel.Buffer[pos++];

                // TODO: these need to move to client session state
                if (packet.Subscriptions.ContainsKey(topic))
                {
                    packet.Subscriptions[topic] = qos;
                }
                else
                {
                    packet.Subscriptions.Add(topic, qos);
                }
            }

            return packet;
        }

        private DisconnectPacket DecodeDisconnect()
        {
            DisconnectPacket packet = new DisconnectPacket();

            packet.Type = PacketType.DISCONNECT;

            if (ProtocolVersion > 4)
            {
                if (remainingLength > 0)
                {
                    packet.ReasonCode = channel.Buffer[pos++];
                }
                if (remainingLength > 1)
                {
                    int propertiesLength = DecodeVariableByteInteger(channel.Buffer, ref pos);
                    int propertiesPos = pos;

                    while (pos < propertiesPos + propertiesLength)
                    {
                        byte id = channel.Buffer[pos++];

                        // TODO: need to be stored in session state

                        switch (id)
                        {
                            case 0x11:
                                packet.SessionExpiry = DecodeFourByteInteger(channel.Buffer, ref pos);
                                break;
                            case 0x1f:
                                packet.ReasonString = DecodeUTF8String(channel.Buffer, ref pos);
                                break;
                            case 0x26:
                                string key = DecodeUTF8String(channel.Buffer, ref pos);
                                string value = DecodeUTF8String(channel.Buffer, ref pos);
                                packet.UserProperties.Add(new Pair<string, string>(key, value));
                                break;
                            case 0x1c:
                                packet.ServerReference = DecodeUTF8String(channel.Buffer, ref pos);
                                break;
                        }
                    }
                }
            }

            // TODO: handle at a higher level
            // stream.Close();
            // channel.Close();

            return packet;
        }

        private PingreqPacket DecodePingreq()
        {
            PingreqPacket packet = new PingreqPacket();

            packet.Type = PacketType.PINGREQ;
            // do nothing but keep this for consistency and possible future proofing

            // TODO: this needs to be handled
            // byte[] pingresp = MQTTEncoder.MakePINGRESP();
            // sendTask = stream.WriteAsync(pingresp, 0, pingresp.Length);
            // offset = 0;

            return packet;
        }

        private Packet DecodeConnect()
        {
            ConnectPacket packet = new ConnectPacket();

            packet.Type = PacketType.CONNECT;

            // process variable header
            byte lengthMSB = channel.Buffer[pos++];
            byte lengthLSB = channel.Buffer[pos++];
            if (lengthMSB != 0 || lengthLSB != 4)
            {
                throw new InvalidDataException("Malformed request");
            }

            if (channel.Buffer[pos++] != 'M' || channel.Buffer[pos++] != 'Q' || channel.Buffer[pos++] != 'T' || channel.Buffer[pos++] != 'T')
            {
                throw new InvalidDataException("Malformed dynamic header");
            }

            ProtocolVersion = channel.Buffer[pos++];
            Console.WriteLine($"Protocol Level: {ProtocolVersion}");
            byte connectFlags = channel.Buffer[pos++];

            packet.UsernameFlag = ((connectFlags & 0x80) != 0);
            packet.PasswordFlag = ((connectFlags & 0x40) != 0);
            packet.WillRetainFlag = ((connectFlags & 0x20) != 0);
            packet.WillQoS = ((connectFlags & 0x1f) >> 3);
            packet.WillFlag = ((connectFlags & 0x4) != 0);
            packet.CleanStart = ((connectFlags & 0x2) != 0);

            if ((connectFlags & 0x1) == 1)
            {
                throw new InvalidDataException("Cannot set reserved flag");
            }

            packet.KeepAlive = DecodeTwoByteInteger(channel.Buffer, ref pos);

            if (ProtocolVersion > 4)
            {
                int propertiesLength = DecodeVariableByteInteger(channel.Buffer, ref pos);
                int propertiesPos = pos;
                // TODO: process properties if they are defined
                if (propertiesLength > 0)
                {
                    while (pos < propertiesPos + propertiesLength)
                    {
                        byte id = channel.Buffer[pos++];

                        switch (id)
                        {
                            case 0x11:
                                packet.SessionExpiry = DecodeFourByteInteger(channel.Buffer, ref pos);
                                break;
                            case 0x21:
                                packet.ReceiveMaximum = DecodeTwoByteInteger(channel.Buffer, ref pos);
                                break;
                            case 0x27:
                                packet.MaximumPacketSize = DecodeFourByteInteger(channel.Buffer, ref pos);
                                break;
                            case 0x22:
                                packet.TopicAliasMaximum = DecodeTwoByteInteger(channel.Buffer, ref pos);
                                break;
                            case 0x19:
                                packet.RequestResponse = channel.Buffer[pos++];
                                break;
                            case 0x17:
                                packet.RequestProblem = channel.Buffer[pos++];
                                break;
                            case 0x26:
                                string name = DecodeUTF8String(channel.Buffer, ref pos);
                                string value = DecodeUTF8String(channel.Buffer, ref pos);
                                packet.UserProperties.Add(new Pair<string, string>(name, value));
                                break;
                            case 0x15:
                                string authMethod = DecodeUTF8String(channel.Buffer, ref pos);
                                break;
                            case 0x16:
                                DecodeBinaryData(channel.Buffer, ref pos);
                                break;
                        }
                    }
                }
            }

            // process payload
            packet.ClientID = DecodeUTF8String(channel.Buffer, ref pos);
            if (string.IsNullOrEmpty(packet.ClientID))
            {
                packet.ClientID = Guid.NewGuid().ToString("N");
            }

            if (packet.WillFlag && ProtocolVersion > 4)
            {
                int willPropertiesLength = DecodeVariableByteInteger(channel.Buffer, ref pos);
                int willPropertiesPos = pos;
                while (pos < willPropertiesPos + willPropertiesLength)
                {
                    byte id = channel.Buffer[pos++];

                    switch (id)
                    {
                        case 0x18:
                            packet.WillDelayInterval = DecodeFourByteInteger(channel.Buffer, ref pos);
                            break;
                        case 0x01:
                            packet.Format = channel.Buffer[pos++];
                            break;
                        case 0x02:
                            packet.MessageExpiry = DecodeFourByteInteger(channel.Buffer, ref pos);
                            break;
                        case 0x03:
                            packet.ContentType = DecodeUTF8String(channel.Buffer, ref pos);
                            break;
                        case 0x08:
                            packet.ResponseTopic = DecodeUTF8String(channel.Buffer, ref pos);
                            break;
                        case 0x09:
                            packet.Correlation = DecodeBinaryData(channel.Buffer, ref pos);
                            break;
                        case 0x26:
                            string key = DecodeUTF8String(channel.Buffer, ref pos);
                            string value = DecodeUTF8String(channel.Buffer, ref pos);
                            packet.WillProperties.Add(new Pair<string, string>(key, value));
                            break;
                    }
                }
                
                packet.WillTopic = DecodeUTF8String(channel.Buffer, ref pos);
            }

            if (packet.UsernameFlag)
            {
                packet.Username = DecodeUTF8String(channel.Buffer, ref pos);
            }

            if (packet.PasswordFlag)
            {
                packet.Password = DecodeBinaryData(channel.Buffer, ref pos);
            }

            // TODO: add checks that CONNACK is the right way to go
            // Also add some more checks around keep alive etc.
            // and make the server reference configurable
            // TODO: handle redirection with reason code 0x9c or 0x9d if using multiple servers
            // TODO: check for additional auth information and handle JWT tokens maybe?

            // TODO: this needs to be handled at a higher level
            // byte[] connack = MQTTEncoder.MakeCONNACK(100, 100, 1, 1, 65535, packet.ID, 100, 32768, "imaserver");
            // sendTask = stream.WriteAsync(connack, 0, connack.Length);
            // offset = 0;

            return packet;
        }

        private PublishPacket DecodePublish()
        {
            PublishPacket packet = new PublishPacket();

            packet.Type = PacketType.PUBLISH;

            packet.Duplicate = (bool)((fixedHeader & 0x8) != 0);
            packet.QoS = (int)(fixedHeader >> 1) & 0x3;
            packet.Retain = (bool)((fixedHeader & 0x1) != 0);

            Console.WriteLine($"QoS Level: {packet.QoS}");

            packet.TopicName = DecodeUTF8String(channel.Buffer, ref pos);
            packet.ID = 0;
            if (packet.QoS > 0)
            {
                packet.ID = DecodeTwoByteInteger(channel.Buffer, ref pos);
            }

            if (ProtocolVersion > 4)
            {
                int propertiesLength = DecodeVariableByteInteger(channel.Buffer, ref pos);
                int propertiesPos = pos;
                while (pos < propertiesPos + propertiesLength)
                {
                    byte id = channel.Buffer[pos++];

                    switch (id)
                    {
                        case 0x01:
                            packet.Format = channel.Buffer[pos++];
                            break;
                        case 0x02:
                            packet.MessageExpiry = DecodeFourByteInteger(channel.Buffer, ref pos);
                            break;
                        case 0x23:
                            packet.TopicAlias = DecodeTwoByteInteger(channel.Buffer, ref pos);
                            break;
                        case 0x08:
                            packet.ResponseTopic = DecodeUTF8String(channel.Buffer, ref pos);
                            break;
                        case 0x09:
                            packet.Correlation = DecodeBinaryData(channel.Buffer, ref pos);
                            break;
                        case 0x26:
                            string key = DecodeUTF8String(channel.Buffer, ref pos);
                            string value = DecodeUTF8String(channel.Buffer, ref pos);
                            packet.UserProperties.Add(new Pair<string, string>(key, value));
                            break;
                        case 0x0b:
                            packet.SubscriptionID = DecodeVariableByteInteger(channel.Buffer, ref pos);
                            break;
                        case 0x03:
                            packet.ContentType = DecodeUTF8String(channel.Buffer, ref pos);
                            break;
                    }
                }
            }

            int payloadLength = remainingLength - pos + 2;
            packet.ApplicationMessage = new byte[payloadLength];
            Array.Copy(channel.Buffer, pos, packet.ApplicationMessage, 0, payloadLength);

            return packet;
        }

        private PubrelPacket DecodePubrel()
        {
            PubrelPacket packet = new PubrelPacket();

            packet.Type = PacketType.PUBREL;
            packet.ID = DecodeTwoByteInteger(channel.Buffer, ref pos);

            if (ProtocolVersion > 4 && remainingLength != 0)
            {
                int propertiesLength = DecodeVariableByteInteger(channel.Buffer, ref pos);
                int propertiesPos = pos;

                while (pos < propertiesPos + propertiesLength)
                {
                    byte id = channel.Buffer[pos++];


                    switch (id)
                    {
                        case 0x1f:
                            packet.ReasonString = DecodeUTF8String(channel.Buffer, ref pos);
                            break;
                        case 0x26:
                            string key = DecodeUTF8String(channel.Buffer, ref pos);
                            string value = DecodeUTF8String(channel.Buffer, ref pos);
                            packet.UserProperties.Add(new Pair<string, string>(key, value));
                            break;
                    }
                }
            }

            // TODO: this needs to be handled at a higher level to take into account QoS etc.
            // byte[] pubcomp = MQTTEncoder.MakePUBCOMP(packetID);
            // sendTask = stream.WriteAsync(pubcomp, 0, pubcomp.Length);

            return packet;
        }

        private void DecodeProperties()
        {
            // TODO: move all property and will property processing to common function
        }

        private UInt32 DecodeFourByteInteger(byte[] buffer, ref int pos)
        {
            UInt32 i = (UInt32)(buffer[pos++] << 24);
            i |= (UInt32)(buffer[pos++] << 16);
            i |= (UInt32)(buffer[pos++] << 8);
            i |= (UInt32)(buffer[pos++]);
            return i;
        }

        private ushort DecodeTwoByteInteger(byte[] buffer, ref int pos)
        {
            ushort i = (ushort)(buffer[pos++] << 8);
            i |= (ushort)(buffer[pos++]);
            return i;
        }

        private string DecodeUTF8String(byte[] buffer, ref int pos)
        {
            short length = (short)(buffer[pos++] << 8);
            length |= (short)(buffer[pos++]);

            string s = System.Text.Encoding.UTF8.GetString(buffer, pos, length);
            pos += length;

            return s;
        }

        public byte[] DecodeBinaryData(byte[] buffer, ref int pos)
        {
            short length = (short)(buffer[pos++] << 8);
            length |= (short)(buffer[pos++]);

            byte[] data = new byte[length];
            Array.Copy(buffer, pos, data, 0, length);
            pos += length;

            return data;
        }

        public int DecodeVariableByteInteger(byte[] buffer, ref int pos)
        {
            int multiplier = 1;
            int value = 0;
            byte encodedByte = 0;

            do
            {
                encodedByte = buffer[pos++];
                value += (encodedByte & 0x7f) * multiplier;
                if (multiplier > 128*128*128)
                {
                    throw new InvalidDataException("Malformed variable byte integer");
                }
                multiplier *= 128;
            } while ((encodedByte & 0x80) != 0);

            return value;
        }

        public void SendNextPendingPacket()
        {
            if (channel.SendTask == null && Session != null)
            {
                lock (Session.PendingPackets)
                {
                    if (Session.PendingPackets.Count > 0)
                    {
                        Packet packet = Session.PendingPackets.First.Value;
                        SendPacket(packet, false);
                        Session.PendingPackets.RemoveFirst();
                    }
                }
            }
        }

        public void SendPacket(Packet packet, bool priority)
        {
            if (channel.SendTask == null)
            {
                channel.SendPacket(packet);
            }
            else
            {
                if (priority)
                {
                    Session.PendingPackets.AddFirst(packet);    
                }
                else
                {
                    Session.PendingPackets.AddLast(packet);
                }
            }
        }
    }
}