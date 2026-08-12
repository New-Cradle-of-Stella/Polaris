using System;
using System.Collections.Generic;
using XX;

namespace Polaris.API
{
    /// <summary>
    /// 音效播放实例的所有权中心：创建 CRI 播放器、回收播完的播放器、发布
    /// <see cref="GameStaticCallbackKind.SoundPlayed"/>。
    /// <para>
    /// 必须有人负责回收：每一次播放都会留下一个持有 CRI 播放器实例的对象，
    /// 一局游戏下来能攒出成千上万个。
    /// </para>
    /// </summary>
    internal static class GameAudioRuntime
    {
        /// <summary>
        /// 同时在播的音效上限。到顶之后新的播放请求会被拒绝，而不是挤掉正在响的——
        /// 静默顶掉别人的声音比少响一声更难查。
        /// </summary>
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

                // force: true 只在需要叠放时开。游戏默认在同一帧里对同一个 cue 去重，
                // 循环播放要绕过这个去重，否则连续两次循环请求会被吃掉一次。
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

                // 开播宽限期内不判死活：CRI 的播放器不是 play() 返回就立刻报 "playing" 的。
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
