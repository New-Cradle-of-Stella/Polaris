using System;
using System.IO;
using NVorbis;
using UnityEngine;

namespace Polaris.Res.Loaders
{
    /// <summary>
    /// 从 wav/ogg 字节构造 Unity 原生 <see cref="AudioClip"/>；播放交给模组自己的 <c>AudioSource</c>。
    /// ogg 用 NVorbis 同步解码（而非 <c>UnityWebRequestMultimedia</c> 协程），以保持 <see cref="ModResources.Audio"/> 是同步接口。
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
            using (MemoryStream stream = new MemoryStream(bytes))
            using (VorbisReader reader = new VorbisReader(stream, false))
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
                        // 流比头部声明的 TotalSamples 短：按实际读到的截断，不报错。
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
