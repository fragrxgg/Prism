using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;  // Canvas, Polyline
using System.Windows.Media;     // Colors, SolidColorBrush, ColorConverter, PixelFormats
using System.Windows.Media.Imaging; // BitmapSource, FormatConvertedBitmap
using System.Windows;           // Point
using System.Windows.Shapes;   // Polyline

namespace histmaker
{
    public class HistogramMaker
    {
        public static int[][] ComputeHistogram(BitmapSource bitmap)
        {
            // [0]=R, [1]=G, [2]=B, ognuno con 256 valori
            int[][] hist = new int[3][];
            hist[0] = new int[256];
            hist[1] = new int[256];
            hist[2] = new int[256];

            // Converti in Rgb24 se necessario
            FormatConvertedBitmap converted = new FormatConvertedBitmap(bitmap, PixelFormats.Rgb24, null, 0);

            int stride = converted.PixelWidth * 3;
            byte[] pixels = new byte[stride * converted.PixelHeight];
            converted.CopyPixels(pixels, stride, 0);

            for (int i = 0; i < pixels.Length; i += 3)
            {
                hist[0][pixels[i]]++;      // R
                hist[1][pixels[i + 1]]++; // G
                hist[2][pixels[i + 2]]++; // B
            }

            return hist;
        }

        public static Canvas DrawHistogram(int[][] hist, Canvas canvas)
        {
            canvas.Children.Clear();

            string[] colors = { "#CC3333", "#33CC33", "#3399CC" };

            for (int ch = 0; ch < 3; ch++)
            {
                int max = 1;
                for (int i = 0; i < 256; i++)
                    if (hist[ch][i] > max) max = hist[ch][i];

                Polyline line = new Polyline();
                line.Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colors[ch]));
                line.StrokeThickness = 1;
                line.Opacity = 0.7;

                for (int i = 0; i < 256; i++)
                {
                    double x = i * (canvas.Width / 256.0);
                    double y = canvas.Height - (hist[ch][i] / (double)max) * canvas.Height;
                    line.Points.Add(new Point(x, y));
                }

                canvas.Children.Add(line);
            }
            return canvas;
        }
    }
}
