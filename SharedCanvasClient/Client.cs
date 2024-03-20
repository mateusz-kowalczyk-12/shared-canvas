using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Drawing;
using System.Linq.Expressions;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Security.Cryptography;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;

namespace SharedCanvasClient
{
    public struct ConnectionData
    {
        public string ConnectionMessage { get; set; }
        public string DrawingBrushColorsJson { get; set; }
    }

    public struct ServerDrawingDataPorts
    {
        public int DrawingDataSendingPort { get; set; }
        public int DrawingDataReceivingPort { get; set; }
    }

    public struct DrawingDataReceived
    {
        public List<UniversalPoint> Points { get; set; }
        public string DrawingBrushColorsJson { get; set; }
    }


    internal class Client
    {
        private MainWindow window;

        private UdpClient mainUdpClient = new UdpClient();
        private UdpClient drawingDataReceivingUdpClient = new UdpClient();

        private IPAddress serverIP;

        private int serverConnectionPort = 0;
        private ServerDrawingDataPorts? serverDrawingDataPorts = null;
        
        Thread drawingDataReceivingThread;


        public Client(MainWindow window)
        {
            this.window = window;
        }

        public ServerDrawingDataPorts? Connect(IPAddress serverIP, int serverConnectionPort, byte[] drawingBrushColors)
        {
            this.serverIP = serverIP;
            this.serverConnectionPort = serverConnectionPort;

            try
            {
                IPEndPoint serverConnectionEndPoint = new IPEndPoint(serverIP, serverConnectionPort);

                ConnectionData connectionDataToSend = new ConnectionData()
                {
                    ConnectionMessage = "connect",
                    DrawingBrushColorsJson = JsonSerializer.Serialize(drawingBrushColors)
                };
                
                string connectionDataToSendJson = JsonSerializer.Serialize(connectionDataToSend);
                byte[] connectionDataToSendEncoded = Encoding.ASCII.GetBytes(connectionDataToSendJson);
                
                mainUdpClient.Send(connectionDataToSendEncoded, connectionDataToSendEncoded.Length, serverConnectionEndPoint);

                byte[] dataReceivedEncoded = mainUdpClient.Receive(ref serverConnectionEndPoint);
                
                string dataReceivedJson = Encoding.ASCII.GetString(dataReceivedEncoded);
                Tuple<ServerDrawingDataPorts, byte> dataReceived
                    = JsonSerializer.Deserialize<Tuple<ServerDrawingDataPorts, byte>>(dataReceivedJson);

                serverDrawingDataPorts = dataReceived.Item1;
                byte[] idEncoded = { dataReceived.Item2 };
                drawingDataReceivingUdpClient.Send(idEncoded, idEncoded.Length, serverConnectionEndPoint);

                drawingDataReceivingThread = new Thread(new ThreadStart(DrawingDataReceiving));
                drawingDataReceivingThread.Start();
            }
            catch (SocketException e)
            {
                Trace.WriteLine("SocketException: {0}", e.ToString());
            }
            
            return serverDrawingDataPorts;
        }

        public int Disconnect()
        {
            try
            {
                IPEndPoint serverConnectionEndPoint = new IPEndPoint(serverIP, serverConnectionPort);
                
                while (serverDrawingDataPorts != null)
                {
                    ConnectionData connectionDataToSend = new ConnectionData()
                    {
                        ConnectionMessage = "disconnect",
                        DrawingBrushColorsJson = ""
                    };
                    string connectionDataToSendJson = JsonSerializer.Serialize(connectionDataToSend);
                    byte[] messageToSendEncoded = Encoding.ASCII.GetBytes(connectionDataToSendJson);

                    mainUdpClient.Send(messageToSendEncoded, messageToSendEncoded.Length, serverConnectionEndPoint);
                    serverDrawingDataPorts = null;
                }
            }
            catch (SocketException e)
            {
                Trace.WriteLine("SocketException: {0}", e.ToString());
            }

            return serverConnectionPort;
        }

        public void SendDrawingData(List<UniversalPoint> drawingDataToSend)
        {
            try
            {
                IPEndPoint serverDrawingDataReceivingEndPoint =
                    new IPEndPoint(serverIP, ((ServerDrawingDataPorts)serverDrawingDataPorts).DrawingDataReceivingPort);

                string drawingDataToSendJson = JsonSerializer.Serialize(drawingDataToSend);
                byte[] drawingDataToSendEncoded = Encoding.ASCII.GetBytes(drawingDataToSendJson);

                mainUdpClient.Send(drawingDataToSendEncoded, drawingDataToSendEncoded.Length, serverDrawingDataReceivingEndPoint);
            }
            catch (SocketException e)
            {
                Trace.WriteLine("SocketException: {0}", e.ToString());
            }
        }

        public void DrawingDataReceiving()
        {
            try
            {
                while (serverDrawingDataPorts != null)
                {
                    IPEndPoint serverDrawingDataSendingEndPoint =
                        new IPEndPoint(serverIP, ((ServerDrawingDataPorts)serverDrawingDataPorts).DrawingDataSendingPort);
                    byte[] drawingDataReceivedEncoded = drawingDataReceivingUdpClient.Receive(ref serverDrawingDataSendingEndPoint);

                    string drawingDataReceivedJson = Encoding.ASCII.GetString(drawingDataReceivedEncoded);
                    DrawingDataReceived drawingDataReceived =
                        JsonSerializer.Deserialize<DrawingDataReceived>(drawingDataReceivedJson);

                    window.Dispatcher.Invoke(() =>
                    {
                        window.DrawReceivedData(drawingDataReceived);
                    });
                }
            }
            catch (SocketException e)
            {
                Trace.WriteLine("Socket exception: " + e.ToString());
            }
        }
    }
}
