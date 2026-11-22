using System;

namespace DRIT.Drawing.Codecs.Jpeg
{
    internal static class IdctTransform
    {
        // Fixed-point constants of the Loeffler-Ligtenberg-Moschytz IDCT (13 fractional bits), as used by
        // libjpeg's default "islow" implementation, so results match libjpeg to within rounding.
        private const int ConstBits = 13;
        private const int Pass1Bits = 2;
        private const int Fix_0_298631336 = 2446;
        private const int Fix_0_390180644 = 3196;
        private const int Fix_0_541196100 = 4433;
        private const int Fix_0_765366865 = 6270;
        private const int Fix_0_899976223 = 7373;
        private const int Fix_1_175875602 = 9633;
        private const int Fix_1_501321110 = 12299;
        private const int Fix_1_847759065 = 15137;
        private const int Fix_1_961570560 = 16069;
        private const int Fix_2_053119869 = 16819;
        private const int Fix_2_562915447 = 20995;
        private const int Fix_3_072711026 = 25172;

        [ThreadStatic]
        private static int[]? _workspace;

        /// <summary>
        /// Inverse DCT of one 8x8 block of dequantised coefficients (natural row-major order) into level-shifted,
        /// clamped 8-bit samples. Integer arithmetic only: the result is identical on every platform even for
        /// corrupt coefficients (overflow wraps, then the sample is clamped).
        /// </summary>
        public static void TransformBlock(int[] coeffs, byte[] destination)
        {
            if (coeffs == null) throw new ArgumentNullException(nameof(coeffs));
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (coeffs.Length < 64) throw new ArgumentException("IDCT requires 64 coefficients.", nameof(coeffs));
            if (destination.Length < 64) throw new ArgumentException("IDCT destination must contain at least 64 bytes.", nameof(destination));

            unchecked
            {
                int[] ws = _workspace ??= new int[64];

                // Pass 1: columns of the input into the workspace.
                for (int col = 0; col < 8; col++)
                {
                    if (coeffs[col + 8] == 0 && coeffs[col + 16] == 0 && coeffs[col + 24] == 0 && coeffs[col + 32] == 0
                        && coeffs[col + 40] == 0 && coeffs[col + 48] == 0 && coeffs[col + 56] == 0)
                    {
                        int dc = coeffs[col] << Pass1Bits;
                        ws[col] = dc; ws[col + 8] = dc; ws[col + 16] = dc; ws[col + 24] = dc;
                        ws[col + 32] = dc; ws[col + 40] = dc; ws[col + 48] = dc; ws[col + 56] = dc;
                        continue;
                    }

                    int z2 = coeffs[col + 16];
                    int z3 = coeffs[col + 48];
                    int z1 = (z2 + z3) * Fix_0_541196100;
                    int tmp2 = z1 + z3 * -Fix_1_847759065;
                    int tmp3 = z1 + z2 * Fix_0_765366865;

                    z2 = coeffs[col];
                    z3 = coeffs[col + 32];
                    int tmp0 = (z2 + z3) << ConstBits;
                    int tmp1 = (z2 - z3) << ConstBits;

                    int tmp10 = tmp0 + tmp3;
                    int tmp13 = tmp0 - tmp3;
                    int tmp11 = tmp1 + tmp2;
                    int tmp12 = tmp1 - tmp2;

                    tmp0 = coeffs[col + 56];
                    tmp1 = coeffs[col + 40];
                    tmp2 = coeffs[col + 24];
                    tmp3 = coeffs[col + 8];

                    z1 = tmp0 + tmp3;
                    z2 = tmp1 + tmp2;
                    z3 = tmp0 + tmp2;
                    int z4 = tmp1 + tmp3;
                    int z5 = (z3 + z4) * Fix_1_175875602;

                    tmp0 *= Fix_0_298631336;
                    tmp1 *= Fix_2_053119869;
                    tmp2 *= Fix_3_072711026;
                    tmp3 *= Fix_1_501321110;
                    z1 *= -Fix_0_899976223;
                    z2 *= -Fix_2_562915447;
                    z3 *= -Fix_1_961570560;
                    z4 *= -Fix_0_390180644;

                    z3 += z5;
                    z4 += z5;

                    tmp0 += z1 + z3;
                    tmp1 += z2 + z4;
                    tmp2 += z2 + z3;
                    tmp3 += z1 + z4;

                    const int shift = ConstBits - Pass1Bits;
                    const int round = 1 << (shift - 1);
                    ws[col]      = (tmp10 + tmp3 + round) >> shift;
                    ws[col + 56] = (tmp10 - tmp3 + round) >> shift;
                    ws[col + 8]  = (tmp11 + tmp2 + round) >> shift;
                    ws[col + 48] = (tmp11 - tmp2 + round) >> shift;
                    ws[col + 16] = (tmp12 + tmp1 + round) >> shift;
                    ws[col + 40] = (tmp12 - tmp1 + round) >> shift;
                    ws[col + 24] = (tmp13 + tmp0 + round) >> shift;
                    ws[col + 32] = (tmp13 - tmp0 + round) >> shift;
                }

                // Pass 2: rows of the workspace into samples.
                for (int row = 0; row < 8; row++)
                {
                    int o = row * 8;
                    if (ws[o + 1] == 0 && ws[o + 2] == 0 && ws[o + 3] == 0 && ws[o + 4] == 0
                        && ws[o + 5] == 0 && ws[o + 6] == 0 && ws[o + 7] == 0)
                    {
                        byte dc = Clamp(((ws[o] + (1 << (Pass1Bits + 2))) >> (Pass1Bits + 3)) + 128);
                        for (int i = 0; i < 8; i++) destination[o + i] = dc;
                        continue;
                    }

                    int z2 = ws[o + 2];
                    int z3 = ws[o + 6];
                    int z1 = (z2 + z3) * Fix_0_541196100;
                    int tmp2 = z1 + z3 * -Fix_1_847759065;
                    int tmp3 = z1 + z2 * Fix_0_765366865;

                    int tmp0 = (ws[o] + ws[o + 4]) << ConstBits;
                    int tmp1 = (ws[o] - ws[o + 4]) << ConstBits;

                    int tmp10 = tmp0 + tmp3;
                    int tmp13 = tmp0 - tmp3;
                    int tmp11 = tmp1 + tmp2;
                    int tmp12 = tmp1 - tmp2;

                    tmp0 = ws[o + 7];
                    tmp1 = ws[o + 5];
                    tmp2 = ws[o + 3];
                    tmp3 = ws[o + 1];

                    z1 = tmp0 + tmp3;
                    z2 = tmp1 + tmp2;
                    z3 = tmp0 + tmp2;
                    int z4 = tmp1 + tmp3;
                    int z5 = (z3 + z4) * Fix_1_175875602;

                    tmp0 *= Fix_0_298631336;
                    tmp1 *= Fix_2_053119869;
                    tmp2 *= Fix_3_072711026;
                    tmp3 *= Fix_1_501321110;
                    z1 *= -Fix_0_899976223;
                    z2 *= -Fix_2_562915447;
                    z3 *= -Fix_1_961570560;
                    z4 *= -Fix_0_390180644;

                    z3 += z5;
                    z4 += z5;

                    tmp0 += z1 + z3;
                    tmp1 += z2 + z4;
                    tmp2 += z2 + z3;
                    tmp3 += z1 + z4;

                    const int shift = ConstBits + Pass1Bits + 3;
                    const int round = 1 << (shift - 1);
                    destination[o]     = Clamp(((tmp10 + tmp3 + round) >> shift) + 128);
                    destination[o + 7] = Clamp(((tmp10 - tmp3 + round) >> shift) + 128);
                    destination[o + 1] = Clamp(((tmp11 + tmp2 + round) >> shift) + 128);
                    destination[o + 6] = Clamp(((tmp11 - tmp2 + round) >> shift) + 128);
                    destination[o + 2] = Clamp(((tmp12 + tmp1 + round) >> shift) + 128);
                    destination[o + 5] = Clamp(((tmp12 - tmp1 + round) >> shift) + 128);
                    destination[o + 3] = Clamp(((tmp13 + tmp0 + round) >> shift) + 128);
                    destination[o + 4] = Clamp(((tmp13 - tmp0 + round) >> shift) + 128);
                }
            }
        }

