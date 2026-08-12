using System;
using XX;

namespace Polaris.API
{
    /// <summary>
    /// 一次音效播放（而非一个音效名，因为同一音效可能同时响多次）。取得实例的入口是
    /// <c>PolarisAPI.Game.Audio.Play</c> 与 <see cref="GameStaticCallbackKind.SoundPlayed"/> 回调；
    /// 播放结束后失效，对失效实例调 <see cref="Stop"/> 不抛异常。
    /// </summary>
    public sealed class GameAudioPlayback : GameInstance
    {
        /// <summary>刚开播的这几帧不做回收：CRI 播放器不是 <c>play()</c> 一返回就立刻报 "playing"，缺这个宽限期会被误判为已播完。</summary>
        internal const int CollectGraceFrames = 10;

        readonly SndPlayer player;
        readonly int bornFrame;

        bool released;

        internal GameAudioPlayback(SndPlayer player, string cue)
        {
            this.player = player;
            Cue = cue;
            bornFrame = SafeFrame();
        }

        /// <summary>这次播放的 cue 名。规范没有列这一项，所以只在库内部使用（诊断与回调负荷）。</summary>
        internal string Cue { get; }

        private protected override bool IsNativeAlive => !released && player != null;

        private protected override string Describe() => $"GameAudioPlayback({Cue})";

        /// <summary>判断该播放实例是否循环播放。</summary>
        public bool IsLooping => Read(static p => p.is_loop != 0, false);

        /// <summary>获取该播放实例的基础音量。</summary>
        public float BaseVolume => Read(static p => p.base_volume, 0f);

        /// <summary>获取该播放实例的剩余播放毫秒数；无法确定时为 0。</summary>
        public long RemainingMilliseconds => Read(static p => p.rest_duration_milisecond, 0L);

        /// <summary>停止该音频播放实例。已经自然播完的实例上调用是安全的空操作。</summary>
        public void Stop() => Control("Stop", static p => p.Stop());

        /// <summary>暂停或恢复该音频播放实例。</summary>
        public void Pause(bool paused) => Control("Pause", p =>
        {
            if (paused)
            {
                p.Pause();
            }
            else
            {
                p.Start();
            }
        });

        /// <summary>判断该音频播放实例是否正在播放。</summary>
        public bool IsPlaying() => Read(static p => p.isPlaying(), false);

        /// <summary>设置该播放实例的 AISAC 控制值（游戏用它做距离衰减、水下闷音之类的实时效果）。</summary>
        public void SetAisac(string control, float value)
        {
            if (string.IsNullOrEmpty(control))
            {
                return;
            }

            Control("SetAisac", p => p.SetAisacControl(control, value));
        }

        /// <summary>这次播放是否已经过了开播宽限期，可以按 <c>isPlaying()</c> 判断死活了。</summary>
        internal bool PastGracePeriod => SafeFrame() - bornFrame >= CollectGraceFrames;

        /// <summary>由音频运行时在回收这次播放时调用：释放 CRI 播放器并让包装器失效。</summary>
        internal void Release()
        {
            if (released)
            {
                return;
            }

            released = true;

            try
            {
                player?.Dispose();
            }
            catch (Exception)
            {
                // 释放失败无法补救，忽略。
            }

            Invalidate();
        }

        internal bool StillSounding()
        {
            try
            {
                return player != null && player.isPlaying();
            }
            catch (Exception)
            {
                // 问不出状态一律当作已结束。
                return false;
            }
        }

        void Control(string what, Action<SndPlayer> action)
        {
            // 不走 EnsureUsable：对可能已播完的音效喊停是正常用法，不必强迫先查状态。
            if (released || player == null)
            {
                return;
            }

            try
            {
                action(player);
            }
            catch (Exception ex)
            {
                PolarisAPI.Errors.Report(ex, $"GameAudioPlayback.{what}");
            }
        }

        TValue Read<TValue>(Func<SndPlayer, TValue> read, TValue fallback)
        {
            if (released || player == null)
            {
                return fallback;
            }

            try
            {
                return read(player);
            }
            catch (Exception)
            {
                return fallback;
            }
        }

        static int SafeFrame()
        {
            try
            {
                return UnityEngine.Time.frameCount;
            }
            catch (Exception)
            {
                return 0;
            }
        }
    }
}
