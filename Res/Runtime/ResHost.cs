using UnityEngine;

namespace Polaris.Res.Runtime
{
    /// <summary>
    /// PolarisRes 的常驻宿主：一个 <c>DontDestroyOnLoad</c> 的根 GameObject，
    /// 挂载 <see cref="ResPump"/>。所有需要"每帧跑一次"或"需要一个 MonoBehaviour 才能
    /// 启动协程/播放音频视频"的子系统都挂在它下面的子物体上，而不是各自到处找宿主。
    /// </summary>
    internal static class ResHost
    {
        private static GameObject root;
        private static ResPump pump;

        internal static ResPump Pump => pump;

        internal static GameObject Root
        {
            get
            {
                EnsureCreated();
                return root;
            }
        }

        /// <summary>幂等：重复调用不会重复创建。</summary>
        internal static void EnsureCreated()
        {
            if (root != null) { return; }

            root = new GameObject("PolarisRes");
            Object.DontDestroyOnLoad(root);
            pump = root.AddComponent<ResPump>();
        }
    }
}
