using System;
using System.Threading;
using System.Threading.Tasks;
using LiteNetLib;
using LiteNetLib.Utils;

namespace LiteNetLibEchoServer
{
    internal class Program
    {
        private static int port = 9050;
        private static int maxConnections = 10;
        private static string connectionKey = "SomeConnectionKey";
        private static EventBasedNetListener listener;
        private static NetManager server;
        private static int millisecondsTimeout = 15;
        
        public static void Main(string[] args)
        {
            listener = new EventBasedNetListener();
            listener.ConnectionRequestEvent += (request) => Console.WriteLine($"ConnectionRequest: {request}");
            listener.DeliveryEvent += (netPeer, userData) => Console.WriteLine($"DeliveryEvent: {netPeer} {userData}");
            listener.NetworkErrorEvent += (endPoint, socketError) => Console.WriteLine($"Error: {endPoint}, {socketError}");
            //listener.NetworkLatencyUpdateEvent += (peer, latency) => Console.WriteLine($"LatencyUpdate: {peer}, {latency}");
            listener.NetworkReceiveEvent += OnNetworkReceiveEvent;
            listener.NetworkReceiveUnconnectedEvent += (point, reader, method) => Console.WriteLine($"ReceiveUnconnected: {point}, {reader}, {method}");
            listener.NtpResponseEvent += (ntpPacket) => Console.WriteLine($"NtpResponse: {ntpPacket}");
            listener.PeerAddressChangedEvent += (peer, address) => Console.WriteLine($"PeerAddressChanged: {peer}, {address}");
            listener.PeerConnectedEvent += (peer) => Console.WriteLine($"PeerConnected: {peer}");
            listener.PeerDisconnectedEvent += (peer, info) => Console.WriteLine($"PeerDisconnected: {peer}, {info}");

            listener.ConnectionRequestEvent += OnConnectionRequestEvent;
            listener.PeerConnectedEvent += OnPeerConnectedEvent;

            server = new NetManager(listener);
            server.Start(port);

            _ = Task.Run(async () =>
            {
                while (true)
                {
                    server.PollEvents();
                    await Task.Delay(millisecondsTimeout);
                }
            });
            
            while (true)
            {
                var input = Console.ReadLine();
                if (input == "quit") break;
                if (IsRandomCommand(input, out var size)) input = ProcessRandomCommand(size);
                Broadcast(input);
            }
            
            server.Stop();
        }

        private static void OnConnectionRequestEvent(ConnectionRequest request)
        {
            if (server.ConnectedPeersCount < maxConnections)
                request.AcceptIfKey(connectionKey);
            else
                request.Reject();
        }

        private static void OnPeerConnectedEvent(NetPeer peer)
        {
            var writer = new NetDataWriter();
            writer.Put("Welcome to the server!");
            peer.Send(writer, DeliveryMethod.ReliableOrdered);
        }

        private static void OnNetworkReceiveEvent(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod)
        {
            var message = reader.GetString();
            var messageSize = message.Length;
            Console.WriteLine($"Receive: {peer} {message}, {channel}, {deliveryMethod}, {messageSize} bytes");
            reader.Recycle();
            
            // Echo!
            var writer = new NetDataWriter();
            writer.Put(message);
            peer.Send(writer, DeliveryMethod.ReliableOrdered);
        }

        private static void Broadcast(string message)
        {
            var writer = new NetDataWriter();
            writer.Put(message);
            server.SendToAll(writer, DeliveryMethod.ReliableOrdered);
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