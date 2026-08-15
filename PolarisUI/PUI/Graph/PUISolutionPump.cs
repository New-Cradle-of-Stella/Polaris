using System.Collections.Generic;
using UnityEngine;

namespace Polaris.PUI
{
    /// <summary>
    /// 每帧统一轮询取消/ESC 输入并分发给所有存活的 <see cref="PUISolution"/>。
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
