using System;
using System.Threading.Tasks;
using LiteNetLib;
using LiteNetLib.Utils;

namespace LiteNetLibEchoClient
{
    internal class Program
    {
        private static string host = "localhost";
        private static int port = 9050;
        private static int maxConnections = 10;
        private static string connectionKey = "SomeConnectionKey";
        private static EventBasedNetListener listener;
        private static NetManager client;
        private static int millisecondsTimeout = 15;

        public static void Main(string[] args)
        {
            listener = new EventBasedNetListener();
            listener.ConnectionRequestEvent += (request) => Console.WriteLine($"ConnectionRequest: {request}");
            listener.DeliveryEvent += (netPeer, userData) => Console.WriteLine($"DeliveryEvent: {netPeer} {userData}");
            listener.NetworkErrorEvent += (endPoint, socketError) => Console.WriteLine($"Error: {endPoint}, {socketError}");
            //listener.NetworkLatencyUpdateEvent += (peer, latency) => Console.WriteLine($"LatencyUpdate: {peer}, {latency}");
            listener.NetworkReceiveEvent += OnNetworkReceive;
            listener.NetworkReceiveUnconnectedEvent += (point, reader, method) => Console.WriteLine($"ReceiveUnconnected: {point}, {reader.GetString()}, {method}");
            listener.NtpResponseEvent += (ntpPacket) => Console.WriteLine($"NtpResponse: {ntpPacket}");
            listener.PeerAddressChangedEvent += (peer, address) => Console.WriteLine($"PeerAddressChanged: {peer}, {address}");
            listener.PeerConnectedEvent += (peer) => Console.WriteLine($"PeerConnected: {peer}");
            listener.PeerDisconnectedEvent += (peer, info) => Console.WriteLine($"PeerDisconnected: {peer}, {info}");

            client = new NetManager(listener);
            client.Start();
            client.Connect(host, port, connectionKey);

            Task.Run(async () =>
            {
                while (true)
                {
                    client.PollEvents();
                    await Task.Delay(millisecondsTimeout);
                }
            });
            
            while (true)
            {
                var input = Console.ReadLine();
                if (input == "quit") break;
                if (IsRandomCommand(input, out var size)) input = ProcessRandomCommand(size);
                SendToServer(input);
            }
            
            client.Stop();
        }

        private static void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod)
        {
            var message = reader.GetString();
            var messageSize = message.Length;
            Console.WriteLine($"Receive: {peer} {message}, {channel}, {deliveryMethod}, {messageSize} bytes");
            reader.Recycle();
        }
        
        private static void SendToServer(string message)
        {
            var writer = new NetDataWriter();
            writer.Put(message);
            client.FirstPeer.Send(writer, DeliveryMethod.ReliableOrdered);
        }
        
        private static bool IsRandomCommand(string input, out int size)
        {
            size = 0;
            if (!input.StartsWith("random")) return false;
            var parts = input.Split(' ');
            return parts.Length == 2 && int.TryParse(parts[1], out size);
        }

        private static string ProcessRandomCommand(object size)
        {
            var characters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var random = new Random();
            var message = new char[(int)size];
            for (var i = 0; i < message.Length; i++)
                message[i] = characters[random.Next(characters.Length)];
            return new string(message);
        }
    }
}