        private static byte Clamp(int value) => value < 0 ? (byte)0 : value > 255 ? (byte)255 : (byte)value;

        // ── Reference implementation (tests only) ────────────────────────────────

        private static readonly double InvSqrt2 = 1.0 / Math.Sqrt(2.0);
        private static readonly double[,] Cosine = BuildCosineTable();

        /// <summary>
        /// Direct floating-point evaluation of the IDCT definition, used by tests as the accuracy reference for
        /// <see cref="TransformBlock"/>. Far too slow for decoding.
        /// </summary>
        internal static void ReferenceTransformBlock(int[] coeffs, byte[] destination)
        {
            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    double sum = 0.0;
                    for (int v = 0; v < 8; v++)
                    {
                        double cv = v == 0 ? InvSqrt2 : 1.0;
                        for (int u = 0; u < 8; u++)
                        {
                            double cu = u == 0 ? InvSqrt2 : 1.0;
                            sum += cu * cv * coeffs[v * 8 + u] * Cosine[x, u] * Cosine[y, v];
                        }
                    }

                    double sample = Math.Round(0.25 * sum + 128.0);
                    destination[y * 8 + x] = sample <= 0 ? (byte)0 : sample >= 255 ? (byte)255 : (byte)sample;
                }
            }
        }

        private static double[,] BuildCosineTable()
        {
            var table = new double[8, 8];
            for (int x = 0; x < 8; x++)
            {
                for (int u = 0; u < 8; u++)
                {
                    table[x, u] = Math.Cos(((2.0 * x + 1.0) * u * Math.PI) / 16.0);
                }
            }

            return table;
        }
    }
}
