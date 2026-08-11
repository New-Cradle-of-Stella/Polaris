using System;
using System.Collections.Generic;
using XX;

namespace Polaris.API
{
    /// <summary>一次播放的句柄。同一个音效可以同时响很多次，靠名字是停不准的。</summary>
    public readonly struct AudioHandle
    {
        public long Id { get; }

        internal AudioHandle(long id)
        {
            Id = id;
        }

        public static AudioHandle None => default;

        public bool IsEmpty => Id == 0;

        public override string ToString() => IsEmpty ? "AudioHandle(empty)" : $"AudioHandle({Id})";
    }

    /// <summary>播放状态。</summary>
    public enum AudioPlaybackState
    {
        /// <summary>句柄不认识，或者这次播放已经结束并被回收。</summary>
        Unknown,
        Playing,
        Paused,
        Stopped,
    }

    /// <summary>
    /// 音频。
    /// <para>
    /// 与旧 LuaAiC 最大的差别是<b>按句柄控制而不是按名字</b>：<c>StopSound("hit")</c> 在同一个音效
    /// 同时响了三次的时候，谁也说不清停的是哪一次。这里每次 <see cref="PlaySound"/> 都发一个
    /// <see cref="AudioHandle"/>，停、暂停、查状态都认这个句柄。
    /// </para>
    /// <para>
    /// 这里只负责游戏自带的 cue。模组自带的音频文件由资源子系统加载，之后再接进来。
    /// </para>
    /// </summary>
    public sealed class AudioGameAPI
    {
        /// <summary>
        /// 同时在播的音效上限。到顶之后新的播放请求会被拒绝而不是挤掉正在响的
        /// ——静默顶掉别人的声音比少响一声更难查。
        /// </summary>
        const int MaxConcurrentSounds = 32;

        /// <summary>
        /// 刚开播的这几帧不做回收。CRI 的播放器不是 <c>play()</c> 返回就立刻报 "playing" 的，
        /// 少了这个宽限期，每一次播放都会在下一帧被自己的回收逻辑当成"已经播完"释放掉。
        /// </summary>
        const int CollectGraceFrames = 10;

        readonly Dictionary<long, Entry> Players = new Dictionary<long, Entry>();
        readonly List<long> Finished = new List<long>(8);

        long nextId = 1;

        struct Entry
        {
            public SndPlayer Player;
            public int BornFrame;
        }

        /// <summary>
        /// 播一个游戏自带的音效 cue，返回可用来控制这一次播放的句柄。
        /// <para>
        /// 同一帧里同一个 cue 默认只会响一次（游戏自己的去重），确实需要叠起来时传
        /// <paramref name="force"/>。
        /// </para>
        /// </summary>
        public AudioHandle PlaySound(string cueName, bool force = false)
        {
            if (string.IsNullOrEmpty(cueName))
            {
                return AudioHandle.None;
            }

            Collect();

            if (Players.Count >= MaxConcurrentSounds)
            {
                Plugin.Logger.LogWarning($"[Polaris] Reached the concurrent sound limit of {MaxConcurrentSounds}; ignoring: {cueName}.");
                return AudioHandle.None;
            }

            try
            {
                var Player = new SndPlayer($"polaris_snd_{nextId}");
                if (!Player.play(cueName, force))
                {
                    Player.Dispose();
                    return AudioHandle.None;
                }

                long id = nextId++;
                Players[id] = new Entry { Player = Player, BornFrame = UnityEngine.Time.frameCount };
                return new AudioHandle(id);
            }
            catch (Exception ex)
            {
                PolarisAPI.Errors.Report(ex, "Audio.PlaySound");
                return AudioHandle.None;
            }
        }

        /// <summary>停止这一次播放。句柄已经回收（自然播完了）时返回成功——目标状态已经达成。</summary>
        public GameActionResult Stop(AudioHandle handle) => Control(handle, "Audio.Stop", Player =>
        {
            Player.Stop();
            return true;
        });

        /// <summary>暂停这一次播放。</summary>
        public GameActionResult Pause(AudioHandle handle) => Control(handle, "Audio.Pause", Player =>
        {
            Player.Pause();
            return true;
        });

        /// <summary>继续播放。</summary>
        public GameActionResult Resume(AudioHandle handle) => Control(handle, "Audio.Resume", Player =>
        {
            Player.Start();
            return true;
        });

