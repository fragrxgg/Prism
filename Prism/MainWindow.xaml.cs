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
                    bmp.Freeze(); // obbligatorio per passare la bitmap a un altro thread
                    return bmp;
            });

            PreviewImage.Source = bitmap;
            drawHistogram(bitmap);
            Mouse.OverrideCursor = null;
        }

        private CancellationTokenSource _exposureCts;

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

                // Chiama GetPreviewBitmap() UNA sola volta
                var bitmap = RawLab.GetPreviewBitmap();
                PreviewImage.Source = bitmap;
                drawHistogram(bitmap);
            }
            catch (TaskCanceledException)
            {
                // Normale: lo slider è stato mosso di nuovo, ignora
            }
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
            RawLab.RotateBitmapSafe(RawLab.GetPreviewBitmap(), 30);
            PreviewImage.Source = RawLab.GetPreviewBitmap();
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
            int[][] hist = await Task.Run(() =>
            {
                return HistogramMaker.ComputeHistogram(img);
            });

            HistogramMaker.DrawHistogram(hist, HistogramCanvas);
        }

        private void onExportClick(object sender, RoutedEventArgs e)
        {
            ExportJPG exportWindow = new ExportJPG();
            exportWindow.ShowDialog();
            return;

            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "JPEG Image|*.jpg|PNG Image|*.png|TIFF Image|*.tiff";

            if (saveFileDialog.ShowDialog() == true)
            {
                RawLab.Export(saveFileDialog.FileName, 100, 96);
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