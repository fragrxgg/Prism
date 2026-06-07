using cubelab;
using histmaker;
using ImageMagick;
using Microsoft.Win32;
using Prism;
using System.Drawing;
using System.IO;
using System.Linq.Expressions;
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
using System.Windows.Threading;

namespace Prism
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MahApps.Metro.Controls.MetroWindow
    {
        public MainWindow()
        {
            try
            {
                InitializeComponent();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error initializing application: " + ex.Message);
            }
        }

        private RAWLab rawLab;

        internal RAWLab RawLab { get => rawLab; set => rawLab = value; }

        public string LutFilePath { get; set; } = "";



        public async void onRAWOpen(object sender, RoutedEventArgs e)
        {
            OpenFileDialog rawFileDialog = new OpenFileDialog();
            rawFileDialog.Filter = "RAW files|*.raw;*.cr2;*.nef;*.arw;*.dng;*.orf;*.rw2;*.pef;*.raf;*.sr2;*.srw;*.x3f;*.bay;*.crw;*.erf;*.kdc;*.mrw;*.mef;*.mos;*.r3d;*.fff|All files|*.*";

            if (rawFileDialog.ShowDialog() == true)
            {
                LoadingPopup loadingPopup = new LoadingPopup();
                loadingPopup.Owner = this;
                loadingPopup.Show(); // ← non bloccante

                try
                {
                    string filePath = rawFileDialog.FileName;

                    RAWLab.ImageData imageData = default;
                    BitmapSource bitmap = await Task.Run(() =>
                    {
                        RawLab = new RAWLab();
                        RawLab.Open(filePath);
                        imageData = RawLab.GetImageData();
                        BitmapSource bmp = RawLab.GetPreviewBitmap();
                        bmp.Freeze();

                        return bmp;
                    });

                    setImageData(imageData);
                    PreviewImage.Source = bitmap;
                    drawHistogram(bitmap);

                    ImageScrollViewer.Visibility = Visibility.Visible;
                    DropZone.Visibility = Visibility.Collapsed;


                }
                finally
                {
                    loadingPopup.Close();
                    setZoom(0.5);
                }
            }
        }


        private async void onJPGOpen(object sender, RoutedEventArgs e)
        {
            OpenFileDialog rawFileDialog = new OpenFileDialog();
            rawFileDialog.Filter = "JPEG file|*.jpg;*.jpeg;*.jpe;*.jfif|All files|*.*";

            if (rawFileDialog.ShowDialog() == true)
            {
                LoadingPopup loadingPopup = new LoadingPopup();
                loadingPopup.Owner = this;
                loadingPopup.Show(); // ← non bloccante

                try
                {
                    string filePath = rawFileDialog.FileName;
                    //BitmapSource bmp = new BitmapImage(new Uri(filePath));
                    BitmapSource bitmap = await Task.Run(() =>
                    {
                        BitmapSource bmp = new BitmapImage(new Uri(filePath));
                        bmp.Freeze();

                        return bmp;
                    });

                    //setImageData(imageData);
                    PreviewImage.Source = bitmap;
                    drawHistogram(bitmap);

                    ImageScrollViewer.Visibility = Visibility.Visible;
                    DropZone.Visibility = Visibility.Collapsed;


                }
                finally
                {
                    loadingPopup.Close();
                    setZoom(0.5);
                }
            }
        }

        private void setImageData(RAWLab.ImageData imageData)
        {
            CameraModel.Text = imageData.Camera;
            FileDetails.Text = imageData.Lens + " | " +
                "ISO " + imageData.Iso + " | " +
                imageData.Shutter + "s | " +
                "f/" + imageData.Aperture + " | " +
                imageData.FocalLength + "mm";
        }

        private void PreviewImage_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                e.Handled = true;

                double zoomFactor = 0.1;

                if (e.Delta > 0)
                {
                    ImageScale.ScaleX += zoomFactor;
                    ImageScale.ScaleY += zoomFactor;
                }
                else
                {
                    ImageScale.ScaleX -= zoomFactor;
                    ImageScale.ScaleY -= zoomFactor;
                }

                // Limiti min/max per evitare problemi
                ImageScale.ScaleX = Math.Max(0.1, Math.Min(5.0, ImageScale.ScaleX));
                ImageScale.ScaleY = Math.Max(0.1, Math.Min(5.0, ImageScale.ScaleY));

                ZoomText.Text = $"Zoom: {(int)(ImageScale.ScaleX * 100)}%";
            }
        }

        private void setZoom(double scale)
        {
            // Limiti di sicurezza (fondamentali)
            scale = Math.Max(0.1, Math.Min(10.0, scale));

            ImageScale.ScaleX = scale;
            ImageScale.ScaleY = scale;
           

            ZoomText.Text = $"Zoom: {(int)(scale * 100)}%";
        }

        /**
         * LUT SECTION
         *
         */

        public static string[] LutList = new string[0];
        private void ComboBox_DropDownOpened(object sender, EventArgs e)
        {

            Mouse.OverrideCursor = Cursors.Wait;
            ProcessDirectory(@".\cube");
            LutBox.ItemsSource = LutList;
            Mouse.OverrideCursor = null;

        }

        public static void ProcessDirectory(string targetDirectory)
        {
            // Process the list of files found in the directory.
            string[] fileEntries = Directory.GetFiles(targetDirectory);
            foreach (string fileName in fileEntries)
                ProcessFile(fileName);

            // Recurse into subdirectories of this directory.
            string[] subdirectoryEntries = Directory.GetDirectories(targetDirectory);
            foreach (string subdirectory in subdirectoryEntries)
                ProcessDirectory(subdirectory);
        }

        public static void ProcessFile(string path)
        {
            path = path.Remove(0, 6);
            LutList = LutList.Append(path).ToArray();
        }

        private async void LutBox_DropDownClosed(object sender, EventArgs e)
        {

            Mouse.OverrideCursor = Cursors.Wait;
            //PreviewImage.Source = null;
            if(LutBox.ItemsSource == null)
            {
                return;
            }

            if(LutBox.SelectedItem == null)
            {
                return;
            }

            LutFilePath = @".\cube\" + LutBox.SelectedItem.ToString();

            BitmapSource bitmap = await Task.Run(() =>
            {
                try
                {
                    CUBELab.LoadCubeLut(LutFilePath);
                }
                catch(Exception e)
                {
                    MessageBox.Show(e.Message);
                }
                    BitmapSource bmp = RawLab.GetPreviewBitmap();
                    bmp.Freeze(); // needed because we are passing it to the UI thread
                return bmp;
            });

            PreviewImage.Source = bitmap;
            drawHistogram(bitmap);
            Mouse.OverrideCursor = null;
        }

        private CancellationTokenSource _exposureCts;
        private CancellationTokenSource _contrastCts;
        private CancellationTokenSource _highLightsCts;
        private CancellationTokenSource _shadowsCts;

        private async void ExposureSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _exposureCts?.Cancel();
            _exposureCts = new CancellationTokenSource();
            var token = _exposureCts.Token;

            try
            {
                await Task.Delay(150, token);

                if (token.IsCancellationRequested) return;

                RawLab.SetExposure((float)-e.NewValue);

                // Call only once GetPreviewBitmap() after the user stops moving the slider
                refreshBitmap();
            }
            catch (TaskCanceledException)
            {
                // Don't do anything, it's normal: the slider was moved again, ignore this update
            }
        }

        private async void ContrastSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _contrastCts?.Cancel();
            _contrastCts = new CancellationTokenSource();
            var token = _contrastCts.Token;

            try
            {
                await Task.Delay(150, token);

                if (token.IsCancellationRequested) return;

                RawLab.SetContrast((float)-e.NewValue);

                // Chiama GetPreviewBitmap() UNA sola volta
                refreshBitmap();
            }
            catch (TaskCanceledException)
            {
                // Don't do anything, it's normal: the slider was moved again, ignore this update
            }
        }

        private async void HighlightsSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _highLightsCts?.Cancel();
            _highLightsCts = new CancellationTokenSource();
            var token = _highLightsCts.Token;

            try
            {
                await Task.Delay(150, token);

                if (token.IsCancellationRequested) return;

                RawLab.SetLights((float)-e.NewValue);

                // Only once GetPreviewBitmap() after the user stops moving the slider
                refreshBitmap();
            }
            catch (TaskCanceledException)
            {
                // Don't do anything, it's normal: the slider was moved again, ignore this update
            }
        }

        private async void ShadowsSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _shadowsCts?.Cancel();
            _shadowsCts = new CancellationTokenSource();
            var token = _shadowsCts.Token;

            try
            {
                await Task.Delay(150, token);

                if (token.IsCancellationRequested) return;

                RawLab.SetShadows((float)-e.NewValue);

                // Only once GetPreviewBitmap() after the user stops moving the slider
                refreshBitmap();
            }
            catch (TaskCanceledException)
            {
                // Don't do anything, it's normal: the slider was moved again, ignore this update
            }
        }

        private void refreshBitmap()
        {
            var bitmap = RawLab.GetPreviewBitmap();
            PreviewImage.Source = bitmap;
            drawHistogram(bitmap);
        }


        private void onHistogramOpen(object sender, RoutedEventArgs e)
        {
            try
            {
                HistogramWindow histogramWindow = new HistogramWindow();
                int[][] histo = HistogramMaker.ComputeHistogram(RawLab.GetPreviewBitmap());
                HistogramMaker.DrawHistogram(histo, histogramWindow.HistoCanvas);
                histogramWindow.Owner = this;
                histogramWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error opening histogram: " + ex.Message);
            }
        }

        private void onImageAlign(object sender, RoutedEventArgs e)
        {
            ImgAlign imgAlignFrm = new ImgAlign();
            imgAlignFrm.Owner = this;
            imgAlignFrm.ShowDialog();
        }

        private CancellationTokenSource _cts;

        private async void LutIntensitySlider_ValueChanged_1(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (RawLab == null) return;

            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            try
            {
                // debounce: aspetta che l'utente smetta di muovere lo slider
                await Task.Delay(150, token);

                float value = (float)e.NewValue;

                var bitmap = await Task.Run(() =>
                {
                    RawLab.setLutOpacity(value);
                    return RawLab.GetPreviewBitmap();
                }, token);

                if (!token.IsCancellationRequested)
                {
                    PreviewImage.Source = bitmap;
                }
            }
            catch (TaskCanceledException)
            {
            }
        }

        private void on100Zoom(object sender, RoutedEventArgs e)
        {
            setZoom(1.0);
        }

        private void onAdaptZoom(object sender, RoutedEventArgs e)
        {
            adaptZoom();
        }

        private void adaptZoom()
        {
            double zoomX = ImageScrollViewer.ActualWidth / PreviewImage.ActualWidth;
            double zoomY = ImageScrollViewer.ActualHeight / PreviewImage.ActualHeight;
            double zoom = Math.Min(zoomX, zoomY);

            setZoom(zoom);
        }

        private async void drawHistogram(BitmapSource img)
        {
            // Extract pixels on the UI thread (where img is accessible)
            int width = img.PixelWidth;
            int height = img.PixelHeight;
            int stride = width * 3;
            byte[] pixels = new byte[stride * height];

            var converted = new FormatConvertedBitmap(img, PixelFormats.Rgb24, null, 0);
            converted.CopyPixels(pixels, stride, 0);

            // Calculate histogram on background thread (only byte array, no WPF objects)
            int[][] hist = await Task.Run(() => HistogramMaker.ComputeHistogramFromPixels(pixels));

            // Draw on UI Thread
            HistogramMaker.DrawHistogram(hist, HistogramCanvas);
        }

        private void onExportClick(object sender, RoutedEventArgs e)
        {

            ExportJPG exportWindow = new ExportJPG();
            exportWindow.ShowDialog();

            int quality = exportWindow.quality;
            int dpi = exportWindow.dpi;
            string exportFolder = exportWindow.exportFolder;

            if (!string.IsNullOrEmpty(exportFolder))
            {
                Mouse.OverrideCursor = Cursors.Wait;
                RawLab.Export(exportFolder, quality, dpi);
                Mouse.OverrideCursor = null;
            }

        }

        private async void onLevelsClick(object sender, RoutedEventArgs e)
        {
            Levels levelsWindow = new Levels(this);

            int[][] hist = await Task.Run(() =>
            {
                BitmapSource bmp = RawLab.GetPreviewBitmap();
                return HistogramMaker.ComputeHistogram(bmp);
            });

            // Disegna solo dopo che la finestra è stata renderizzata
            levelsWindow.Loaded += (s, args) =>
            {
                HistogramMaker.DrawHistogram(hist, levelsWindow.miniCanvas);
            };

            levelsWindow.ShowDialog(); 
            return;

        }

        public async void RefreshPreview()
        {
            var bitmap = await Task.Run(() =>
            {
                var bmp = RawLab.GetPreviewBitmap();
                bmp.Freeze();
                return bmp;
            });

            PreviewImage.Source = bitmap;
        }

        private void StackPanel_Unloaded(object sender, RoutedEventArgs e)
        {
            base.OnClosed(e);
            RawLab?.Close();
        }

        private void ToggleButton_Checked(object sender, RoutedEventArgs e)
        {
            RawLab.setCameraWB(true);
            TempSlider.IsEnabled = false;
            TempSlider.Value = 0;
        }

        private void ToggleButton_Unchecked(object sender, RoutedEventArgs e)
        {
            RawLab.setCameraWB(false);
            TempSlider.IsEnabled = true;
        }

        private void ToggleButton_Checked_1(object sender, RoutedEventArgs e)
        {
            RawLab.setAutoWB(true);
            TempSlider.IsEnabled = false;
            TempSlider.Value = 0;
        }

        private void ToggleButton_Unchecked_1(object sender, RoutedEventArgs e)
        {
            RawLab.setAutoWB(false);
            TempSlider.IsEnabled = true;
        }

        private void onResizingClick(object sender, RoutedEventArgs e)
        {

        }


        /** Cropping*/

        private void onCropImg(object sender, RoutedEventArgs e)
        {
            _cropMode = !_cropMode;
            CropCanvas.Visibility = _cropMode ? Visibility.Visible : Visibility.Collapsed;
            ImageScrollViewer.Cursor = _cropMode ? Cursors.Cross : Cursors.Arrow;
        }



        private bool _isCropping = false;
        private bool _cropMode = false;
        private System.Windows.Point _cropStart;
        private System.Windows.Shapes.Rectangle _cropRect;


        private void CropCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!_cropMode) return;
            _isCropping = true;
            _cropStart = e.GetPosition(CropCanvas);

            if (_cropRect != null) CropCanvas.Children.Remove(_cropRect);

            _cropRect = new System.Windows.Shapes.Rectangle
            {
                Stroke = System.Windows.Media.Brushes.White,
                StrokeThickness = 1.5,
                StrokeDashArray = new DoubleCollection { 4, 2 },
                Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(40, 255, 255, 255))
            };

            Canvas.SetLeft(_cropRect, _cropStart.X);
            Canvas.SetTop(_cropRect, _cropStart.Y);
            CropCanvas.Children.Add(_cropRect);
            CropCanvas.CaptureMouse();
        }

        private void CropCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isCropping || _cropRect == null) return;

            var pos = e.GetPosition(CropCanvas);
            double x = Math.Min(pos.X, _cropStart.X);
            double y = Math.Min(pos.Y, _cropStart.Y);
            double w = Math.Abs(pos.X - _cropStart.X);
            double h = Math.Abs(pos.Y - _cropStart.Y);

            Canvas.SetLeft(_cropRect, x);
            Canvas.SetTop(_cropRect, y);
            _cropRect.Width = w;
            _cropRect.Height = h;

            // Calculate real crop size in pixels based on the current zoom level and image size
            var src = PreviewImage.Source as BitmapSource;
            if (src != null && w > 0 && h > 0)
            {
                double scaleX = src.PixelWidth / (PreviewImage.ActualWidth * ImageScale.ScaleX);
                double scaleY = src.PixelHeight / (PreviewImage.ActualHeight * ImageScale.ScaleY);

                int cropW = (int)(w * scaleX);
                int cropH = (int)(h * scaleY);

                CropToastText.Text = $"{cropW} × {cropH}  ({src.PixelWidth} × {src.PixelHeight})";

                // Position the toast near the cursor, but with a small offset
                Canvas.SetLeft(CropToast, pos.X + 14);
                Canvas.SetTop(CropToast, pos.Y + 14);

                CropToast.Visibility = Visibility.Visible;
            }
        }

        private void CropCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isCropping) return;
            _isCropping = false;
            CropCanvas.ReleaseMouseCapture();
            ApplyCrop();
            CropToast.Visibility = Visibility.Collapsed;
        }

        private void ApplyCrop()
        {
            if (_cropRect == null || _cropRect.Width < 5 || _cropRect.Height < 5) return;

            var src = PreviewImage.Source as BitmapSource;
            if (src == null) return;

            // Canvas's coordinates correspond to the scaled image, so we need to convert them back to real pixels
            // We have to consider both the zoom level and the actual size of the image in the UI

            double scaleX = src.PixelWidth / (PreviewImage.ActualWidth * ImageScale.ScaleX);
            double scaleY = src.PixelHeight / (PreviewImage.ActualHeight * ImageScale.ScaleY);

            int px = (int)(Canvas.GetLeft(_cropRect) * scaleX);
            int py = (int)(Canvas.GetTop(_cropRect) * scaleY);
            int pw = (int)(_cropRect.Width * scaleX);
            int ph = (int)(_cropRect.Height * scaleY);

            px = Math.Clamp(px, 0, src.PixelWidth - 1);
            py = Math.Clamp(py, 0, src.PixelHeight - 1);
            pw = Math.Clamp(pw, 1, src.PixelWidth - px);
            ph = Math.Clamp(ph, 1, src.PixelHeight - py);

            var cropped = new CroppedBitmap(src, new Int32Rect(px, py, pw, ph));
            PreviewImage.Source = cropped;
            drawHistogram(cropped);

            // Reset
            CropCanvas.Children.Remove(_cropRect);
            _cropRect = null;
            _cropMode = false;
            CropCanvas.Visibility = Visibility.Collapsed;
            ImageScrollViewer.Cursor = Cursors.Arrow;
        }

        private void onCloseApp(object sender, RoutedEventArgs e)
        {
            Close();
        }



        /** Manage Metadata files saving **/

        //private void onSaveClick()
        //{
        //    SaveFileDialog saveFileDialog = new SaveFileDialog();
        //    saveFileDialog.Filter = "Wakame Project|*.wakememeta";
        //    if (saveFileDialog.ShowDialog() == true)
        //    {
        //        string filePath = saveFileDialog.FileName;
        //        //RawLab.SaveMetadata(filePath);
        //    }
        //}
    }
}