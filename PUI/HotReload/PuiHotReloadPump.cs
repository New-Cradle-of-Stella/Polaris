using Polaris.PUI.Wire;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Polaris.PUI.HotReload
{
    /// <summary>
    /// 命名管道服务端跑在后台线程，但 <see cref="PUIManager.ApplyHotReload"/> 会碰
    /// GameObject/Unity API，必须在主线程执行。这个组件把请求排进队列，在
    /// <see cref="Update"/>（主线程）里逐个处理，处理完再唤醒等待它的后台线程。
    /// 只有 <see cref="PuiHotReloadServer"/> 真正启动时才会挂到 <see cref="PUIManager.Root"/>
    /// 上，不开热重载的场景下不会存在这个组件。
    /// </summary>
    internal sealed class PuiHotReloadPump : MonoBehaviour
    {
        private sealed class PendingRequest
        {
            public string PuiName;
            public List<PuiWireCommand> Commands;
            public readonly ManualResetEventSlim Done = new ManualResetEventSlim(false);
            public bool Ok;
            public string Error;
        }

        private static PuiHotReloadPump instance;
        private readonly ConcurrentQueue<PendingRequest> queue = new ConcurrentQueue<PendingRequest>();

        public static void EnsureInstance(GameObject root)
        {
            if (instance != null)
            {
                return;
            }

            instance = root.AddComponent<PuiHotReloadPump>();
        }

        /// <summary>由后台管道线程调用：排队等待主线程处理完这次热重载，返回结果。</summary>
        public static (bool ok, string error) EnqueueAndWait(string puiName, List<PuiWireCommand> commands, TimeSpan timeout)
        {
            if (instance == null)
            {
                return (false, "Hot reload is not ready yet (PuiHotReloadPump is not initialized)");
            }

            var request = new PendingRequest { PuiName = puiName, Commands = commands };
            instance.queue.Enqueue(request);

            if (!request.Done.Wait(timeout))
            {
                return (false, "Timed out waiting for the game main thread");
            }

            return (request.Ok, request.Error);
        }

        /// <summary>
        /// Root 是 DontDestroyOnLoad 的，只有整个游戏进程退出时才会走到这里；负责把
        /// <see cref="PuiHotReloadServer"/> 的后台管道线程收干净，避免它卡住进程退出流程。
        /// </summary>
        private void OnApplicationQuit()
        {
            PuiHotReloadServer.Stop();
        }

        private void Update()
        {
            while (queue.TryDequeue(out PendingRequest request))
            {
                try
                {
                    (request.Ok, request.Error) = PUIManager.ApplyHotReload(request.PuiName, request.Commands);
                }
                catch (Exception ex)
                {
                    request.Ok = false;
                    request.Error = ex.Message;
                }
                finally
                {
                    request.Done.Set();
                }
            }
        }
    }
}
