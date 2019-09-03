using System;
using System.Collections.Generic;

namespace mqtt
{
    public class MQTTEncoder
    {
        static byte[] EncodeVariableByteInteger(int value)
        {
            List<byte> bytes = new List<byte>();

            int X = value;
            do
            {
                byte encodedByte = (byte)(X % 128);
                X = X / 128;
                if (X > 0)
                {
                    encodedByte |= 0x80;
                }
                bytes.Add(encodedByte);
            } while (X > 0);

            return bytes.ToArray();
        }

        static void EncodeFourByteInteger(UInt32 value, byte[] buffer, int pos)
        {
            if (buffer.Length < pos + 4) 
            {
                throw new InvalidOperationException("Give me a bigger buffer please");
            }

            buffer[pos + 3] = (byte)(value & 0xff);
            buffer[pos + 2] = (byte)((value >> 8) & 0xff);
            buffer[pos + 1] = (byte)((value >> 16) & 0xff);
            buffer[pos] = (byte)((value >> 24) & 0xff);
        }

        static void EncodeTwoByteInteger(UInt16 value, byte[] buffer, int pos)
        {
            if (buffer.Length < pos + 2)
            {
                throw new InvalidOperationException("Give me a bigger buffer please.");
            }

            buffer[pos] = (byte)((value >> 8) & 0xff);
            buffer[pos + 1] = (byte)(value & 0xff);
        }

        static void EncodeUTF8String(string value, byte[] buffer, int pos)
        {
            if (buffer.Length < value.Length + pos)
            {
                throw new InvalidOperationException("Give me a bigger buffer please");
            }

            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(value);
            Array.Copy(bytes, 0, buffer, pos, bytes.Length);
        }

        // TODO: move the parameters into a state object somewhere/how
        // TODO: need to understand whether this is success or rejection

        public static PingrespPacket MakePINGRESP()
        {
            PingrespPacket packet = new PingrespPacket();

            packet.Payload = new byte[2];
            packet.Payload[0] = 0xd0;
            packet.Payload[1] = 0;

            return packet;
        }

        public static Packet MakePUBACK(UInt16 packetID)
        {
            PubackPacket packet = new PubackPacket();

            // TODO: Need to add reason code and properties to be compliant with v5
            packet.Payload = new byte[4];
            packet.Payload[0] = 0x40;
            packet.Payload[1] = 2;
            EncodeTwoByteInteger(packetID, packet.Payload, 2);

            return packet;
        }

        public static Packet MakeUNSUBACK(UInt16 packetID)
        {
            UnsubackPacket packet = new UnsubackPacket();

            // TODO: Need to add reason code and properties to be compliant with v5
            packet.Payload = new byte[4];
            packet.Payload[0] = 0xb0;
            packet.Payload[1] = 2;
            EncodeTwoByteInteger(packetID, packet.Payload, 2);

            return packet;
        }

        public static Packet MakeSUBACK(UInt16 packetID, List<byte> returnCodes)
        {
            SubackPacket packet = new SubackPacket();

            // TODO: add return codes to payload
            // TODO: Need to add reason code and properties to be compliant with v5
            packet.Payload = new byte[4 + returnCodes.Count];
            packet.Payload[0] = 0x90;
            packet.Payload[1] = 2;
            EncodeTwoByteInteger(packetID, packet.Payload, 2);

            int pos = 4;
            foreach (byte returnCode in returnCodes)
            {
                packet.Payload[pos++] = returnCode;
            }

            return packet;
        }
        
        public static Packet MakePUBCOMP(UInt16 packetID)
        {
            PubcompPacket packet = new PubcompPacket();

            // TODO: Need to add reason code and properties to be compliant with v5
            packet.Payload = new byte[4];
            packet.Payload[0] = 0x70;
            packet.Payload[1] = 2;
            EncodeTwoByteInteger(packetID, packet.Payload, 2);
            
            return packet;
        }

        public static Packet MakePUBREC(UInt16 packetID)
        {
            PubrecPacket packet = new PubrecPacket();

            // TODO: Need to add reason code and properties to be compliant with v5
            packet.Payload = new byte[4];
            packet.Payload[0] = 0x50;
            packet.Payload[1] = 2;
            EncodeTwoByteInteger(packetID, packet.Payload, 2);

            return packet;
        }

