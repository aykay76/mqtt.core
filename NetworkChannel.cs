using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;
using System.Collections.Generic;

namespace mqtt
{
    public class NetworkChannel
    {
        public enum State {
            Accepting,
            Connecting,
            Connected,
            Disconnected,
            SuspectedDead,
            ConfirmedDead
        }

        public State state;
        public IPAddress address;
        public int port;
        public DateTime lastSeen;
        public uint hash;
        private TcpClient channel;

        public DateTime disconnectTime;

        private Task<int> recvTask = null;
        private byte[] networkBuffer;
        public byte[] Buffer { get; set; }

        Stream stream = null;

        public Task SendTask { get; set; }

        public NetworkChannel(TcpClient client)
        {
            channel = client;
            networkBuffer = new byte[1500];
        }

        public void InitialiseServer(bool tls, string certificatePath)
        {
            NetworkStream networkStream = channel.GetStream();

            if (tls)
            {
                SslStream secureStream = new SslStream(networkStream, true);
                // load PFX, private and public key for encryption
                X509Certificate cert = X509Certificate2.CreateFromCertFile(certificatePath);
                secureStream.AuthenticateAsServer(cert);

                stream = secureStream;
            }

            stream = networkStream;
        }

        public void InitialiseClient(string serverName, bool tls, string certificatePath)
        {
            NetworkStream networkStream = channel.GetStream();

            if (tls)
            {
                SslStream secureStream = new SslStream(networkStream, true);
                // load PFX, private and public key for encryption
                X509Certificate cert = X509Certificate2.CreateFromCertFile(certificatePath);
                secureStream.AuthenticateAsClient(serverName);

                stream = secureStream;
            }

            stream = networkStream;
        }

        public bool HasData()
        {
            if (Buffer != null && Buffer.Length > 0)
            {
                Console.WriteLine("Getting existing data from buffer");
                return true;
            }

            // process what we have in buffer before trying to get any more
            if (recvTask == null && channel != null)
            {
                try
                {
                    Console.WriteLine("Looking for more data on network");
                    recvTask = stream.ReadAsync(networkBuffer, 0, networkBuffer.Length);
                }
                catch (ObjectDisposedException)
                {
                    Console.WriteLine("It seems the other end gave up on me, disconnected");
                    throw;
                }
            }

            if (recvTask != null && recvTask.IsCompletedSuccessfully)
            {
                try
                {
                    int bytesRead = recvTask.Result;
                    if (bytesRead == 0)
                    {
                        state = State.Disconnected;
                        disconnectTime = DateTime.Now;
                        channel.Dispose();
                        channel = new TcpClient();
                    }
                    else if (bytesRead == -1)
                    {
                        // TODO: some other error occurred
                    }
                    else
                    {
                        Console.WriteLine($"{bytesRead} bytes read.");
                        Console.WriteLine(System.Text.Encoding.UTF8.GetString(networkBuffer));
                        
                        // Copy network buffer into packet buffer and return true (we has data)
                        if (Buffer == null)
                        {
                            Buffer = new byte[bytesRead];
                            Array.Copy(networkBuffer, Buffer, bytesRead);
                        }
                        else
                        {
                            byte[] newBuffer = new byte[Buffer.Length + bytesRead];
                            Array.Copy(Buffer, newBuffer, Buffer.Length);
                            Array.Copy(networkBuffer, 0, newBuffer, Buffer.Length, bytesRead);
                            Buffer = newBuffer;
                        }
                        recvTask = null;

                        if (bytesRead == 16)
                        {
                            return true;
                        }

                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            }

            return false;
        }

        public void SendPacket(Packet packet)
        {
            if (SendTask == null)
            {
                SendTask = stream.WriteAsync(packet.Payload, 0, packet.Payload.Length);
            }

            if (SendTask.IsCompletedSuccessfully)
            {
                SendTask = null;
            }
        }

        public void MoveBuffer(int pos)
        {
            if (Buffer.Length == pos)
            {
                Buffer = null;
                return;
            }

            byte[] newBuffer = new byte[Buffer.Length - pos];
            Array.Copy(Buffer, pos, newBuffer, 0, Buffer.Length - pos);
            Buffer = newBuffer;
        }
    }
}
