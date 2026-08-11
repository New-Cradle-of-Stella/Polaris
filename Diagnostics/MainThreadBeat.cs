using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using UnityEngine.SceneManagement;

namespace Polaris.Diagnostics
{
    /// <summary>
    /// 主线程的"我还活着"信号，以及"此刻正在执行谁的代码"的面包屑。<see cref="Watchdog"/>
    /// 在后台线程读这两样东西，据此判断主线程是不是卡住了、以及卡在谁身上。
    /// <para>
    /// <b>写入方永远只有主线程，读取方永远只有看门狗线程</b>，所以这里没有锁：每帧都要走的
    /// 代码上挂一把锁，代价会落在从不出问题的那 99.99% 的帧上。取而代之的是
    /// <see cref="Volatile"/> 读写 + 只用引用/int/long 这种单次写入就完整的字段——看门狗最坏
    /// 情况是读到上一帧的值，而它要回答的问题是"最近这三十秒有没有推进过"，差一帧毫无影响。
    /// </para>
    /// </summary>
    internal static class MainThreadBeat
    {
        /// <summary>
        /// 计时一律用 <see cref="Stopwatch"/>，不用 <c>Environment.TickCount</c>（约 49 天回绕）
        /// 也不用 <c>DateTime.UtcNow</c>（玩家改系统时间、夏令时切换都会让它跳）。
        /// 卡死判定唯一需要的性质就是单调，而这正是 Stopwatch 唯一保证的性质。
        /// </summary>
        static readonly Stopwatch Clock = Stopwatch.StartNew();

        /// <summary>面包屑栈的深度上限。嵌套超过这个数只累加深度、不再占槽（见 <see cref="Push"/>）。</summary>
        const int MaxDepth = 8;

        /// <summary>每隔多少帧采一次当前场景名。采样而不是每帧读，是因为 <c>Scene.name</c> 会分配字符串。</summary>
        const int SceneSampleFrames = 30;

        static int mainThreadId;
        static bool installed;

        static long beatMillis;
        static int beatFrame;
        static bool beaten;
        static string sceneName;

        static readonly string[] activities = new string[MaxDepth];
        static readonly Assembly[] owners = new Assembly[MaxDepth];
        static int depth;

        /// <summary>由 <c>Plugin.Awake</c> 最先调用，记住主线程是哪一个并给心跳一个初值。</summary>
        internal static void Install()
        {
            if (installed)
            {
                return;
            }

            installed = true;
            mainThreadId = Thread.CurrentThread.ManagedThreadId;
            Volatile.Write(ref beatMillis, Clock.ElapsedMilliseconds);
        }

        /// <summary>
        /// 面包屑的写入方必须是主线程，否则两个线程一起推同一个栈会把它撕坏。
        /// 后台线程调进来时 <see cref="Enter"/> 直接返回空作用域——它的调用点通常是
        /// <see cref="Infra.ErrorsAPI.Guard"/> 这种两边都能用的 API，不该为此要求调用方判断自己在哪个线程。
        /// </summary>
        internal static bool OnMainThread => Thread.CurrentThread.ManagedThreadId == mainThreadId;

        // ================== 心跳 ==================

        /// <summary>由 <c>Plugin.Update</c> 每帧调用。必须便宜：一次 Stopwatch 读 + 两次字段写。</summary>
        internal static void Beat(int frame)
        {
            Volatile.Write(ref beatMillis, Clock.ElapsedMilliseconds);
            beatFrame = frame;
            beaten = true;

            if (frame % SceneSampleFrames == 0)
            {
                SampleScene();
            }
        }

        /// <summary>
        /// 主线程本来就该推进却被外部原因停下（窗口失焦、系统休眠）之后重新开始跑时，
        /// 由主线程调一次把基线抹平——否则看门狗会把"停在后台的那五分钟"当成卡死。
        /// </summary>
        internal static void ResetBaseline()
        {
            Volatile.Write(ref beatMillis, Clock.ElapsedMilliseconds);
        }

        /// <summary>看门狗线程用的单调时钟读数，毫秒。</summary>
        internal static long ElapsedMillis => Clock.ElapsedMilliseconds;

        /// <summary>主线程上一次推进到现在过了几秒。</summary>
        internal static double SecondsSinceBeat
            => Math.Max(0L, Clock.ElapsedMilliseconds - Volatile.Read(ref beatMillis)) / 1000d;

