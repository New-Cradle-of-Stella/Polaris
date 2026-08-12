using System;

namespace Polaris.API
{
    /// <summary>
    /// 输入动作到游戏内部按键槽的映射，以及"这次按住持续了多少帧"的记账。
    /// <para>
    /// 游戏为每个动作维护一个 float（<c>KEY.mv*</c>），语义是：<c>&gt; 0</c> 表示正被按住、
    /// 数值等于已按住的帧数；<c>== 1</c> 就是"这一帧刚按下"；<c>&lt; 0</c> 表示刚松开、
    /// 绝对值是松开后的帧数。松开之后按住时长就查不到了，所以这里每帧记一份，
    /// 供 <c>WasReleased(action, heldFrames)</c> 判断"轻点还是长按后松开"。
    /// </para>
    /// </summary>
    internal static class InputBinding
    {
        static readonly GameInputAction[] AllActions = (GameInputAction[])Enum.GetValues(typeof(GameInputAction));

        /// <summary>每个动作上一次按住结束时的持续帧数。</summary>
        static readonly int[] lastHeld = new int[AllActions.Length];

        /// <summary>每个动作这一帧的按住帧数，用来在松开的那一帧把上面那份定格下来。</summary>
        static readonly int[] currentHeld = new int[AllActions.Length];

        /// <summary>上一帧的按下状态，用来发布 <c>ActionPressed</c>/<c>ActionReleased</c> 两条静态回调。</summary>
        static readonly bool[] wasDown = new bool[AllActions.Length];

        internal static int LastHeldFrames(GameInputAction action)
        {
            int i = (int)action;
            return i >= 0 && i < lastHeld.Length ? lastHeld[i] : 0;
        }

        /// <summary>
        /// 取某个动作的原始 mv 值。取不到按键对象（游戏还没起来、正在重建键位）时返回 0，
        /// 也就是"什么都没按"——这比抛异常合理：输入查询在任何时刻都应该能安全地问一句。
        /// </summary>
        internal static float Value(GameInputAction action)
        {
            XX.KEY ka = GameBinding.KeyAssign;
            if (ka == null)
            {
                return 0f;
            }

            try
            {
                switch (action)
                {
                    case GameInputAction.Left: return ka.mvLA;
                    case GameInputAction.Right: return ka.mvRA;
                    case GameInputAction.Up: return ka.mvTA;
                    case GameInputAction.Down: return ka.mvBA;
                    case GameInputAction.Jump: return ka.mvJUMP;
                    case GameInputAction.Run: return ka.mvRUN;
                    case GameInputAction.Check: return ka.mvCHECK;
                    case GameInputAction.Menu: return ka.mvMENU;
                    case GameInputAction.Submit: return ka.mvSUBMIT;
                    case GameInputAction.Cancel: return ka.mvCANCEL;
                    case GameInputAction.TabLeft: return ka.mvLTAB;
                    case GameInputAction.TabRight: return ka.mvRTAB;
                    case GameInputAction.Add: return ka.mvADD;
                    case GameInputAction.Remove: return ka.mvREM;
                    case GameInputAction.ButtonZ: return ka.mvZ;
                    case GameInputAction.ButtonX: return ka.mvX;
                    case GameInputAction.ButtonC: return ka.mvC;
                    case GameInputAction.ButtonA: return ka.mvA;
                    case GameInputAction.ButtonS: return ka.mvS;
                    case GameInputAction.ButtonD: return ka.mvD;
                    case GameInputAction.Shift: return ka.mvLSH;
                    default: return 0f;
                }
            }
            catch (Exception)
            {
                return 0f;
            }
        }

        /// <summary>
        /// 每帧调用：记账按住时长，并发布按下/释放两条静态回调。
        /// <para>
        /// 走轮询而不是给输入系统打补丁：游戏把输入写进这些字段的位置不止一处（键盘、手柄、
        /// 事件模拟按键），而读一遍字段就能得到最终结果。
        /// </para>
        /// </summary>
        internal static void Pump()
        {
            for (int i = 0; i < AllActions.Length; i++)
            {
                GameInputAction action = AllActions[i];
                float v = Value(action);
                bool down = v > 0f;

                if (down)
                {
                    currentHeld[i] = (int)v;
                }

                if (down == wasDown[i])
                {
                    continue;
                }

                wasDown[i] = down;

                if (down)
                {
                    GameCallbackHub.PublishStatic(
                        GameStaticCallbackKind.ActionPressed, () => new ActionPressedCallbackData(action));
                }
                else
                {
                    int held = currentHeld[i];
                    lastHeld[i] = held;
                    currentHeld[i] = 0;
                    GameCallbackHub.PublishStatic(
                        GameStaticCallbackKind.ActionReleased, () => new ActionReleasedCallbackData(action, held));
                }
            }
        }
    }
}