        /// <summary>查这一次播放的状态。</summary>
        public AudioPlaybackState GetState(AudioHandle handle)
        {
            if (handle.IsEmpty || !Players.TryGetValue(handle.Id, out Entry Slot))
            {
                return AudioPlaybackState.Unknown;
            }

            try
            {
                return Slot.Player.isPlaying() ? AudioPlaybackState.Playing : AudioPlaybackState.Stopped;
            }
            catch (Exception)
            {
                return AudioPlaybackState.Unknown;
            }
        }

        /// <summary>当前有没有 BGM 在放。</summary>
        public bool IsMusicPlaying
        {
            get
            {
                try
                {
                    return BGM.isFrontPlaying();
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// 淡出当前 BGM。<paramref name="frames"/> 是淡出时长（帧）。
        /// <para>
        /// 只提供淡出而不提供"换一首"，是因为 BGM 的切换绑着游戏自己的分块、拍点与过渡表，
        /// 从外部塞一首进去会让那套状态机对不上。要放模组自己的曲子属于资源层的题目。
        /// </para>
        /// </summary>
        public GameActionResult FadeOutMusic(float frames = 120f)
        {
            try
            {
                BGM.fadeout(0f, frames);
                return GameActionResult.Ok();
            }
            catch (Exception ex)
            {
                PolarisAPI.Errors.Report(ex, "Audio.FadeOutMusic");
                return GameActionResult.Fail(GameActionStatus.Failed, ex.Message);
            }
        }

        /// <summary>淡入当前 BGM。</summary>
        public GameActionResult FadeInMusic(float frames = 120f)
        {
            try
            {
                BGM.fadein(100f, frames);
                return GameActionResult.Ok();
            }
            catch (Exception ex)
            {
                PolarisAPI.Errors.Report(ex, "Audio.FadeInMusic");
                return GameActionResult.Fail(GameActionStatus.Failed, ex.Message);
            }
        }

        GameActionResult Control(AudioHandle handle, string what, Func<SndPlayer, bool> action)
        {
            if (handle.IsEmpty)
            {
                return GameActionResult.Fail(GameActionStatus.InvalidArgument, "Empty audio handle.");
            }

            if (!Players.TryGetValue(handle.Id, out Entry Slot))
            {
                // 已经播完并被回收：调用方想要的结果（这一声别响了）已经成立，报失败只会
                // 让"停掉可能已经结束的音效"这种再普通不过的写法被迫先查一次状态。
                return GameActionResult.Ok();
            }

            try
            {
                action(Slot.Player);
                return GameActionResult.Ok();
            }
            catch (Exception ex)
            {
                PolarisAPI.Errors.Report(ex, what);
                return GameActionResult.Fail(GameActionStatus.Failed, ex.Message);
            }
        }

        /// <summary>由 <see cref="GameStateAPI.Pump"/> 每帧调用：回收已经播完的播放器。</summary>
        internal void Pump() => Collect();

        /// <summary>
        /// 把已经不响了的播放器释放掉。不做这一步的话，每一次 <c>PlaySound</c> 都会留下一个
        /// 持有 CRI 播放器实例的对象，一局游戏下来能攒出成千上万个。
        /// </summary>
        void Collect()
        {
            if (Players.Count == 0)
            {
                return;
            }

            Finished.Clear();
            int now = UnityEngine.Time.frameCount;

            foreach (KeyValuePair<long, Entry> pair in Players)
            {
                if (now - pair.Value.BornFrame < CollectGraceFrames)
                {
                    continue;
                }

                try
                {
                    if (!pair.Value.Player.isPlaying())
                    {
                        Finished.Add(pair.Key);
                    }
                }
                catch (Exception)
                {
                    // 问不出状态的播放器一律当作已结束回收掉：留着它只会每帧再抛一次。
                    Finished.Add(pair.Key);
                }
            }

            foreach (long id in Finished)
            {
                if (Players.TryGetValue(id, out Entry Slot))
                {
                    Players.Remove(id);
                    try
                    {
                        Slot.Player.Dispose();
                    }
                    catch (Exception)
                    {
                        // 释放失败没有补救手段，也不值得打扰调用方。
                    }
                }
            }
        }
    }
}