        /// <summary>主线程是否已经至少推进过一帧（用来区分"启动阶段"和"游戏中"，两者阈值不同）。</summary>
        internal static bool HasBeaten => beaten;

        /// <summary>最后一次推进时的帧号。</summary>
        internal static int LastFrame => beatFrame;

        /// <summary>最近采到的场景名；还没采到为 null。</summary>
        internal static string SceneName => sceneName;

        static void SampleScene()
        {
            try
            {
                sceneName = SceneManager.GetActiveScene().name;
            }
            catch (Exception)
            {
                // 场景名只是报告里的一条线索，读不到就不读，绝不能把每帧都要走的心跳弄成抛异常的地方。
            }
        }

        // ================== 面包屑 ==================

        /// <summary>
        /// 进入一段"正在替某个模组执行代码"的区间。返回的作用域是 <c>struct</c>，
        /// <c>using</c> 起来不装箱——这条路径要能放心地铺到每个回调调用点上。
        /// </summary>
        internal static Scope Enter(string what, Assembly owner)
        {
            if (string.IsNullOrEmpty(what) || !OnMainThread)
            {
                return default;
            }

            Push(what, owner);
            return new Scope(true);
        }

        internal static void Push(string what, Assembly owner)
        {
            int d = depth;
            if (d < 0)
            {
                d = 0;
            }

            if (d < MaxDepth)
            {
                // 先写槽再发布深度：看门狗读到新深度时，对应的槽必须已经有内容。
                activities[d] = what;
                owners[d] = owner;
            }

            Volatile.Write(ref depth, d + 1);
        }

        internal static void Pop()
        {
            int d = Volatile.Read(ref depth) - 1;
            if (d < 0)
            {
                Volatile.Write(ref depth, 0);
                return;
            }

            // 先发布深度再清槽，和 Push 相反：看门狗读到的深度只会指向仍然有效的槽。
            Volatile.Write(ref depth, d);

            if (d < MaxDepth)
            {
                activities[d] = null;
                owners[d] = null;
            }
        }

        /// <summary>栈顶那一条，也就是"最内层正在执行的是什么"。没有埋点时为 null。</summary>
        internal static string CurrentActivity()
        {
            int top = Top();
            return top >= 0 ? activities[top] : null;
        }

        /// <summary>栈顶那一条的责任程序集。没有埋点、或调用方没给出责任方时为 null。</summary>
        internal static Assembly CurrentOwner()
        {
            int top = Top();
            return top >= 0 ? owners[top] : null;
        }

        /// <summary>
        /// 整条面包屑链，由外到内用 <c>→</c> 连起来，例如
        /// <c>本地化子系统初始化 → WhenReady 回调</c>。报告里写这一条而不是只写栈顶：
        /// 外层说明"这是在哪个阶段"，内层说明"具体卡在哪一步"，两者缺一都不够定位。
        /// </summary>
        internal static string ActivityChain()
        {
            int d = Volatile.Read(ref depth);
            if (d <= 0)
            {
                return null;
            }

            int used = Math.Min(d, MaxDepth);
            var parts = new System.Collections.Generic.List<string>(used);
            for (int i = 0; i < used; i++)
            {
                string part = activities[i];
                if (!string.IsNullOrEmpty(part))
                {
                    parts.Add(part);
                }
            }

            if (parts.Count == 0)
            {
                return null;
            }

            string chain = string.Join(" → ", parts.ToArray());
            return d > MaxDepth ? chain + $" → …（还有 {d - MaxDepth} 层）" : chain;
        }

        static int Top()
        {
            int d = Volatile.Read(ref depth);
            if (d <= 0)
            {
                return -1;
            }

            return Math.Min(d, MaxDepth) - 1;
        }

        /// <summary>
        /// <see cref="Enter"/> 的作用域。<c>readonly struct</c> + 一个 <c>active</c> 标记：
        /// 非主线程或空标题时拿到的是 <c>default</c>，<see cref="Dispose"/> 什么都不做，
        /// 于是调用方永远可以无条件 <c>using</c>。
        /// </summary>
        internal readonly struct Scope : IDisposable
        {
            readonly bool active;

            internal Scope(bool active) => this.active = active;

            public void Dispose()
            {
                if (active)
                {
                    Pop();
                }
            }
        }
    }
}
