using System.Reflection;

namespace Polaris.Event
{
    /// <summary>
    /// 扫描器传给生成 registrar 的注册句柄：携带"这是哪个模组、哪个命名空间在注册"。构造函数是
    /// internal——生成代码只能拿到扫描器给它的这一份 context，无法自己构造一个假冒别的程序集的实例，
    /// 因此生成代码不可能伪造 owner。
    /// </summary>
    public sealed class PolarisEventRegistrationContext
    {
        public string Namespace { get; }
        public Assembly OwnerAssembly { get; }

        internal PolarisEventRegistrationContext(string @namespace, Assembly ownerAssembly)
        {
            Namespace = @namespace;
            OwnerAssembly = ownerAssembly;
        }

        public void Register(string logicalId, string commandText, string sourcePath, string contentHash)
        {
            string runtimeKey = PolarisEventId.BuildRuntimeKey(Namespace, logicalId);
            var definition = new PolarisEventDefinition(Namespace, logicalId, runtimeKey, commandText, sourcePath, contentHash, OwnerAssembly);
            PolarisEventRegistry.Register(definition);
        }
    }
}
