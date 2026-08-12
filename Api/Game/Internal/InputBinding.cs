using System;

namespace Polaris.API
{
    /// <summary>
    /// 输入动作到游戏内部按键槽（<c>KEY.mv*</c>）的映射，并逐帧记录按住时长供松开时读取。
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

        /// <summary>取某个动作的原始 mv 值；键位对象不可用时返回 0（视为未按下）而不抛异常。</summary>
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

        /// <summary>每帧调用：记账按住时长并发布按下/释放回调；用轮询而非打补丁，因为输入来源不止一处。</summary>
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
