using System;
using System.Collections.Generic;
using System.Reflection;

namespace Polaris.API
{
    /// <summary>
    /// 游戏循环：每帧回调、窗口焦点、退出，以及游戏自己的帧计数。
    /// <para>
    /// 与旧 LuaAiC 的 <c>FrameFunc</c> 有两点不同。其一，回调<b>不能</b>通过返回值结束游戏——
    /// 让任意一段内容脚本有权关掉玩家的游戏，这个能力和它带来的事故完全不成比例。其二，
    /// 单个订阅者抛异常只会让它自己被归因，不影响其它订阅者，也不会把这一帧掀掉。
    /// </para>
    /// <para>
    /// 没有 <c>Rendering</c> 事件：那需要接进游戏自己的渲染阶段，属于渲染命令缓冲那一层的事，
    /// 在这里放一个"其实是在 Update 里触发"的假 Render 回调只会误导人。
    /// </para>
    /// </summary>
    public sealed class GameLoopAPI
    {
        /// <summary>
        /// 每帧（Unity <c>Update</c>）。高频路径，订阅者请自己控制开销。
        /// <para>
        /// 用显式的 add/remove 而不是字段式事件，是为了在派发时不走
        /// <c>Delegate.GetInvocationList()</c>——那个方法每次调用都新建一个数组，
        /// 放在每帧执行的路径上就是每秒几十次的白白分配。这里改成维护一份订阅者列表，
        /// 只有增删订阅时才动它。
        /// </para>
        /// </summary>
        public event Action Updating
        {
            add => Subscribe(UpdatingHandlers, value);
            remove => Unsubscribe(UpdatingHandlers, value);
        }

        /// <summary>每帧的 <c>LateUpdate</c>：这一帧里所有 <c>Update</c> 都跑完之后。</summary>
        public event Action LateUpdating
        {
            add => Subscribe(LateUpdatingHandlers, value);
            remove => Unsubscribe(LateUpdatingHandlers, value);
        }

        /// <summary>窗口焦点变化，参数为"是否拿到焦点"。低频，直接用字段式事件。</summary>
        public event Action<bool> FocusChanged;

        /// <summary>进程即将退出。只适合做快速收尾，不要在这里做存盘之外的重活。</summary>
        public event Action Stopping;

        readonly List<Action> UpdatingHandlers = new List<Action>(4);
        readonly List<Action> LateUpdatingHandlers = new List<Action>(2);

        /// <summary>派发时的临时缓冲：订阅者可能在自己的回调里退订，直接遍历原列表会炸。</summary>
        readonly List<Action> Dispatch = new List<Action>(8);

        /// <summary>Unity 帧号。</summary>
        public int FrameCount => UnityEngine.Time.frameCount;

        /// <summary>
        /// 游戏自己的帧计数（<c>XX.IN.totalframe</c>）。它和 <see cref="FrameCount"/> 不是一回事：
        /// 游戏在读档、演出与暂停期间不一定推进自己的计数，需要"游戏内时间"的逻辑要用这个。
        /// </summary>
        public int GameFrameCount
        {
            get
            {
                try
                {
                    return XX.IN.totalframe;
                }
                catch (Exception)
                {
                    return 0;
                }
            }
        }

        /// <summary>窗口当前是否有焦点。</summary>
        public bool HasFocus
        {
            get
            {
                try
                {
                    return XX.IN.application_focus;
                }
                catch (Exception)
                {
                    return true;
                }
            }
        }

        /// <summary>
        /// 暂停游戏。<b>本版本未支持</b>：游戏没有一个全局暂停开关，菜单和事件各自停自己那一摊，
        /// 硬做一个会让两边对"现在是不是暂停"的判断打架。详见 <see cref="GameCapabilities"/>。
        /// </summary>
        public GameActionResult Pause(string ownerKey)
            => GameActionResult.Unsupported("This game version has no usable global pause entry point.");

        /// <summary>与 <see cref="Pause"/> 成对，同样未支持。</summary>
        public GameActionResult Resume(string ownerKey)
            => GameActionResult.Unsupported("This game version has no usable global pause entry point.");

        internal void PumpUpdate() => Raise(UpdatingHandlers, "Loop.Updating");

        internal void PumpLateUpdate() => Raise(LateUpdatingHandlers, "Loop.LateUpdating");

        internal void RaiseFocusChanged(bool hasFocus)
        {
            LifecycleCallbacks.PublishFocusChanged(hasFocus);

            Action<bool> handlers = FocusChanged;
            if (handlers == null)
            {
                return;
            }

            foreach (Delegate handler in handlers.GetInvocationList())
            {
                try
                {
                    ((Action<bool>)handler)(hasFocus);
                }
                catch (Exception ex)
                {
                    Report(ex, handler, "Loop.FocusChanged");
                }
            }
        }

        internal void RaiseStopping()
        {
            LifecycleCallbacks.PublishStopping();

            Action handlers = Stopping;
            if (handlers == null)
            {
                return;
            }

            foreach (Delegate handler in handlers.GetInvocationList())
            {
                try
                {
                    ((Action)handler)();
                }
                catch (Exception ex)
                {
                    Report(ex, handler, "Loop.Stopping");
                }
            }
        }

        static void Subscribe(List<Action> Handlers, Action handler)
        {
            if (handler != null && !Handlers.Contains(handler))
            {
                Handlers.Add(handler);
            }
        }

        static void Unsubscribe(List<Action> Handlers, Action handler)
        {
            if (handler != null)
            {
                Handlers.Remove(handler);
            }
        }

        /// <summary>
        /// 逐个调用：一个订阅者抛异常不该让排在它后面的收不到通知。先复制一份再遍历，
        /// 因为订阅者完全可能在自己的回调里退订（"这件事我只关心一次"是很常见的写法）。
        /// </summary>
        void Raise(List<Action> Handlers, string what)
        {
            if (Handlers.Count == 0)
            {
                return;
            }

            Dispatch.Clear();
            Dispatch.AddRange(Handlers);

            for (int i = 0; i < Dispatch.Count; i++)
            {
                Action handler = Dispatch[i];
                try
                {
                    handler();
                }
                catch (Exception ex)
                {
                    Report(ex, handler, what);
                }
            }

            Dispatch.Clear();
        }

        /// <summary>责任人直接取委托所在的程序集，不必走堆栈推断。</summary>
        static void Report(Exception ex, Delegate handler, string what)
        {
            MethodInfo method = handler?.Method;
            PolarisAPI.Errors.Report(ex, $"{what} callback", method?.DeclaringType?.Assembly);
        }
    }
}
