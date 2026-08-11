using System.Collections.Generic;
using UnityEngine;

namespace Polaris.PUI
{
    /// <summary>
    /// 每帧驱动所有存活 <see cref="PUISolution"/> 的取消/ESC 轮询的挂载组件；由
    /// <see cref="PUIManager.Init"/> 在 Root 上无条件挂载一份，不依赖热重载开关——取消/ESC 触发
    /// 是通用图状态机的一部分，任何创建了 <see cref="PUISolution"/> 的 mod 都可能用到。
    /// 一个组件驱动全部实例：每帧只读一次输入，而不是每个 Solution 各读一次。
    /// </summary>
    internal sealed class PUISolutionPump : MonoBehaviour
    {
        private static readonly List<PUISolution> live = new List<PUISolution>();

        public static void EnsureInstance(GameObject root)
        {
            if (root.GetComponent<PUISolutionPump>() == null)
            {
                root.AddComponent<PUISolutionPump>();
            }
        }

        internal static void Attach(PUISolution solution)
        {
            if (!live.Contains(solution))
            {
                live.Add(solution);
            }
        }

        internal static void Detach(PUISolution solution)
        {
            live.Remove(solution);
        }

        private void Update()
        {
            if (live.Count == 0 || !MainMenuAPI.IsCancelInputPressed())
            {
                return;
            }

            for (int i = 0; i < live.Count; i++)
            {
                live[i].PollCancel();
            }
        }
    }
}
