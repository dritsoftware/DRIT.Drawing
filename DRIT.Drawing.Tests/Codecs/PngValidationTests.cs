using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DRIT.Drawing.Colors;
using DRIT.Drawing.Enums;
using DRIT.Drawing.Imaging;

namespace DRIT.Drawing.Tests.Codecs;

[TestClass]
public sealed class PngValidationTests
{
    [TestMethod]
    public void Png_InvalidChunkCrc_ThrowsInvalidData()
    {
        byte[] bytes = MakeStaticPng();
        int ihdr = FindChunk(bytes, "IHDR");
        bytes[ihdr + 8 + 13] ^= 0x01;

        Assert.ThrowsException<InvalidDataException>(() => Decode(bytes));
    }

    [TestMethod]
    public void Png_InvalidIhdrCombination_ThrowsInvalidData()
    {
        byte[] bytes = MakeStaticPng();
        int ihdr = FindChunk(bytes, "IHDR");
        bytes[ihdr + 8 + 8] = 4;
        RewriteChunkCrc(bytes, ihdr);

        Assert.ThrowsException<InvalidDataException>(() => Decode(bytes));
    }

    [TestMethod]
    public void Apng_SequenceGap_ThrowsInvalidData()
    {
        byte[] bytes = MakeAnimatedPng();
        int[] frameControls = FindChunks(bytes, "fcTL");
        Assert.AreEqual(2, frameControls.Length);
        WriteUInt32BE(bytes, frameControls[1] + 8, 7);
        RewriteChunkCrc(bytes, frameControls[1]);

        Assert.ThrowsException<InvalidDataException>(() => Decode(bytes));
    }

    [TestMethod]
    public void Apng_FrameOutsideCanvas_ThrowsInvalidData()
    {
        byte[] bytes = MakeAnimatedPng();
        int[] frameControls = FindChunks(bytes, "fcTL");
        Assert.AreEqual(2, frameControls.Length);
        WriteUInt32BE(bytes, frameControls[1] + 8 + 4, 3);
        RewriteChunkCrc(bytes, frameControls[1]);

        Assert.ThrowsException<InvalidDataException>(() => Decode(bytes));
    }

    private static Bitmap Decode(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        return new Bitmap(stream);
    }

    private static byte[] MakeStaticPng()
    {
        using var source = new Bitmap(1, 1, PixelFormat.Format32bppArgb);
        source.SetPixel(0, 0, Color.Red);
        using var stream = new MemoryStream();
        source.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }

    private static byte[] MakeAnimatedPng()
    {
        using var first = new Bitmap(1, 1, PixelFormat.Format32bppArgb);
        first.SetPixel(0, 0, Color.Red);
        using var second = new Bitmap(1, 1, PixelFormat.Format32bppArgb);
        second.SetPixel(0, 0, Color.Blue);

        var frames = new List<AnimationFrame>
        {
            FrameFromBitmap(first),
            FrameFromBitmap(second),
        };
        using var animation = new Bitmap(1, 1, PixelFormat.Format32bppArgb);
        animation.SetAnimationFrames(frames, 0);
        using var stream = new MemoryStream();
        animation.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }

    private static AnimationFrame FrameFromBitmap(Bitmap source)
    {
        Color color = source.GetPixel(0, 0);
        return new AnimationFrame(new DRIT.Drawing.Geometry.Rectangle(0, 0, 1, 1), new[]
        {
            color.B, color.G, color.R, color.A
        })
        {
            BlendMethod = FrameBlendMethod.Source,
            DisposeMethod = FrameDisposeMethod.None,
        };
    }

    private static int FindChunk(byte[] bytes, string type)
    {
        int[] matches = FindChunks(bytes, type);
        if (matches.Length == 0)
            throw new InvalidDataException($"Chunk {type} was not found.");
        return matches[0];
    }

    private static int[] FindChunks(byte[] bytes, string type)
    {
        var result = new List<int>();
        for (int offset = 8; offset + 12 <= bytes.Length; )
        {
            int length = checked((int)ReadUInt32BE(bytes, offset));
            if (offset + 12 + length > bytes.Length)
                throw new InvalidDataException("PNG chunk is truncated.");
            string actualType = Encoding.ASCII.GetString(bytes, offset + 4, 4);
            if (actualType == type)
                result.Add(offset);
            offset += 12 + length;
            if (actualType == "IEND")
                break;
        }
        return result.ToArray();
    }

    private static void RewriteChunkCrc(byte[] bytes, int chunkOffset)
    {
        int length = checked((int)ReadUInt32BE(bytes, chunkOffset));
        var crcInput = new byte[4 + length];
        Buffer.BlockCopy(bytes, chunkOffset + 4, crcInput, 0, crcInput.Length);
        uint crc = Crc32(crcInput);
        int crcOffset = chunkOffset + 8 + length;
        WriteUInt32BE(bytes, crcOffset, crc);
    }

    private static uint Crc32(byte[] bytes)
    {
        uint crc = 0xFFFFFFFFu;
        for (int i = 0; i < bytes.Length; i++)
        {
            crc ^= bytes[i];
            for (int bit = 0; bit < 8; bit++)
                crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
        }
        return ~crc;
    }

    private static uint ReadUInt32BE(byte[] bytes, int offset)
        => ((uint)bytes[offset] << 24)
            | ((uint)bytes[offset + 1] << 16)
            | ((uint)bytes[offset + 2] << 8)
            | bytes[offset + 3];

    private static void WriteUInt32BE(byte[] bytes, int offset, uint value)
    {
        bytes[offset] = (byte)(value >> 24);
        bytes[offset + 1] = (byte)(value >> 16);
        bytes[offset + 2] = (byte)(value >> 8);
        bytes[offset + 3] = (byte)value;
    }
}
