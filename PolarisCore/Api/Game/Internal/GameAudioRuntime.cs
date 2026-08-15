using System;
using System.Collections.Generic;
using XX;

namespace Polaris.API
{
    /// <summary>
    /// 音效播放实例的所有权中心：创建/回收 CRI 播放器并发布 <see cref="GameStaticCallbackKind.SoundPlayed"/>。
    /// </summary>
    internal static class GameAudioRuntime
    {
        /// <summary>同时在播的音效上限；到顶后新请求被拒绝而不是挤掉正在响的。</summary>
        const int MaxConcurrentSounds = 32;

        static readonly List<GameAudioPlayback> live = new(8);
        static readonly List<GameAudioPlayback> finished = new(8);

        static long nextPlayerId = 1;

        internal static GameAudioPlayback Play(string cue, bool loop)
        {
            if (string.IsNullOrEmpty(cue))
            {
                return null;
            }

            Collect();

            if (live.Count >= MaxConcurrentSounds)
            {
                Plugin.Logger.LogWarning(
                    $"[Polaris] Reached the concurrent sound limit of {MaxConcurrentSounds}; ignoring: {cue}.");
                return null;
            }

            try
            {
                var player = new SndPlayer($"polaris_snd_{nextPlayerId++}");

                // 游戏默认同帧内对同一 cue 去重；循环播放需绕过，否则会被吃掉一次。
                if (!player.play(cue, loop))
                {
                    player.Dispose();
                    return null;
                }

                if (loop)
                {
                    player.is_loop = 1;
                }

                var playback = new GameAudioPlayback(player, cue);
                live.Add(playback);

                GameCallbackHub.PublishStatic(
                    GameStaticCallbackKind.SoundPlayed, () => new SoundPlayedCallbackData(cue, playback));

                return playback;
            }
            catch (Exception ex)
            {
                PolarisAPI.Errors.Report(ex, "Game.Audio.Play");
                return null;
            }
        }

        /// <summary>由每帧的泵调用。</summary>
        internal static void Pump() => Collect();

        /// <summary>世界卸载时把还在手上的播放实例全部释放掉。</summary>
        internal static void ReleaseAll()
        {
            if (live.Count == 0)
            {
                return;
            }

            var all = new List<GameAudioPlayback>(live);
            live.Clear();

            foreach (GameAudioPlayback playback in all)
            {
                playback.Release();
            }
        }

        static void Collect()
        {
            if (live.Count == 0)
            {
                return;
            }

            finished.Clear();

            for (int i = 0; i < live.Count; i++)
            {
                GameAudioPlayback playback = live[i];

                // 开播宽限期内不判死活：play() 返回后 CRI 播放器不会立刻报 "playing"。
                if (!playback.PastGracePeriod)
                {
                    continue;
                }

                if (!playback.StillSounding())
                {
                    finished.Add(playback);
                }
            }

            foreach (GameAudioPlayback playback in finished)
            {
                live.Remove(playback);
                playback.Release();
            }

            finished.Clear();
        }
    }
}
