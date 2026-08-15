using System;
using System.Text;

namespace Polaris.Res.Loaders
{
    /// <summary>解析结果：交给 <see cref="AudioLoader"/> 直接喂给 <c>AudioClip.Create</c>/<c>SetData</c>。</summary>
    internal readonly struct WavData
    {
        internal WavData(float[] samples, int sampleCount, int channels, int sampleRate)
        {
            Samples = samples;
            SampleCount = sampleCount;
            Channels = channels;
            SampleRate = sampleRate;
        }

        /// <summary>交错排列的采样值（每个声道依次一个，和 Unity <c>AudioClip.SetData</c> 的期望一致）。</summary>
        internal float[] Samples { get; }

        /// <summary>每个声道的采样数（不是 <see cref="Samples"/> 的总长度）。</summary>
        internal int SampleCount { get; }

        internal int Channels { get; }

        internal int SampleRate { get; }
    }

    /// <summary>手写 RIFF/WAVE 解析（little-endian），只支持 PCM 整数（8/16/24/32 位）与 IEEE float32，不引入第三方依赖。</summary>
    internal static class WavParser
    {
        /// <summary><c>fmt </c> 块里的编码标识：1 = PCM 整数，3 = IEEE float32；其它编码直接报错，不去猜测。</summary>
        private const ushort FormatPcm = 1;
        private const ushort FormatIeeeFloat = 3;

        internal static WavData Parse(byte[] bytes, ResourceId id)
        {
            if (bytes.Length < 12 || !HasAscii(bytes, 0, "RIFF") || !HasAscii(bytes, 8, "WAVE"))
            {
                throw new ResourceLoadException(id, "Not a valid RIFF/WAVE file (missing the RIFF/WAVE header).");
            }

            int position = 12;
            ushort audioFormat = 0;
            ushort channels = 0;
            uint sampleRate = 0;
            ushort bitsPerSample = 0;
            byte[] dataChunk = null;

            while (position + 8 <= bytes.Length)
            {
                string chunkId = Encoding.ASCII.GetString(bytes, position, 4);
                uint chunkSize = ReadUInt32(bytes, position + 4);
                int payloadStart = position + 8;

                if (payloadStart + chunkSize > bytes.Length)
                {
                    // 声明长度超出实际字节数（常见于被截断的文件）：容错截断，不当成致命错误。
                    chunkSize = (uint)Math.Max(0, bytes.Length - payloadStart);
                }

                if (chunkId == "fmt ")
                {
                    audioFormat = ReadUInt16(bytes, payloadStart);
                    channels = ReadUInt16(bytes, payloadStart + 2);
                    sampleRate = ReadUInt32(bytes, payloadStart + 4);
                    bitsPerSample = ReadUInt16(bytes, payloadStart + 14);
                }
                else if (chunkId == "data")
                {
                    dataChunk = new byte[chunkSize];
                    Array.Copy(bytes, payloadStart, dataChunk, 0, (int)chunkSize);
                }

                position = payloadStart + (int)chunkSize;
                if ((chunkSize & 1) == 1)
                {
                    // RIFF 块按偶数字节对齐，奇数长度的块后面有 1 字节 padding。
                    position++;
                }
            }

            if (channels == 0 || sampleRate == 0 || bitsPerSample == 0)
            {
                throw new ResourceLoadException(id, "wav is missing a valid fmt chunk.");
            }

            if (dataChunk == null)
            {
                throw new ResourceLoadException(id, "wav is missing the data chunk.");
            }

            if (audioFormat != FormatPcm && audioFormat != FormatIeeeFloat)
            {
                throw new ResourceLoadException(
                    id, $"Unsupported wav encoding (audioFormat={audioFormat}); only PCM integer and IEEE float32 are supported.");
            }

            int bytesPerSample = bitsPerSample / 8;
            if (bytesPerSample <= 0 || dataChunk.Length % (bytesPerSample * channels) != 0)
            {
                throw new ResourceLoadException(
                    id, $"wav data chunk length does not match the channel count/bit depth (bitsPerSample={bitsPerSample}, channels={channels}).");
            }

            float[] samples = DecodeSamples(dataChunk, audioFormat, bitsPerSample, id);
            return new WavData(samples, samples.Length / channels, channels, (int)sampleRate);
        }

        /// <summary>把 data 块整体解码成 [-1, 1] 区间的交错浮点采样；返回值含全部声道，长度是"每声道采样数 × 声道数"。</summary>
        private static float[] DecodeSamples(byte[] data, ushort audioFormat, ushort bitsPerSample, ResourceId id)
        {
            int totalSampleValues = data.Length / (bitsPerSample / 8);
            float[] samples = new float[totalSampleValues];

            if (audioFormat == FormatIeeeFloat)
            {
                if (bitsPerSample != 32)
                {
                    throw new ResourceLoadException(id, $"Unsupported IEEE float bit depth: {bitsPerSample} (only 32-bit is supported).");
                }

                for (int i = 0; i < totalSampleValues; i++)
                {
                    samples[i] = BitConverter.ToSingle(data, i * 4);
                }

                return samples;
            }

            switch (bitsPerSample)
            {
                case 8:
                    for (int i = 0; i < totalSampleValues; i++)
                    {
                        // 8 位 PCM 是无符号、128 为零点。
                        samples[i] = (data[i] - 128) / 128f;
                    }

                    break;
                case 16:
                    for (int i = 0; i < totalSampleValues; i++)
                    {
                        samples[i] = BitConverter.ToInt16(data, i * 2) / 32768f;
                    }

                    break;
                case 24:
                    for (int i = 0; i < totalSampleValues; i++)
                    {
                        int offset = i * 3;
                        int value = data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16);
                        if ((value & 0x800000) != 0)
                        {
                            value = unchecked((int)(value | 0xFF000000));
                        }

                        samples[i] = value / 8388608f;
                    }

                    break;
                case 32:
                    for (int i = 0; i < totalSampleValues; i++)
                    {
                        samples[i] = BitConverter.ToInt32(data, i * 4) / 2147483648f;
                    }

                    break;
                default:
                    throw new ResourceLoadException(id, $"Unsupported PCM bit depth: {bitsPerSample} (only 8/16/24/32-bit are supported).");
            }

            return samples;
        }

        /// <summary><paramref name="bytes"/> 从 <paramref name="offset"/> 起是否是给定的 ASCII 标记（RIFF 的四字符块标识）。</summary>
        private static bool HasAscii(byte[] bytes, int offset, string ascii)
        {
            for (int i = 0; i < ascii.Length; i++)
            {
                if (bytes[offset + i] != (byte)ascii[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static ushort ReadUInt16(byte[] bytes, int offset) =>
            (ushort)(bytes[offset] | (bytes[offset + 1] << 8));

        private static uint ReadUInt32(byte[] bytes, int offset) =>
            (uint)(bytes[offset] | (bytes[offset + 1] << 8) | (bytes[offset + 2] << 16) | (bytes[offset + 3] << 24));
    }
}
