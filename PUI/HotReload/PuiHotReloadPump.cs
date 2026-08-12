using Polaris.PUI.Wire;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Polaris.PUI.HotReload
{
    /// <summary>把后台管道线程的热重载请求排队，在主线程 <see cref="Update"/> 里处理后唤醒等待方（Unity API 只能在主线程调用）。</summary>
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

        /// <summary>进程退出时清理 <see cref="PuiHotReloadServer"/> 的后台管道线程，避免卡住退出流程。</summary>
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
