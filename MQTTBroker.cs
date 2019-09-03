using System;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;

namespace mqtt
{
    public class MQTTBroker
    {
        TcpListener listener = new TcpListener(IPAddress.Any, 1883);
        Task<TcpClient> listenerTask = null;
        List<MQTTClient> clients = new List<MQTTClient>();
        ServerSettings serverSettings = new ServerSettings();

        // list of saved sessions, when a client connects if there was an existing session, it will be picked from this list
        // map saved session state to client ID so that it can be retrieved if necessary
        Dictionary<string, SessionState> savedSessions = new Dictionary<string, SessionState>();

        public void Run()
        {
            // Initialise
            // TODO: use 8883 for TLS, making this configurable including cert path etc.

            try
            {
                Console.WriteLine("Starting listener");
                listener.Start();
            }
            catch (SocketException)
            {
                Console.WriteLine("Could not bind socket, already in use?");
                throw;
            }
        }

        public void Loop()
        {
            List<MQTTClient> deadClients = new List<MQTTClient>();

            // 1. Check for pending connections
            if (listener.Pending())
            {
                listenerTask = listener.AcceptTcpClientAsync();
            }

            // 2. Check for successfully accepted connection
            if (listenerTask != null && listenerTask.IsCompletedSuccessfully)
            {
                // establish the network channel, then attach to a new client object
                NetworkChannel channel = new NetworkChannel(listenerTask.Result);
                channel.InitialiseServer(false, "");

                MQTTClient client = new MQTTClient(channel);
                // manage client state
                clients.Add(client);

                listenerTask = null;
            }
            
            // 3. Is there anything to receive?
            foreach (MQTTClient client in clients)
            {
                try
                {
                    Packet packet = client.GetPacket();
                    if (packet != null)
                    {
                        // got a new packet, act on it
                        switch (packet.Type)
                        {
                            case PacketType.CONNECT:
                                ProcessConnect(client, packet as ConnectPacket);
                                break;
                            case PacketType.PUBLISH:
                                ProcessPublish(client, packet as PublishPacket);
                                break;
                            case PacketType.PUBREL:
                                ProcessPubrel(client, packet as PubrelPacket);
                                break;
                            case PacketType.SUBSCRIBE:
                                ProcessSubscribe(client, packet as SubscribePacket);
                                break;
                            case PacketType.UNSUBSCRIBE:
                                ProcessUnsubscribe(client, packet as UnsubscribePacket);
                                break;
                            case PacketType.PINGREQ:
                                ProcessPingreq(client, packet as PingreqPacket);
                                break;
                        }
                    }

                    // 4. is there anything to send?
                    client.SendNextPendingPacket();

                    // TODO: 5. check for any expired sessions or keep alive expiries
                    // TODO: add to deadclients list and close connections
                }
                catch (ObjectDisposedException)
                {
                    Console.WriteLine("Client closed connection, adding to dead clients list");
                    deadClients.Add(client);
                }
            }

            // if session expiry and connection is closed then delete session
            foreach (var client in deadClients)
            {
                Console.WriteLine("Removing dead client");
                clients.Remove(client);
            }
            
            deadClients.Clear();
        }

        private void ProcessConnect(MQTTClient client, ConnectPacket packet)
        {
            // *** handle session *** //
            // check clean start and other aspects of session manageemnt
            // if new session
            if (packet.CleanStart)
            {
                if (savedSessions.ContainsKey(packet.ClientID))
                {
                    savedSessions.Remove(packet.ClientID);
                }
            }

            // find session, if not found create new
            SessionState session = null;

            if (savedSessions.ContainsKey(packet.ClientID))
            {
                session = savedSessions[packet.ClientID];

                if (session.StartTime.AddSeconds(packet.SessionExpiry) < DateTime.Now)
                {
                    // if expired set to null
                    session = null;
                }
                else
                {
                    session.StartTime = DateTime.Now;
                }
            }

            // no previous session? create a new one
            if (session == null)
            {
                session = new SessionState();
            }

            // by the time we get here, we should have a valid session to assign to the client
            client.Session = session;

            // also copy across some other stuff
            client.ID = packet.ClientID;

            // Need to prepare a CONNACK packet and stuff it in the client's pending queue
            Packet connack = MQTTEncoder.MakeCONNACK(client.ID, serverSettings);
            client.SendPacket(connack, false);
        }

