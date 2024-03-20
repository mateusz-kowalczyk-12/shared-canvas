using System;
using System.Collections.Generic;
using System.DirectoryServices.ActiveDirectory;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SharedCanvasClient
{
    [Serializable]
    public struct UniversalPoint
    {
        public double X {  get; set; }
        public double Y { get; set; }
    }

    public partial class MainWindow : System.Windows.Window
    {
        private Client client;

        UniversalPoint lastCanvasPoint;

        Brush drawingBrush = new SolidColorBrush(Colors.Black);
        byte[] drawingBrushColors = { 0, 0, 0 };

        private List<UniversalPoint> pointsCollected;


        public MainWindow()
        {
            InitializeComponent();

            client = new Client(this);

            pointsCollected = new List<UniversalPoint>();
        }


        private void Connect(object sender, RoutedEventArgs e)
        {
            IPAddress serverIP = IPAddress.Parse(serverIPTextBox.Text);
            int serverConnectionPort = int.Parse(serverPortTextBox.Text);
            
            ServerDrawingDataPorts serverDrawingDataPorts =
                (ServerDrawingDataPorts)client.Connect(serverIP, serverConnectionPort, drawingBrushColors);

            connectButton.IsEnabled = false;
            disconnectButton.IsEnabled = true;
            serverIPTextBox.IsEnabled = false;
            serverPortTextBox.Text = $"{serverDrawingDataPorts.DrawingDataReceivingPort}/{serverDrawingDataPorts.DrawingDataSendingPort}";
            serverPortTextBox.IsEnabled = false;
            connectionStateLabel.Content = "Connected";
            connectionStateLabel.Foreground = new SolidColorBrush(Color.FromRgb(50, 150, 100));
        }

        private void Disconnect(object sender, RoutedEventArgs e)
        {
            int serverConnectionPort = client.Disconnect();

            serverIPTextBox.IsEnabled = true;
            serverPortTextBox.Text = serverConnectionPort.ToString();
            serverPortTextBox.IsEnabled = true;
            connectionStateLabel.Content = "Not connected";
            connectionStateLabel.Foreground = new SolidColorBrush(Color.FromRgb(210, 35, 35));
            disconnectButton.IsEnabled = false;
            connectButton.IsEnabled = true;
        }


        private void ChooseColor(object sender, MouseButtonEventArgs e)
        {
            ColorDialog colorDialog = new ColorDialog();
            if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                drawingBrush = new SolidColorBrush(Color.FromRgb(
                    colorDialog.Color.R,
                    colorDialog.Color.G,
                    colorDialog.Color.B
                ));
            }

            drawingBrushColors.SetValue(colorDialog.Color.R, 0);
            drawingBrushColors.SetValue(colorDialog.Color.G, 1);
            drawingBrushColors.SetValue(colorDialog.Color.B, 2);

            colorChoiceCanvas.Background = drawingBrush;
        }


        private void StartedDrawingOnCanvas(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Point newPoint = e.GetPosition(canvas);
                UniversalPoint newUniversalPoint = new UniversalPoint()
                {
                    X = newPoint.X,
                    Y = newPoint.Y
                };

                lastCanvasPoint = newUniversalPoint;
                pointsCollected.Add(newUniversalPoint);
            }
        }

        private void DrawingOnCanvas(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Point newPoint = e.GetPosition(canvas);
                UniversalPoint newUniversalPoint = new UniversalPoint()
                {
                    X = newPoint.X,
                    Y = newPoint.Y
                };

                if (!newUniversalPoint.Equals(lastCanvasPoint))
                {
                    Line newLine = new Line()
                    {
                        X1 = lastCanvasPoint.X,
                        Y1 = lastCanvasPoint.Y,
                        X2 = newPoint.X,
                        Y2 = newPoint.Y,
                        Stroke = drawingBrush,
                        StrokeThickness = 1
                    };
                    canvas.Children.Add(newLine);

                    pointsCollected.Add(newUniversalPoint);
                    lastCanvasPoint = newUniversalPoint;
                }
            }
        }

        public void DrawReceivedData(DrawingDataReceived drawingDataReceived)
        {
            UniversalPoint[] pointsToDraw = drawingDataReceived.Points.ToArray();
            byte[] drawingColors = JsonSerializer.Deserialize<byte[]>(drawingDataReceived.DrawingBrushColorsJson);

            for (int i = 0; i + 1 < pointsToDraw.Length; i++)
            {
                Line newLine = new Line()
                {
                    X1 = pointsToDraw[i].X,
                    Y1 = pointsToDraw[i].Y,
                    X2 = pointsToDraw[i + 1].X,
                    Y2 = pointsToDraw[i + 1].Y,
                    Stroke = new SolidColorBrush(Color.FromRgb(
                        drawingColors[0],
                        drawingColors[1],
                        drawingColors[2]
                    )),
                    StrokeThickness = 1
                };
                canvas.Children.Add(newLine);
            }
        }

        private void FinishedDrawingOnCanvas(object sender, MouseButtonEventArgs e)
        {
            client.SendDrawingData(pointsCollected);
            pointsCollected.Clear();
        }
    }
}
