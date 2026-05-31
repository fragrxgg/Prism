using System;
using System.IO;
using System.Threading.Tasks;

namespace cubelab
{
    public static class CUBELab
    {
        private static float[] _lut = new float[0];
        private static int _lutSize = 0;
        private static int _lutSize2 = 0;

        public static bool HasLut => _lutSize > 0;

        public static void LoadCubeLut(string cubePath)
        {
            int size = 0;
            float[] lut = null;
            int index = 0;

            foreach (string rawLine in File.ReadLines(cubePath))
            {
                string line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line[0] == '#') continue;

                if (line.StartsWith("LUT_3D_SIZE", StringComparison.OrdinalIgnoreCase))
                {
                    size = int.Parse(line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[1]);
                    lut = new float[size * size * size * 3];
                    continue;
                }

                if (char.IsLetter(line[0])) continue;

                if (lut != null)
                {
                    string[] parts = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 3) continue;

                    float r, g, b;
                    if (!float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out r)) continue;
                    if (!float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out g)) continue;
                    if (!float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out b)) continue;

                    int ri = index % size;
                    int gi = (index / size) % size;
                    int bi = index / (size * size);

                    int flatIdx = (bi * size * size + gi * size + ri) * 3;
                    lut[flatIdx] = r;
                    lut[flatIdx + 1] = g;
                    lut[flatIdx + 2] = b;
                    index++;
                }
            }

            if (lut == null) throw new InvalidDataException("LUT 3D non trovata nel file .cube.");
            _lut = lut;
            _lutSize = size;
            _lutSize2 = size * size;
        }

        public static void ClearLut()
        {
            _lut = new float[0];
            _lutSize = 0;
            _lutSize2 = 0;
        }

        public static void ApplyLutToPixels(byte[] pixels, float opacity = 1.0f)
        {
            if (!HasLut || opacity <= 0f) return;

            float scale = _lutSize - 1f;
            float[] lut = _lut;
            int size = _lutSize;
            int size2 = _lutSize2;
            bool fullBlend = opacity >= 1f;

            const int chunkPixels = 16384;
            int pixelCount = pixels.Length / 3;
            int chunkCount = (pixelCount + chunkPixels - 1) / chunkPixels;

            Parallel.For(0, chunkCount, chunk =>
            {
                int pixStart = chunk * chunkPixels;
                int pixEnd = Math.Min(pixStart + chunkPixels, pixelCount);

                for (int px = pixStart; px < pixEnd; px++)
                {
                    int i = px * 3;

                    float rF = pixels[i] / 255f * scale;
                    float gF = pixels[i + 1] / 255f * scale;
                    float bF = pixels[i + 2] / 255f * scale;

                    int r0 = (int)rF, r1 = Math.Min(r0 + 1, size - 1);
                    int g0 = (int)gF, g1 = Math.Min(g0 + 1, size - 1);
                    int b0 = (int)bF, b1 = Math.Min(b0 + 1, size - 1);

                    float rf = rF - r0;
                    float gf = gF - g0;
                    float bf = bF - b0;

                    int i000 = (b0 * size2 + g0 * size + r0) * 3;
                    int i100 = (b0 * size2 + g0 * size + r1) * 3;
                    int i010 = (b0 * size2 + g1 * size + r0) * 3;
                    int i110 = (b0 * size2 + g1 * size + r1) * 3;
                    int i001 = (b1 * size2 + g0 * size + r0) * 3;
                    int i101 = (b1 * size2 + g0 * size + r1) * 3;
                    int i011 = (b1 * size2 + g1 * size + r0) * 3;
                    int i111 = (b1 * size2 + g1 * size + r1) * 3;

                    float outR = TrilinearInline(
                        lut[i000], lut[i100], lut[i010], lut[i110],
                        lut[i001], lut[i101], lut[i011], lut[i111],
                        rf, gf, bf) * 255f;

                    float outG = TrilinearInline(
                        lut[i000 + 1], lut[i100 + 1], lut[i010 + 1], lut[i110 + 1],
                        lut[i001 + 1], lut[i101 + 1], lut[i011 + 1], lut[i111 + 1],
                        rf, gf, bf) * 255f;

                    float outB = TrilinearInline(
                        lut[i000 + 2], lut[i100 + 2], lut[i010 + 2], lut[i110 + 2],
                        lut[i001 + 2], lut[i101 + 2], lut[i011 + 2], lut[i111 + 2],
                        rf, gf, bf) * 255f;

                    if (fullBlend)
                    {
                        pixels[i] = ClampToByte((int)(outR + 0.5f));
                        pixels[i + 1] = ClampToByte((int)(outG + 0.5f));
                        pixels[i + 2] = ClampToByte((int)(outB + 0.5f));
                    }
                    else
                    {
                        float origR = pixels[i];
                        float origG = pixels[i + 1];
                        float origB = pixels[i + 2];
                        pixels[i] = ClampToByte((int)(origR + (outR - origR) * opacity + 0.5f));
                        pixels[i + 1] = ClampToByte((int)(origG + (outG - origG) * opacity + 0.5f));
                        pixels[i + 2] = ClampToByte((int)(origB + (outB - origB) * opacity + 0.5f));
                    }
                }
            });
        }

        private static byte ClampToByte(int v)
        {
            if (v < 0) return 0;
            if (v > 255) return 255;
            return (byte)v;
        }

        private static float TrilinearInline(
            float c000, float c100, float c010, float c110,
            float c001, float c101, float c011, float c111,
            float rf, float gf, float bf)
        {
            float c00 = c000 + (c100 - c000) * rf;
            float c01 = c001 + (c101 - c001) * rf;
            float c10 = c010 + (c110 - c010) * rf;
            float c11 = c011 + (c111 - c011) * rf;
            float c0 = c00 + (c10 - c00) * gf;
            float c1 = c01 + (c11 - c01) * gf;
            return c0 + (c1 - c0) * bf;
        }
    }
}