        private void ProcessPublish(MQTTClient client, PublishPacket packet)
        {
            foreach (MQTTClient other in clients)
            {
                if (other.ID != client.ID)
                {
                    foreach (KeyValuePair<string, byte> subscription in other.Session.Subscriptions)
                    {
                        // TODO: Need to find highest matching qos
                        if (topicMatchesSubscription(packet.TopicName, subscription.Key))
                        {
                            // TODO: prepare PUBLISH packets to add to other client pending queue
                        }
                    }
                }
            }

            // clients processed, respond to sender
            if (packet.QoS == 1)
            {
                Packet puback = MQTTEncoder.MakePUBACK(packet.ID);
                client.SendPacket(puback, false);
            }
            else if (packet.QoS == 2)
            {
                Packet pubrec = MQTTEncoder.MakePUBREC(packet.ID);
                client.SendPacket(pubrec, false);
            }
        }

        private void ProcessPubrel(MQTTClient client, PubrelPacket packet)
        {
            Packet pubcomp = MQTTEncoder.MakePUBCOMP(packet.ID);
            client.SendPacket(pubcomp, false);
        }

        private void ProcessSubscribe(MQTTClient client, SubscribePacket packet)
        {
            List<byte> returnCodes = new List<byte>();
            foreach (string topic in packet.Subscriptions.Keys)
            {
                client.Session.Subscriptions[topic] = packet.Subscriptions[topic];
                returnCodes.Add(packet.Subscriptions[topic]);
            }

            // TODO: register the client's subscription so that it can be matched against future publishings

            // TODO: save the subscriptions in the session state

            // TODO: when successfully registered return a SUBACK
            
            Console.WriteLine("Sending SUBACK");
            Packet suback = MQTTEncoder.MakeSUBACK(packet.ID, returnCodes);
            client.SendPacket(suback, false);
        }

        private void ProcessUnsubscribe(MQTTClient client, UnsubscribePacket packet)
        {
            Console.WriteLine("Sending UNSUBACK");
            Packet suback = MQTTEncoder.MakeUNSUBACK(packet.ID);
            client.SendPacket(suback, false);
        }

        private void ProcessPingreq(MQTTClient client, PingreqPacket packet)
        {
            PingrespPacket response = MQTTEncoder.MakePINGRESP();
            client.SendPacket(response, true);
        }

        public void Stop()
        {
            // TODO: should gracefully ask all clients to DISCONNECT, maybe pointing to an alternative server?
            listener.Stop();
        }

        static bool topicIsValid(string topic)
        {
            string[] topicLevels = topic.Split('/');
            for (int i = 0; i < topicLevels.Length; i++)
            {
                if (topicLevels[i].Contains("+") && topicLevels[i] != "+")
                {
                    return false;
                }
                if (topicLevels[i].Contains("#") && topicLevels[i][topicLevels[i].Length - 1] != '#')
                {
                    return false;
                }
            }

            return true;
        }

        static bool topicMatchesSubscription(string topic, string subscription)
        {
            // handle the really easy case first :)
            if (subscription == "#")
            {
                if (topic[0] == '$')
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }

            // break each item down into parts
            string[] topicLevels = topic.Split('/');
            string[] subscriptionLevels = subscription.Split('/');

            // now check each part if we have a mismatch we're done
            for (int i = 0; i < topicLevels.Length; i++)
            {
                if (topicLevels[i] != subscriptionLevels[i] || subscriptionLevels[i] != "+" || subscriptionLevels[i] != "#")
                {
                    return false;
                }
            }

            return true;
        }
    }
}