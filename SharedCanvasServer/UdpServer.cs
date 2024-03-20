using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Drawing;
using System.Windows;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text.Json;
using System.Collections.Concurrent;

namespace TestServer
{
    public struct ConnectionData
    {
        public string ConnectionMessage { get; set; }
        public string DrawingBrushColorsJson { get; set; }
    }
    public class Client
    {
        public byte Id { get; set; }
        public IPEndPoint MainEndPoint { get; set; }
        public IPEndPoint DrawingDataReceivingEndPoint { get; set; }
        public string DrawingBrushColorsJson { get; set; }
    }

    public struct UniversalPoint
    {
        public double X { get; set; }
        public double Y { get; set; }
    }

    public struct QueuedPoints
    {
        public List<UniversalPoint> Points { get; set; }
        public int ClientId { get; set; }
    }

    public struct ServerDrawingDataPorts
    {
        public int DrawingDataSendingPort { get; set; }
        public int DrawingDataReceivingPort { get; set; }
    }

    public struct DrawingDataToSend
    {
        public List<UniversalPoint> Points { get; set; }
        public string DrawingBrushColorsJson { get; set; }
    }

    internal class Server
    {
        private const int connectionPort = 11000;
        private ServerDrawingDataPorts drawingDataPorts= new ServerDrawingDataPorts()
        {
            DrawingDataReceivingPort = 11001,
            DrawingDataSendingPort = 11002
        };
        
        private List<Client> connectedClients = new List<Client>();

        private List<QueuedPoints> queuedPointsCollection = new List<QueuedPoints>();


        public Server()
        {
            Thread drawingDataReceivingThread = new Thread(new ThreadStart(DrawingDataReceiving));
            drawingDataReceivingThread.Start();

            Thread drawingDataSendingThread = new Thread(new ThreadStart(DrawingDataSending));
            drawingDataSendingThread.Start();

            Connection();
        }


        private void Connection()
        {
            UdpClient connectionUdpClient = new UdpClient(connectionPort);

            try
            {
                while (true)
                {
                    IPEndPoint clientMainEndPoint = new IPEndPoint(IPAddress.Any, 0);

                    byte[] connectionDataReceivedEncoded = connectionUdpClient.Receive(ref clientMainEndPoint);
                    string connectionDataReceivedJson = Encoding.ASCII.GetString(connectionDataReceivedEncoded);
                    
                    ConnectionData connectionDataReceived = JsonSerializer.Deserialize<ConnectionData>(connectionDataReceivedJson);
                    Console.WriteLine("Received message from client {0}: " + connectionDataReceived.ConnectionMessage, clientMainEndPoint.ToString());

                    switch (connectionDataReceived.ConnectionMessage)
                    {
                        case "connect":
                            HandleConnectMessage(connectionUdpClient, clientMainEndPoint, connectionDataReceived.DrawingBrushColorsJson);
                            break;

                        case "disconnect":
                            HandleDisconnectMessage(connectionUdpClient, clientMainEndPoint);
                            break;

                        default:
                            break;
                    }
                }
            }
            catch (SocketException e)
            {
                Trace.WriteLine("SocketException: {0}", e.ToString());
            }
            finally
            {
                connectionUdpClient.Close();
            }
        }

        private void HandleConnectMessage(UdpClient connectionUdpClient, IPEndPoint clientMainEndPoint, string DrawingBrushColorsJson)
        {
            Client newConnectedClient = getNewConnectedClient(clientMainEndPoint, DrawingBrushColorsJson);
            connectedClients.Add(newConnectedClient);

            Tuple<ServerDrawingDataPorts, byte> dataToSend = Tuple.Create(drawingDataPorts, newConnectedClient.Id);

            string dataToSendSerialized = JsonSerializer.Serialize(dataToSend);
            byte[] dataToSendEncoded = Encoding.ASCII.GetBytes(dataToSendSerialized);

            connectionUdpClient.Send(dataToSendEncoded, dataToSendEncoded.Length, clientMainEndPoint);

            IPEndPoint clientDataReceivingEndPoint = new IPEndPoint(clientMainEndPoint.Address, 0);

            byte[] connectedClientIdEncoded = connectionUdpClient.Receive(ref clientDataReceivingEndPoint);
            byte connectedClientId = connectedClientIdEncoded[0];

            connectedClients
                .Where(connectedClient => connectedClient.Id == newConnectedClient.Id)
                .First()
                .DrawingDataReceivingEndPoint = clientDataReceivingEndPoint;
        }

