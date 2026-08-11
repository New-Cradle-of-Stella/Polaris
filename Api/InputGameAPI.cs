using System;

namespace Polaris.API
{
    /// <summary>
    /// 玩家输入的只读查询。
    /// <para>
    /// 只暴露<b>游戏动作</b>（跳跃、攻击、菜单……），不暴露 Windows 虚拟键码。旧 LuaAiC 的
    /// <c>GetKeyState(vk)</c> 那一路有三个问题：绕过玩家自己的改键设置、把 Windows 专有协议
    /// 焊进了内容文件、手柄的 XInput/DInput 编号还各是一套。按动作查则天然跟着玩家的键位走，
    /// 键盘和手柄也是同一份代码。
    /// </para>
    /// <para>
    /// 实现上读的是游戏为每个动作维护的一个 float（<c>KEY.mv*</c>），语义是：<c>&gt; 0</c> 表示
    /// 正被按住、数值等于已按住的帧数；<c>== 1</c> 就是"这一帧刚按下"；<c>&lt; 0</c> 表示刚松开、
    /// 绝对值是松开后的帧数。本类把这套约定翻译成 <see cref="IsDown"/>/<see cref="WasPressed"/>/
    /// <see cref="WasReleased"/>，调用方不必知道 mv 值这回事。
    /// </para>
    /// </summary>
    public sealed class InputGameAPI
    {
        /// <summary>这一帧这个动作是不是被按住。</summary>
        public bool IsDown(GameInputAction action) => Value(action) > 0f;

        /// <summary>这一帧是不是刚按下（按下沿）。跨帧只会为真一次。</summary>
        public bool WasPressed(GameInputAction action)
        {
            float v = Value(action);
            return v > 0f && v <= 1f;
        }

        /// <summary>这一帧是不是刚松开（松开沿）。</summary>
        public bool WasReleased(GameInputAction action)
        {
            float v = Value(action);
            return v < 0f && v > -1024f && v >= -1f;
        }

        /// <summary>这个动作已经被按住了多少帧；没按住返回 0。长按判定用它。</summary>
        public float HeldFrames(GameInputAction action)
        {
            float v = Value(action);
            return v > 0f ? v : 0f;
        }

        /// <summary>方向输入合成的一个向量，X/Y 各取 -1/0/1。</summary>
        public GameVector2 DirectionAxis()
        {
            float x = (IsDown(GameInputAction.Right) ? 1f : 0f) - (IsDown(GameInputAction.Left) ? 1f : 0f);
            float y = (IsDown(GameInputAction.Down) ? 1f : 0f) - (IsDown(GameInputAction.Up) ? 1f : 0f);
            return new GameVector2(x, y);
        }

        /// <summary>鼠标位置（游戏的 GUI 坐标系，与 <c>XX.IN</c> 的 1280×720 基准一致）。</summary>
        public GameVector2 MousePosition
        {
            get
            {
                try
                {
                    return XX.IN.Mouse;
                }
                catch (Exception)
                {
                    return GameVector2.Zero;
                }
            }
        }

        /// <summary>本帧滚轮增量。</summary>
        public GameVector2 MouseWheelDelta
        {
            get
            {
                try
                {
                    return XX.IN.MouseWheel;
                }
                catch (Exception)
                {
                    return GameVector2.Zero;
                }
            }
        }

        /// <summary>
        /// 取某个动作的原始 mv 值。取不到按键对象（游戏还没起来、正在重建键位）时返回 0，
        /// 也就是"什么都没按"——这比抛异常合理：输入查询在任何时刻都应该能安全地问一句。
        /// </summary>
        static float Value(GameInputAction action)
        {
            XX.KEY KA = GameBinding.KeyAssign;
            if (KA == null)
            {
                return 0f;
            }

            try
            {
                switch (action)
                {
                    case GameInputAction.Left: return KA.mvLA;
                    case GameInputAction.Right: return KA.mvRA;
                    case GameInputAction.Up: return KA.mvTA;
                    case GameInputAction.Down: return KA.mvBA;
                    case GameInputAction.Jump: return KA.mvJUMP;
                    case GameInputAction.Run: return KA.mvRUN;
                    case GameInputAction.Check: return KA.mvCHECK;
                    case GameInputAction.Menu: return KA.mvMENU;
                    case GameInputAction.Submit: return KA.mvSUBMIT;
                    case GameInputAction.Cancel: return KA.mvCANCEL;
                    case GameInputAction.TabLeft: return KA.mvLTAB;
                    case GameInputAction.TabRight: return KA.mvRTAB;
                    case GameInputAction.Add: return KA.mvADD;
                    case GameInputAction.Remove: return KA.mvREM;
                    case GameInputAction.ButtonZ: return KA.mvZ;
                    case GameInputAction.ButtonX: return KA.mvX;
                    case GameInputAction.ButtonC: return KA.mvC;
                    case GameInputAction.ButtonA: return KA.mvA;
                    case GameInputAction.ButtonS: return KA.mvS;
                    case GameInputAction.ButtonD: return KA.mvD;
                    case GameInputAction.Shift: return KA.mvLSH;
                    default: return 0f;
                }
            }
            catch (Exception)
            {
                return 0f;
            }
        }
    }

    /// <summary>
    /// 游戏动作。名字对应游戏内部按键映射对象上的动作槽，含义以玩家的键位设置为准。
    /// <para>
    /// <c>ButtonZ</c>–<c>ButtonD</c> 这几个保留了游戏内部的字母命名而不是猜一个玩法含义
    /// （"攻击""魔法"……）：这些槽在不同场景下承载的动作并不固定，起一个看起来很确定的名字
    /// 反而会让调用方写出在别的场景里不成立的假设。
    /// </para>
    /// </summary>
    public enum GameInputAction
    {
        Left,
        Right,
        Up,
        Down,
        Jump,
        Run,
        Check,
        Menu,
        Submit,
        Cancel,
        TabLeft,
        TabRight,
        Add,
        Remove,
        ButtonZ,
        ButtonX,
        ButtonC,
        ButtonA,
        ButtonS,
        ButtonD,
        Shift,
    }
}