        public static Packet MakeCONNACK(string clientID, ServerSettings serverSettings)
        {
            Packet packet = new Packet();

            // need to construct the packet backwards to be able to encode length of properties etc.
            byte[] sessionExpiryInterval = new byte[5];
            sessionExpiryInterval[0] = 0x11;
            EncodeFourByteInteger(serverSettings.SessionExpiry, sessionExpiryInterval, 1);

            // receive maximum
            byte[] receiveMaximum = new byte[3];
            receiveMaximum[0] = 0x21;
            EncodeTwoByteInteger(serverSettings.ReceiveLimit, receiveMaximum, 1);

            // max qos, could be 0 or 1, if not available then 2
            byte[] maxQos = new byte[2];
            maxQos[0] = 0x24;
            maxQos[1] = serverSettings.QoS;

            byte[] retainAvailable = new byte[2];
            retainAvailable[0] = 0x25;
            retainAvailable[1] = serverSettings.RetainSupport;

            byte[] maxPacket = new byte[5];
            maxPacket[0] = 0x27;
            EncodeFourByteInteger(serverSettings.MaxPacketSize, maxPacket, 1);

            byte[] assignedClientIdentifier = new byte[0];
            if (clientID.Length == 0)
            {
                string assignedClientID = Guid.NewGuid().ToString("N");
                assignedClientIdentifier = new byte[1 + clientID.Length];
                assignedClientIdentifier[0] = 0x12;
                EncodeUTF8String(assignedClientID, assignedClientIdentifier, 1);
            }

            byte[] topicAliasMaximum = new byte[3];
            topicAliasMaximum[0] = 0x22;
            EncodeTwoByteInteger(serverSettings.TopicAliasMax, topicAliasMaximum, 1);

            // TODO: reason string - make sure it doesn't increase the size of the packet beyond the max packet size specified by client

            // TODO: add user properties (0x26) as name/value pairs if needed

            // we will support wilcard subscriptions - no need to add anything

            // we will support subscription identifiers - no need to add anything

            // we will support shared subscriptions - no need to add anything

            byte[] serverKeepAliveBuffer = new byte[3];
            serverKeepAliveBuffer[0] = 0x13;
            EncodeTwoByteInteger(serverSettings.ServerKeepAlive, serverKeepAliveBuffer, 1);

            // TODO: add response information if needed

            byte[] serverReference = new byte[1 + serverSettings.ServerRef.Length];
            serverReference[0] = 0x1c;
            EncodeUTF8String(serverSettings.ServerRef, serverReference, 1);

            // TODO: add authentication method

            // TODO: add authentication data

            // now calculate and encode the length of the properties
            int propertiesLength = sessionExpiryInterval.Length + 
                                   receiveMaximum.Length + 
                                   maxQos.Length + 
                                   retainAvailable.Length + 
                                   maxPacket.Length + 
                                   assignedClientIdentifier.Length + 
                                   topicAliasMaximum.Length + 
                                   serverKeepAliveBuffer.Length + 
                                   serverReference.Length;
            byte[] propertiesLengthBuffer = EncodeVariableByteInteger(propertiesLength);

            int variableHeaderLength = 2 + propertiesLengthBuffer.Length + propertiesLength;
            byte[] variableHeaderLengthBuffer = EncodeVariableByteInteger(variableHeaderLength);

            // construct the packet
            int p = 0;
            packet.Payload = new byte[1 + variableHeaderLengthBuffer.Length + 2 + propertiesLengthBuffer.Length + propertiesLength];
            packet.Payload[p++] = 0x20; // control packet type 2 (CONNACK)
            Array.Copy(variableHeaderLengthBuffer, 0, packet.Payload, p, variableHeaderLengthBuffer.Length);
            p += variableHeaderLengthBuffer.Length;

            // TODO: set bit 0 whether previous session was found, for now it's zero
            //       need to check clean start flag etc. (see section 3.2.2.1.1)
            packet.Payload[p++] = 0;

            // TODO: assuming success for now, but need to handle bad reasons too (see section 3.2.2.2)
            packet.Payload[p++] = 0;
            
            // copy in the encoded properties length
            Array.Copy(propertiesLengthBuffer, 0, packet.Payload, p, propertiesLengthBuffer.Length);
            p += propertiesLengthBuffer.Length;

            // now copy in each of the properties
            Array.Copy(sessionExpiryInterval, 0, packet.Payload, p, sessionExpiryInterval.Length);
            p += sessionExpiryInterval.Length;
            Array.Copy(receiveMaximum, 0, packet.Payload, p, receiveMaximum.Length);
            p += receiveMaximum.Length;
            Array.Copy(maxQos, 0, packet.Payload, p, maxQos.Length);
            p += maxQos.Length;
            Array.Copy(retainAvailable, 0, packet.Payload, p, retainAvailable.Length);
            p += retainAvailable.Length;
            Array.Copy(maxPacket, 0, packet.Payload, p, maxPacket.Length);
            p += maxPacket.Length;
            Array.Copy(assignedClientIdentifier, 0, packet.Payload, p, assignedClientIdentifier.Length);
            p += assignedClientIdentifier.Length;
            Array.Copy(topicAliasMaximum, 0, packet.Payload, p, topicAliasMaximum.Length);
            p += topicAliasMaximum.Length;
            Array.Copy(serverKeepAliveBuffer, 0, packet.Payload, p, serverKeepAliveBuffer.Length);
            p += serverKeepAliveBuffer.Length;
            Array.Copy(serverReference, 0, packet.Payload, p, serverReference.Length);
            p += serverReference.Length;

            return packet;
        }
    }
}