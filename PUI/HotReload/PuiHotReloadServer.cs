using Polaris.PUI.Wire;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using UnityEngine;

namespace Polaris.PUI.HotReload
{
    /// <summary>
    /// 游戏进程侧的热重载命名管道服务端。只有 <see cref="PUIManager.Init"/> 发现至少一个
    /// 程序集标了 <see cref="PUIHotFixEnabledAttribute"/> 时才会 <see cref="Start"/>；
    /// 纯 release 场景（没有任何插件开启热重载）不会创建这个线程，没有任何额外开销。
    /// 管道名字、二进制帧格式要跟 PolarisSourceCodeGenerator 项目里的 PuiHotReloadClient/
    /// PuiWireWriter 保持一致。
    /// </summary>
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

        /// <summary>
        /// 由 <see cref="PuiHotReloadPump"/> 在 OnApplicationQuit 时调用。<see cref="NamedPipeServerStream.WaitForConnection"/>
        /// 没有超时/取消参数，唯一能把它从阻塞里唤醒的办法是真的建立一次连接；否则这个后台线程会
        /// 一直卡在里面，导致 Mono 在关闭时等不到它退出，整个游戏进程挂起不退出。
        /// </summary>
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
