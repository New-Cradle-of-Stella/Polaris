using System;
using System.IO;
using UnityEngine;

namespace Polaris.Res.Loaders
{
    /// <summary>
    /// 从 wav/ogg 字节构造 <see cref="AudioClip"/>。游戏本身没有"原始音频"这类封装——游戏音频
    /// 走 CRIWARE cue sheet（<c>.acb</c>/<c>.awb</c>），不认裸 wav/ogg 文件——所以这里直接产出
    /// Unity 原生 <see cref="AudioClip"/>，播放交给模组自己的 <c>AudioSource</c>，PolarisRes
    /// 只负责把文件解码成能用的 <see cref="AudioClip"/>。
    /// <para>
    /// wav 是手写 RIFF/PCM 解析（见 <see cref="WavParser"/>），同步完成没有难度；ogg(Vorbis)
    /// 解码本身复杂得多，Unity 又没有公开的同步解码 API，这里引入 NVorbis（纯 C#、同步解码）
    /// 而不是走 <c>UnityWebRequestMultimedia</c> 协程——否则 <see cref="ModResources.Audio"/>
    /// 会被迫变成像 PXLS 一样的跨帧异步接口，而目前"只有 PXLS 异步"是刻意维持的架构不变量。
    /// </para>
    /// </summary>
    internal static class AudioLoader
    {
        internal static AudioClip FromBytes(byte[] bytes, string absolutePath, ResourceId id)
        {
            string extension = Path.GetExtension(absolutePath);

            if (string.Equals(extension, ".wav", StringComparison.OrdinalIgnoreCase))
            {
                WavData wav = WavParser.Parse(bytes, id);
                return CreateClip(id, wav.Samples, wav.SampleCount, wav.Channels, wav.SampleRate);
            }

            if (string.Equals(extension, ".ogg", StringComparison.OrdinalIgnoreCase))
            {
                return FromOgg(bytes, id);
            }

            throw new ResourceLoadException(id, $"Unsupported audio extension: \"{extension}\" (only .wav/.ogg are supported).");
        }

        private static AudioClip FromOgg(byte[] bytes, ResourceId id)
        {
            using (var stream = new MemoryStream(bytes))
            using (var reader = new NVorbis.VorbisReader(stream, false))
            {
                int channels = reader.Channels;
                int sampleRate = reader.SampleRate;
                long totalSamples = reader.TotalSamples;
                if (totalSamples <= 0 || channels <= 0)
                {
                    throw new ResourceLoadException(id, "ogg decode produced nothing (TotalSamples/Channels <= 0).");
                }

                float[] samples = new float[totalSamples * channels];
                int readTotal = 0;
                while (readTotal < samples.Length)
                {
                    int read = reader.ReadSamples(samples, readTotal, samples.Length - readTotal);
                    if (read <= 0)
                    {
                        // Vorbis 流比头部声明的 TotalSamples 短：按实际读到的截断，不报错——
                        // 这和 wav 遇到被截断 data 块时的容错思路一致。
                        break;
                    }

                    readTotal += read;
                }

                if (readTotal < samples.Length)
                {
                    Array.Resize(ref samples, readTotal);
                }

                return CreateClip(id, samples, readTotal / channels, channels, sampleRate);
            }
        }

        private static AudioClip CreateClip(ResourceId id, float[] samples, int sampleCount, int channels, int sampleRate)
        {
            AudioClip clip = AudioClip.Create(id.Path, sampleCount, channels, sampleRate, stream: false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
