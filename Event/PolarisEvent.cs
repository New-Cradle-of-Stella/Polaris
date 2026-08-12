using System;
using System.Reflection;
using evt;
using Polaris.API;

namespace Polaris.Event
{
    /// <summary>
    /// PolarisEvent 的公开门面。<see cref="Start"/>/<see cref="Change"/> 不复用
    /// <c>PolarisAPI.Game.Events.Start/Change</c>（<c>Api\Game\PolarisGameAPI.cs:594-642</c>）——
    /// 那两个方法不检查 <c>EV.stack</c>/<c>EV.changeEvent</c> 的返回值、也不支持传参，这里直接对接
    /// 底层 <c>EV</c>，按实现计划 §5.3 的时序：解析 -> 查 registry -> 确保内容已安装 -> 调用 ->
    /// 判空/判 false -> 仅成功后才 <c>EV.evStart()</c> -> 返回 <see cref="GameEvent"/> 包装。
    /// </summary>
    public static class PolarisEvent
    {
        public static bool IsRegistered(string logicalId) => Resolve(logicalId, Assembly.GetCallingAssembly()) != null;

        public static PolarisEventReference Get(string logicalId)
        {
            var definition = Resolve(logicalId, Assembly.GetCallingAssembly());
            return definition == null ? null : new PolarisEventReference(definition.Namespace, definition.LogicalId);
        }

        public static GameEvent Start(string logicalId, params string[] args)
            => StartCore(ResolveOrThrow(logicalId, Assembly.GetCallingAssembly()), args);

        public static bool TryStart(string logicalId, out GameEvent gameEvent, params string[] args)
        {
            var definition = Resolve(logicalId, Assembly.GetCallingAssembly());
            if (definition == null)
            {
                gameEvent = null;
                return false;
            }

            try
            {
                gameEvent = StartCore(definition, args);
                return gameEvent != null;
            }
            catch (Exception)
            {
                gameEvent = null;
                return false;
            }
        }

        public static GameEvent Change(string logicalId, params string[] args)
            => ChangeCore(ResolveOrThrow(logicalId, Assembly.GetCallingAssembly()), args);

        internal static GameEvent StartByKey(string @namespace, string logicalId, string[] args)
            => StartCore(ResolveOrThrowByKey(@namespace, logicalId), args);

        internal static GameEvent ChangeByKey(string @namespace, string logicalId, string[] args)
            => ChangeCore(ResolveOrThrowByKey(@namespace, logicalId), args);

        static GameEvent StartCore(PolarisEventDefinition definition, string[] args)
        {
            PolarisEventRuntime.EnsureAllInstalled();

            object result;
            try
            {
                result = EV.stack(definition.RuntimeKey, 0, -1, args, null);
            }
            catch (Exception ex)
            {
                PolarisAPI.Errors.Report(ex, "PolarisEvent.Start");
                throw new InvalidOperationException($"The game refused to start the event: {definition.RuntimeKey}.", ex);
            }

            if (result == null)
            {
                throw new InvalidOperationException($"The game refused to start the event: {definition.RuntimeKey}.");
            }

            EV.evStart();
            return GameEvent.Wrap(definition.RuntimeKey);
        }

        static GameEvent ChangeCore(PolarisEventDefinition definition, string[] args)
        {
            PolarisEventRuntime.EnsureAllInstalled();

            bool ok;
            try
            {
                ok = EV.changeEvent(definition.RuntimeKey, 0, args);
            }
            catch (Exception ex)
            {
                PolarisAPI.Errors.Report(ex, "PolarisEvent.Change");
                throw new InvalidOperationException($"The game refused to change to the event: {definition.RuntimeKey}.", ex);
            }

            if (!ok)
            {
                throw new InvalidOperationException($"The game refused to change to the event: {definition.RuntimeKey}.");
            }

            return GameEvent.Wrap(definition.RuntimeKey);
        }

        static PolarisEventDefinition Resolve(string logicalId, Assembly callerAssembly)
        {
            string ns = PolarisEventRegistry.NamespaceOf(callerAssembly);
            if (ns == null || string.IsNullOrEmpty(logicalId))
            {
                return null;
            }

            string runtimeKey = PolarisEventId.BuildRuntimeKey(ns, logicalId);
            return PolarisEventRegistry.TryGet(runtimeKey, out var definition) ? definition : null;
        }

        static PolarisEventDefinition ResolveOrThrow(string logicalId, Assembly callerAssembly)
            => Resolve(logicalId, callerAssembly)
               ?? throw new InvalidOperationException(
                   $"No PolarisEvent named '{logicalId}' is registered for assembly "
                   + $"'{callerAssembly?.GetName().Name}'. Did the build generate a registrar for it, and did "
                   + "the plugin finish scanning?");

        static PolarisEventDefinition ResolveOrThrowByKey(string @namespace, string logicalId)
        {
            string runtimeKey = PolarisEventId.BuildRuntimeKey(@namespace, logicalId);
            return PolarisEventRegistry.TryGet(runtimeKey, out var definition)
                ? definition
                : throw new InvalidOperationException($"No PolarisEvent registered for key '{runtimeKey}'.");
        }
    }
}