        private void HandleDisconnectMessage(UdpClient connectionUdpClient, IPEndPoint clientMainEndPoint)
        {
            Client? connectedClientToRemove = connectedClients
                            .Where(connectedClient => connectedClient.MainEndPoint.Equals(clientMainEndPoint))
                            .FirstOrDefault();

            if (connectedClientToRemove != null)
                connectedClients.Remove((Client)connectedClientToRemove);
        }


        private void DrawingDataReceiving()
        {
            UdpClient drawingDataReceivingUdpClient = new UdpClient(drawingDataPorts.DrawingDataReceivingPort);

            while (true)
            {
                try
                {
                    IPEndPoint clientEndPoint = new IPEndPoint(IPAddress.Any, 0);
                    
                    byte[] drawingDataReceived = drawingDataReceivingUdpClient.Receive(ref clientEndPoint);
                    string drawingDataReceivedJson = Encoding.ASCII.GetString(drawingDataReceived);

                    List<UniversalPoint> pointsReceived =
                        JsonSerializer.Deserialize<List<UniversalPoint>>(drawingDataReceivedJson);

                    int clientId = connectedClients
                        .Where(connectedClient => connectedClient.MainEndPoint.Equals(clientEndPoint))
                        .Select(connectedClient => connectedClient.Id)
                        .First();

                    queuedPointsCollection.Add(new QueuedPoints()
                    {
                        Points = pointsReceived,
                        ClientId = clientId
                    });
                }
                catch (SocketException e)
                {
                    Trace.WriteLine("Socket exception: {0}", e.ToString());
                }
            }
        }

        private void DrawingDataSending()
        {
            UdpClient drawingDataSendingUdpClient = new UdpClient(drawingDataPorts.DrawingDataSendingPort);

            while (true)
            {
                if (queuedPointsCollection.Count > 0)
                {
                    QueuedPoints pointsToSend = queuedPointsCollection.First();

                    List<Client> clientsToBeSent =
                        connectedClients
                            .Where(connectedClient => connectedClient.Id != pointsToSend.ClientId)
                            .Select(connectedClient => connectedClient)
                            .ToList();

                    if (clientsToBeSent.Count > 0)
                    {
                        string clientDrawingBrushColorsJson =
                            connectedClients
                                .Where(connectedClient => connectedClient.Id == pointsToSend.ClientId)
                                .Select(connectedClient => connectedClient.DrawingBrushColorsJson)
                                .First();

                        DrawingDataToSend drawingDataToSend = new DrawingDataToSend()
                        {
                            Points = pointsToSend.Points,
                            DrawingBrushColorsJson = clientDrawingBrushColorsJson
                        };
                        string drawingDataToSendJson = JsonSerializer.Serialize(drawingDataToSend);
                        byte[] drawingDataToSendEncoded = Encoding.ASCII.GetBytes(drawingDataToSendJson);

                        foreach (Client clientToBeSent in clientsToBeSent)
                        {
                            drawingDataSendingUdpClient
                                .Send(drawingDataToSendEncoded, drawingDataToSendEncoded.Length, clientToBeSent.DrawingDataReceivingEndPoint);
                        }

                        queuedPointsCollection.Remove(pointsToSend);
                    }
                }
            }
        }


        private Client getNewConnectedClient(IPEndPoint clientMainEndPoint, string DrawingBrushColorsJson)
        {
            byte id = 0;
            List<int> ids = new List<int>();

            connectedClients.ForEach(client =>
            {
                ids.Add(client.Id);
            });

            while (ids.Contains(id))
                id++;

            return new Client()
            {
                Id = id,
                MainEndPoint = clientMainEndPoint,
                DrawingDataReceivingEndPoint = null,
                DrawingBrushColorsJson = DrawingBrushColorsJson
            };
        }
    }
}
