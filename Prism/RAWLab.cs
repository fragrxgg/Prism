/**
 * This class encapsulates RAW image processing using LibRaw
 * */

using Sdcb.LibRaw;
using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using cubelab;
using System.Windows;

namespace Prism
{
    internal class RAWLab
    {
        private byte[] _basePixels = null;
        private byte[] _workingPixels = null;
        private int _cachedWidth;
        private int _cachedHeight;


        // Combined LUT exposure+levels, invalidated when parameters change
        private byte[] _combinedLut = null;
        private bool _lutDirty = true;

        public RawContext? RawContext;

        public float Exposure = 0f;
        public float Contrast = 0;
        public float Temperature = 5500;
        public float Saturation = 0;
        public string FilePath = "";
        public int UserSat = 0;
        public float Straighten = 0f;
        private float lutOpacity = 1.0f;

        private bool autoWB = false;
        private bool cameraWB = false;

        public byte InputBlack = 0;
        public byte InputWhite = 255;
        public float InputGamma = 1.0f;
        public byte OutputBlack = 0;
        public byte OutputWhite = 255;

        public struct ImageData
        {
            public string Camera;
            public string Lens;
            public float Iso;
            public float Shutter;
            public float Aperture;
            public float FocalLength;
        }

        // ── Invalidazione LUT ────────────────────────────────────────────────

        private void MarkLutDirty() => _lutDirty = true;

        public void SetExposure(float value)
        {
            Exposure = -value;
            MarkLutDirty();
        }

        public void SetInputBlack(byte v) { InputBlack = v; MarkLutDirty(); }
        public void SetInputWhite(byte v) { InputWhite = v; MarkLutDirty(); }
        public void SetInputGamma(float v) { InputGamma = v; MarkLutDirty(); }
        public void SetOutputBlack(byte v) { OutputBlack = v; MarkLutDirty(); }
        public void SetOutputWhite(byte v) { OutputWhite = v; MarkLutDirty(); }

        public void setLutOpacity(float opacity)
        {
            lutOpacity = opacity / 100f;
            // opacity non tocca la LUT tonal, non serve MarkLutDirty
        }

        // ── LUT combinata exposure + levels ──────────────────────────────────

        /// <summary>
        /// Ricalcola la LUT 256-entry solo quando i parametri sono cambiati.
        /// Una singola Pow per entry invece di una per pixel.
        /// </summary>
        private void EnsureCombinedLut()
        {
            if (!_lutDirty && _combinedLut != null) return;

            _combinedLut ??= new byte[256];

            float exposureFactor = MathF.Pow(2f, Exposure);

            bool levelsAreIdentity =
                InputBlack == 0 && InputWhite == 255 &&
                InputGamma == 1.0f &&
                OutputBlack == 0 && OutputWhite == 255;

            if (levelsAreIdentity)
            {
                // Fast path: solo exposure
                for (int i = 0; i < 256; i++)
                {
                    int v = (int)(i * exposureFactor);
                    _combinedLut[i] = (byte)Math.Clamp(v, 0, 255);
                }
            }
            else if (InputBlack < InputWhite && OutputBlack < OutputWhite)
            {
                float inRange = InputWhite - InputBlack;
                float outRange = OutputWhite - OutputBlack;
                float invGamma = 1f / InputGamma;

                for (int i = 0; i < 256; i++)
                {
                    // 1. Exposure
                    float v = Math.Clamp(i * exposureFactor, 0f, 255f);

                    // 2. Levels input clamp + normalize
                    float normalized = Math.Clamp((v - InputBlack) / inRange, 0f, 1f);

                    // 3. Gamma
                    float gamma = MathF.Pow(normalized, invGamma);

                    // 4. Output range
                    _combinedLut[i] = (byte)Math.Clamp((int)(OutputBlack + gamma * outRange), 0, 255);
                }
            }
            else
            {
                // Valori inconsistenti: solo exposure, levels saltati
                for (int i = 0; i < 256; i++)
                {
                    int v = (int)(i * exposureFactor);
                    _combinedLut[i] = (byte)Math.Clamp(v, 0, 255);
                }
            }

            _lutDirty = false;
        }

        /// <summary>
        /// Applica la LUT combinata in un unico loop parallelo per chunk.
        /// </summary>
        private void ApplyCombinedLut(byte[] pixels)
        {
            EnsureCombinedLut();
            byte[] lut = _combinedLut;

            // Parallelizza su blocchi da 64 KB per minimizzare overhead
            const int chunkSize = 65536;
            int chunkCount = (pixels.Length + chunkSize - 1) / chunkSize;

            Parallel.For(0, chunkCount, chunk =>
            {
                int start = chunk * chunkSize;
                int end = Math.Min(start + chunkSize, pixels.Length);
                for (int i = start; i < end; i++)
                    pixels[i] = lut[pixels[i]];
            });
        }

        // ── Open / WB ────────────────────────────────────────────────────────

        public void Open(string path)
        {
            RawContext?.Dispose();
            RawContext = RawContext.OpenFile(path);
            RawContext.Unpack();
            FilePath = path;
            _basePixels = null;
            MarkLutDirty();
        }

        public void setUserSaturation(int value) { UserSat = value; }

        public void setAutoWB(bool enabled)
        {
            autoWB = enabled;
            cameraWB = !enabled;
            _basePixels = null; // WB cambia il decode RAW
        }

