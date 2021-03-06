using System;
using System.IO;

public class ElfLZSS
{
    // Modified from Haruhiko Okumura's lzss.c
    private const int Offset = 0x01;
    private const int EOF = -1;
    private const int EI = 12;
    private const int EJ = 4;
    private const int P = 1;
    private const int N = 1 << EI;
    private const int F = (1 << EJ) + P;

    public static byte[] Compress(byte[] data)
    {
        if (data.Length == 0)
            return new byte[0];

        var buffer = new byte[N * 2];
        var position = 0;
        var mask = (byte)0x80;
        var byteBuffer = (byte)0;
        var bufferEnd = N * 2;
        var r = N + Offset;
        var s = Offset + F;

        for (var i = 0; i < r; ++i)
            buffer[i] = 0x20;

        for (var i = r; i < N * 2; ++i)
        {
            if (position >= data.Length)
            {
                bufferEnd = i;
                break;
            }

            buffer[i] = data[position++];
        }

        using (var stream = new MemoryStream())
        {
            while (r < bufferEnd)
            {
                var x = 0;
                var y = 1;
                var c = buffer[r];

                for (var i = r - 1; i >= s; i--)
                {
                    // 0x0000 = End
                    if ((i & (N - 1)) == 0)
                        continue;

                    if (buffer[i] == c)
                    {
                        var f = Math.Min(F, bufferEnd - r);
                        int j;

                        for (j = 1; j < f; j++)
                        {
                            if (buffer[i + j] != buffer[r + j])
                                break;
                        }

                        if (j > y)
                        {
                            x = i;
                            y = j;
                        }
                    }
                }

                if (y <= P)
                {
                    SetBit(1, 1, stream, ref mask, ref byteBuffer);
                    SetBit(8, c, stream, ref mask, ref byteBuffer);
                }
                else
                {
                    SetBit(1, 0, stream, ref mask, ref byteBuffer);
                    SetBit(EI, x & (N - 1), stream, ref mask, ref byteBuffer);
                    SetBit(EJ, y - 2, stream, ref mask, ref byteBuffer);
                }

                r += y;
                s += y;

                if (r >= N * 2 - F)
                {
                    for (var i = 0; i < N; i++)
                        buffer[i] = buffer[i + N];

                    bufferEnd -= N;
                    r -= N;
                    s -= N;

                    while (bufferEnd < N * 2 && position < data.Length)
                        buffer[bufferEnd++] = data[position++];
                }
            }

            // Write end
            SetBit(1 + EI + EJ, 0, stream, ref mask, ref byteBuffer);

            if (mask != 0x80)
                stream.WriteByte(byteBuffer);

            return stream.ToArray();
        }
    }

    public static byte[] Decompress(byte[] data)
    {
        if (data.Length == 0)
            return new byte[0];

        var buffer = new byte[N];
        var position = 0;
        var mask = (byte)0x80;
        var r = Offset;

        for (var i = 0; i < r; ++i)
            buffer[i] = 0x20;

        using (var stream = new MemoryStream())
        {
            while (true)
            {
                var mode = GetBit(1, data, ref position, ref mask);

                if (mode == EOF)
                    break;

                if (mode == 1)
                {
                    var output = GetBit(8, data, ref position, ref mask);

                    if (output == EOF)
                        break;

                    stream.WriteByte(buffer[r++] = (byte)output);
                    r &= N - 1;
                }
                else
                {
                    var i = GetBit(EI, data, ref position, ref mask);

                    if (i == EOF)
                        break;

                    var j = GetBit(EJ, data, ref position, ref mask);

                    if (j == EOF)
                        break;

                    // Skip end
                    if (i == 0)
                        break;

                    for (var k = 0; k <= j + 1; k++)
                    {
                        var output = buffer[(i + k) & (N - 1)];

                        stream.WriteByte(buffer[r++] = output);
                        r &= N - 1;
                    }
                }
            }

            return stream.ToArray();
        }
    }

    public static void SetBit(int n, int value, Stream stream, ref byte mask, ref byte buffer)
    {
        var valueMask = 1 << (n - 1);

        for (var i = 0; i < n; ++i)
        {
            if ((value & valueMask) != 0)
                buffer |= mask;

            mask >>= 1;
            valueMask >>= 1;

            if (mask == 0)
            {
                stream.WriteByte(buffer);

                buffer = 0;
                mask = 0x80;
            }
        }
    }

    private static int GetBit(int n, byte[] data, ref int position, ref byte mask)
    {
        var value = 0;

        for (var i = 0; i < n; ++i)
        {
            if (mask == 0)
            {
                if (++position >= data.Length)
                    return EOF;

                mask = 0x80;
            }

            value <<= 1;

            if ((data[position] & mask) != 0)
                ++value;

            mask >>= 1;
        }

        return value;
    }
}