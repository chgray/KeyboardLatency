using System.Printing;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography.Pkcs;

namespace KeyboardLatency
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

           RecordingMediaElement.MouseDown += RecordingMediaElement_MouseDown;
        }

        private enum ButtonState { 
            Unknown = 0,
            Cord1 = 1,
            Cord2 = 2
        }

        private ButtonState TriggerButtonState = ButtonState.Unknown;
        private ButtonState SetStopButtonState = ButtonState.Unknown;

        private Point TriggerButtonA;
        private Point TriggerButtonB;

        private Point SetStopButtonA;
        private Point SetStopButtonB;

        private void RecordingMediaElement_MouseDown(object sender, MouseEventArgs e)
        {
            var p = e.GetPosition(RecordingMediaElement);
            System.Diagnostics.Debug.Print($"{p.X}, {p.Y}");

            var percentX = p.X / RecordingMediaElement.ActualWidth;
            var percentY = p.Y / RecordingMediaElement.ActualHeight;

            var pixelX = RecordingMediaElement.NaturalVideoWidth * percentX;
            var pixelY = RecordingMediaElement.NaturalVideoHeight * percentY;

            System.Diagnostics.Debug.Print($"{p.X}, {p.Y} ==> {pixelX}, {pixelY}");

            if (TriggerButtonState == ButtonState.Cord1)
            {
                TriggerButtonA = new Point(pixelX, pixelY);
                TriggerButtonState = ButtonState.Cord2;
            } else if (TriggerButtonState == ButtonState.Cord2)
            {
                TriggerButtonB = new Point(pixelX, pixelY);
                TriggerButtonState = ButtonState.Unknown;

                if (TriggerButtonA.Y > TriggerButtonB.Y)
                {
                    Point temp = TriggerButtonA;
                    TriggerButtonA = TriggerButtonB;
                    TriggerButtonB = temp;
                }

                if (TriggerButtonA.X > TriggerButtonB.X)
                {
                    var tempX = TriggerButtonA.X;
                    TriggerButtonA.X = TriggerButtonB.X;
                    TriggerButtonB.X = tempX;
                }
            }

            if (SetStopButtonState == ButtonState.Cord1)
            {
                SetStopButtonA = new Point(pixelX, pixelY);
                SetStopButtonState = ButtonState.Cord2;
            }
            else if (SetStopButtonState == ButtonState.Cord2)
            {
                SetStopButtonB = new Point(pixelX, pixelY);
                SetStopButtonState = ButtonState.Unknown;

                if (SetStopButtonA.Y > SetStopButtonB.Y)
                {
                    Point temp = SetStopButtonA;
                    SetStopButtonA = SetStopButtonB;
                    SetStopButtonB = temp;
                }
                if (SetStopButtonA.X > SetStopButtonB.X)
                {
                    var tempX = SetStopButtonA.X;
                    SetStopButtonA.X = SetStopButtonB.X;
                    SetStopButtonB.X = tempX;
                }
            }
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            RecordingMediaElement.Position = TimeSpan.FromMicroseconds(TimeSlider.Value);
            System.Diagnostics.Debug.WriteLine(TimeSlider.Value);   
        }



        private void Button_Click(object sender, RoutedEventArgs e)
        {
            VisualBrush sourceBrush = new VisualBrush(RecordingMediaElement);
            DrawingVisual drawingVisual = new DrawingVisual();
            DrawingContext drawingContet = drawingVisual.RenderOpen();

            using (drawingContet)
            {
                drawingContet.DrawRectangle(sourceBrush, null, new Rect(new Point(0, 0), new Point(RecordingMediaElement.RenderSize.Width, RecordingMediaElement.RenderSize.Height)));

                RenderTargetBitmap renderTarget = new RenderTargetBitmap((int)RecordingMediaElement.NaturalVideoWidth, (int)RecordingMediaElement.NaturalVideoHeight, 96, 96, PixelFormats.Pbgra32);
                renderTarget.Render(drawingVisual);

                //Extract Pixel Data
                int stride = (renderTarget.PixelWidth * (renderTarget.Format.BitsPerPixel / 8));
                byte[] pixelData = new byte[stride * renderTarget.PixelHeight];
                renderTarget.CopyPixels(pixelData, stride, 0);

                float rtotal = 0;
                float gtotal = 0;
                float btotal = 0;
                int count = 0;
                for (int x = (int)TriggerButtonA.X; x < TriggerButtonB.X; ++x)
                {
                    for (int y = (int)TriggerButtonA.Y; y < TriggerButtonB.Y; ++y)
                    {
                        int index = (int)y * stride + (int)x * 4; // 4 bytes per pixel (BGRA)
                        byte blue = pixelData[index];
                        byte green = pixelData[index + 1];
                        byte red = pixelData[index + 2];
                        byte alpha = pixelData[index + 3];
                        Color pixelColor = Color.FromArgb(alpha, red, green, blue);

                        rtotal += red;
                        gtotal += green;
                        btotal += blue;
                        count++;                        
                    }
                }


                JpegBitmapEncoder jpg = new JpegBitmapEncoder();
                jpg.Frames.Add(BitmapFrame.Create(renderTarget));

                byte[] image;
                using (MemoryStream outStream = new MemoryStream())
                {
                    jpg.Save(outStream);
                    image = outStream.ToArray();
                }

                using (FileStream f = new FileStream(@"C:\\temp\\frame.jpg", FileMode.Create, FileAccess.ReadWrite))
                {
                    BinaryWriter bw = new BinaryWriter(f);
                    bw.Write(image);
                    bw.Close();
                }
            }
        }

        private void TriggerButtonClick(object sender, RoutedEventArgs e)
        {
            TriggerButtonState = ButtonState.Cord1;
            SetStopButtonState = ButtonState.Unknown;
        }

        private void SetStopClick(object sender, RoutedEventArgs e)
        {
            TriggerButtonState = ButtonState.Unknown;
            SetStopButtonState = ButtonState.Cord1;
        }

        private void Play_Click(object sender, RoutedEventArgs e)
        {
            RecordingMediaElement.Play();
        }

        private void Load_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();

            // Set filter for file extension and default file extension 
            dlg.DefaultExt = ".MP4";
            dlg.Filter = "MP4 Files(*.MP4)|*.MP4";

            // Display OpenFileDialog by calling ShowDialog method 
            Nullable<bool> result = dlg.ShowDialog();

            // Get the selected file name and display in a TextBox 
            if (result == true)
            {
                // Open document 
                string filename = dlg.FileName;

                var videoPath = Directory.GetCurrentDirectory();
                RecordingMediaElement.Source = new Uri(filename, UriKind.Absolute);
                RecordingMediaElement.Play();
            }
        }
    }
}