        public void setCameraWB(bool enabled)
        {
            cameraWB = enabled;
            autoWB = !enabled;
            _basePixels = null;
        }

        // ── Metadata ─────────────────────────────────────────────────────────

        public ImageData GetImageData()
        {
            if (RawContext == null)
                throw new InvalidOperationException("Nessun file RAW aperto.");

            var p = RawContext.ImageParams;
            var o = RawContext.ImageOtherParams;

            return new ImageData
            {
                Camera = $"{p.Make} {p.Model}",
                Lens = RawContext.LensInfo.Lens,
                Iso = o.IsoSpeed,
                Shutter = o.Shutter,
                Aperture = o.Aperture,
                FocalLength = o.FocalLength
            };
        }

        // ── Preview ──────────────────────────────────────────────────────────

        public BitmapSource GetPreviewBitmap()
        {
            if (RawContext == null)
                throw new InvalidOperationException("Nessun file RAW aperto.");

            // Decode RAW una volta sola; invalida solo se WB o file cambiano
            if (_basePixels == null)
            {
                string path = FilePath;
                RawContext.Dispose();
                RawContext = RawContext.OpenFile(path);
                RawContext.Unpack();

                RawContext.DcrawProcess(c =>
                {
                    c.UseCameraWb = cameraWB;
                    c.UseAutoWb = autoWB;
                    c.HalfSize = true;
                    c.OutputBps = 8;
                    c.Interpolation = true;
                    c.UserSaturation = UserSat != 0 ? UserSat : 0;
                    c.OutputColor = LibRawColorSpace.SRGB;
                });

                using ProcessedImage img = RawContext.MakeDcrawMemoryImage();
                _cachedWidth = img.Width;
                _cachedHeight = img.Height;
                int stride = img.Width * 3;
                _basePixels = new byte[stride * img.Height];
                Marshal.Copy(img.DataPointer, _basePixels, 0, _basePixels.Length);
            }

            // Riusa buffer working senza riallocare
            if (_workingPixels == null || _workingPixels.Length != _basePixels.Length)
                _workingPixels = new byte[_basePixels.Length];

            Buffer.BlockCopy(_basePixels, 0, _workingPixels, 0, _basePixels.Length);

            // Un solo passaggio: exposure + levels via LUT pre-calcolata
            ApplyCombinedLut(_workingPixels);

            // CUBE LUT separata (dipendenza esterna)
            CUBELab.ApplyLutToPixels(_workingPixels, lutOpacity);

            int outStride = _cachedWidth * 3;
            var bitmap = BitmapSource.Create(
                _cachedWidth, _cachedHeight, 96, 96,
                PixelFormats.Rgb24, null, _workingPixels, outStride
            );
            bitmap.Freeze();

            return Math.Abs(Straighten) > 0.01f
                ? RotateBitmapSafe(bitmap, Straighten)
                : bitmap;
        }

        // ── Rotation ─────────────────────────────────────────────────────────

        public BitmapSource RotateBitmapSafe(BitmapSource source, double angle)
        {
            double radians = angle * Math.PI / 180.0;
            double cos = Math.Abs(Math.Cos(radians));
            double sin = Math.Abs(Math.Sin(radians));

            int newWidth = (int)(source.PixelWidth * cos + source.PixelHeight * sin);
            int newHeight = (int)(source.PixelWidth * sin + source.PixelHeight * cos);

            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.PushTransform(new TranslateTransform(newWidth / 2.0, newHeight / 2.0));
                dc.PushTransform(new RotateTransform(angle));
                dc.PushTransform(new TranslateTransform(-source.PixelWidth / 2.0, -source.PixelHeight / 2.0));
                dc.DrawImage(source, new Rect(0, 0, source.PixelWidth, source.PixelHeight));
                dc.Pop(); dc.Pop(); dc.Pop();
            }

            var rtb = new RenderTargetBitmap(newWidth, newHeight, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            return rtb;
        }

        // ── Export ───────────────────────────────────────────────────────────

        public void Export(string outputPath, int quality, int dpi)
        {
            if (RawContext == null)
                throw new InvalidOperationException("Nessun file RAW aperto.");

            RawContext.DcrawProcess(c =>
            {
                c.UseCameraWb = true;
                c.HalfSize = false;
                c.OutputBps = 8;
                c.Interpolation = true;
                c.UserSaturation = UserSat;
                c.OutputColor = LibRawColorSpace.SRGB;
            });

            using ProcessedImage img = RawContext.MakeDcrawMemoryImage();

            int stride = img.Width * 3;
            byte[] exportPx = new byte[stride * img.Height];
            Marshal.Copy(img.DataPointer, exportPx, 0, exportPx.Length);

            ApplyCombinedLut(exportPx);
            CUBELab.ApplyLutToPixels(exportPx, lutOpacity);

            var bitmap = BitmapSource.Create(
                img.Width, img.Height, dpi, dpi,
                PixelFormats.Rgb24, null, exportPx, stride
            );
            bitmap.Freeze();

            using var fileStream = System.IO.File.Create(outputPath);
            var encoder = new JpegBitmapEncoder { QualityLevel = quality };
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            encoder.Save(fileStream);
        }

        // ── Cleanup ──────────────────────────────────────────────────────────

        public void Close()
        {
            RawContext?.Dispose();
            RawContext = null;
            _basePixels = null;
            _workingPixels = null;
            _combinedLut = null;
            _lutDirty = true;
        }
    }
}