using System;
using System.Collections.Generic;

namespace DRIT.Drawing.Codecs
{
    /// <summary>Palette and per-pixel indices of one GIF frame.</summary>
    internal sealed class GifPalette
    {
        /// <summary>Palette as R, G, B triples, including the transparent entry (black) when there is one.</summary>
        public byte[] Rgb { get; set; } = Array.Empty<byte>();

        /// <summary>Number of palette entries, including the transparent one.</summary>
        public int ColorCount { get; set; }

        /// <summary>Index of the transparent entry, or -1.</summary>
        public int TransparentIndex { get; set; } = -1;

        /// <summary>One palette index per pixel.</summary>
        public byte[] Indices { get; set; } = Array.Empty<byte>();
    }

    /// <summary>
    /// Reduces straight-alpha BGRA pixels to a GIF palette of at most 256 entries. Images that already use few
    /// enough colours keep them exactly (in order of first appearance); richer images are reduced with a
    /// population-weighted median cut. Pixels with alpha below 128 become the transparent entry.
    /// </summary>
    internal static class GifQuantizer
    {
        public static GifPalette Quantize(byte[] bgra, int pixelCount)
        {
            var histogram = new Dictionary<int, int>();
            var firstSeen = new List<int>();
            bool transparent = false;

            for (int i = 0; i < pixelCount; i++)
            {
                int o = i * 4;
                if (bgra[o + 3] < 128)
                {
                    transparent = true;
                    continue;
                }

                int key = (bgra[o + 2] << 16) | (bgra[o + 1] << 8) | bgra[o];
                if (histogram.TryGetValue(key, out int count))
                {
                    histogram[key] = count + 1;
                }
                else
                {
                    histogram[key] = 1;
                    firstSeen.Add(key);
                }
            }

            int maxColors = transparent ? 255 : 256;
            var colourToIndex = new Dictionary<int, int>(histogram.Count);
            var paletteColours = new List<(byte r, byte g, byte b)>();

            if (firstSeen.Count <= maxColors)
            {
                foreach (int key in firstSeen)
                {
                    colourToIndex[key] = paletteColours.Count;
                    paletteColours.Add(((byte)(key >> 16), (byte)(key >> 8), (byte)key));
                }
            }
            else
            {
                MedianCut(firstSeen, histogram, maxColors, colourToIndex, paletteColours);
            }

            // An entirely transparent image still needs one colour to carry the transparent index.
            if (paletteColours.Count == 0)
                paletteColours.Add((0, 0, 0));

            int transparentIndex = -1;
            if (transparent)
            {
                transparentIndex = paletteColours.Count;
                paletteColours.Add((0, 0, 0));
            }

            var indices = new byte[pixelCount];
            int lastKey = -1, lastIndex = 0;
            for (int i = 0; i < pixelCount; i++)
            {
                int o = i * 4;
                if (bgra[o + 3] < 128)
                {
                    indices[i] = (byte)transparentIndex;
                    continue;
                }

                int key = (bgra[o + 2] << 16) | (bgra[o + 1] << 8) | bgra[o];
                if (key != lastKey)
                {
                    lastKey = key;
                    lastIndex = colourToIndex[key];
                }
                indices[i] = (byte)lastIndex;
            }

            var rgb = new byte[paletteColours.Count * 3];
            for (int i = 0; i < paletteColours.Count; i++)
            {
                rgb[i * 3] = paletteColours[i].r;
                rgb[i * 3 + 1] = paletteColours[i].g;
                rgb[i * 3 + 2] = paletteColours[i].b;
            }

            return new GifPalette
            {
                Rgb = rgb,
                ColorCount = paletteColours.Count,
                TransparentIndex = transparentIndex,
                Indices = indices,
            };
        }

        private static int Channel(int key, int channel) => (key >> (16 - 8 * channel)) & 0xFF;

        private static void MedianCut(
            List<int> colours,
            Dictionary<int, int> histogram,
            int boxCount,
            Dictionary<int, int> colourToIndex,
            List<(byte r, byte g, byte b)> palette)
        {
            int[] keys = colours.ToArray();
            var order = new int[keys.Length];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            var weights = new long[keys.Length];
            for (int i = 0; i < keys.Length; i++) weights[i] = histogram[keys[i]];

            var boxes = new List<(int start, int end)> { (0, keys.Length) };
            while (boxes.Count < boxCount)
            {
                // Split the box holding the most distinct colours; stop when every box is a single colour.
                int pick = -1, best = 1;
                for (int b = 0; b < boxes.Count; b++)
                {
                    int size = boxes[b].end - boxes[b].start;
                    if (size > best) { best = size; pick = b; }
                }
                if (pick < 0)
                    break;

                (int start, int end) = boxes[pick];
                int min0 = 255, min1 = 255, min2 = 255, max0 = 0, max1 = 0, max2 = 0;
                for (int i = start; i < end; i++)
                {
                    int key = keys[order[i]];
                    int c0 = Channel(key, 0), c1 = Channel(key, 1), c2 = Channel(key, 2);
                    if (c0 < min0) min0 = c0; if (c0 > max0) max0 = c0;
                    if (c1 < min1) min1 = c1; if (c1 > max1) max1 = c1;
                    if (c2 < min2) min2 = c2; if (c2 > max2) max2 = c2;
                }

                int channel = (max0 - min0) >= (max1 - min1) && (max0 - min0) >= (max2 - min2) ? 0
                            : (max1 - min1) >= (max2 - min2) ? 1 : 2;
                int ch = channel;
                Array.Sort(order, start, end - start, Comparer<int>.Create((x, y) => Channel(keys[x], ch).CompareTo(Channel(keys[y], ch))));

                long total = 0;
                for (int i = start; i < end; i++) total += weights[order[i]];
                long running = 0;
                int split = start + 1;
                for (int i = start; i < end - 1; i++)
                {
                    running += weights[order[i]];
                    split = i + 1;
                    if (running * 2 >= total)
                        break;
                }

                boxes[pick] = (start, split);
                boxes.Add((split, end));
            }

            for (int b = 0; b < boxes.Count; b++)
            {
                long sumR = 0, sumG = 0, sumB = 0, sumW = 0;
                for (int i = boxes[b].start; i < boxes[b].end; i++)
                {
                    int key = keys[order[i]];
                    long w = weights[order[i]];
                    sumR += Channel(key, 0) * w;
                    sumG += Channel(key, 1) * w;
                    sumB += Channel(key, 2) * w;
                    sumW += w;
                }
                palette.Add(((byte)((sumR + sumW / 2) / sumW), (byte)((sumG + sumW / 2) / sumW), (byte)((sumB + sumW / 2) / sumW)));
                for (int i = boxes[b].start; i < boxes[b].end; i++)
                    colourToIndex[keys[order[i]]] = b;
            }
        }
    }
}
