using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using Polaris.PUI.Wire;
using UnityEngine;

namespace Polaris.PUI.HotReload
{
    /// <summary>游戏进程侧的热重载命名管道服务端；只在有插件标了 <see cref="PUIHotFixEnabledAttribute"/> 时才启动。</summary>
    internal static class PuiHotReloadServer
    {
        /// <summary>跟 PolarisSourceCodeGenerator.PUI.PuiVisualEditor.HotReload.PuiHotReloadClient.PipeName 保持一致。</summary>
        public const string PipeName = "Polaris.PUI.HotReload";

        private static Thread thread;
        private static volatile bool running;

        public static void Start(GameObject root)
        {
            if (thread != null)
            {
                return;
            }

            PuiHotReloadPump.EnsureInstance(root);

            running = true;
            thread = new Thread(Loop)
            {
                IsBackground = true,
                Name = "Polaris.PUI.HotReloadServer",
            };
            thread.Start();
        }

        /// <summary>由 <see cref="PuiHotReloadPump"/> 在退出时调用；用一次假连接顶开阻塞中的 <see cref="NamedPipeServerStream.WaitForConnection"/>，让后台线程能退出。</summary>
        public static void Stop()
        {
            if (thread == null)
            {
                return;
            }

            running = false;

            try
            {
                using (var dummy = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut))
                {
                    dummy.Connect(200);
                }
            }
            catch
            {
                // 没有连上也没关系：说明线程本来就没卡在 WaitForConnection 里（比如正在处理另一个连接）。
            }

            thread.Join(TimeSpan.FromSeconds(6));
            thread = null;
        }

        private static void Loop()
        {
            while (running)
            {
                try
                {
                    using (var pipe = new NamedPipeServerStream(PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.None))
                    {
                        pipe.WaitForConnection();

                        // Stop() 用假连接顶开了 WaitForConnection，不是真的热重载请求
                        if (!running)
                        {
                            break;
                        }

                        HandleConnection(pipe);
                    }
                }
                catch (Exception ex)
                {
                    if (!running)
                    {
                        break;
                    }

                    Plugin.Logger?.LogError($"[Polaris.PUI.HotReload] Pipe handling exception: {ex}");
                }
            }
        }

        private static void HandleConnection(NamedPipeServerStream pipe)
        {
            string puiName;
            List<PuiWireCommand> commands;

            using (var reader = new BinaryReader(pipe, Encoding.UTF8, leaveOpen: true))
            {
                (puiName, commands) = PuiWireReader.Read(reader);
            }

            (bool ok, string error) = PuiHotReloadPump.EnqueueAndWait(puiName, commands, TimeSpan.FromSeconds(5));

            using (var writer = new BinaryWriter(pipe, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(ok);
                writer.Write(error ?? "");
                writer.Flush();
            }
        }
    }
}
