using System.Reflection;

namespace Polaris.Event
{
    /// <summary>一个已注册事件的完整记账信息：谁注册的、注册成什么样。</summary>
    public sealed class PolarisEventDefinition
    {
        public string Namespace { get; }
        public string LogicalId { get; }
        public string RuntimeKey { get; }
        public string CommandText { get; }
        public string SourcePath { get; }
        public string ContentHash { get; }
        public Assembly OwnerAssembly { get; }

        public PolarisEventDefinition(
            string @namespace,
            string logicalId,
            string runtimeKey,
            string commandText,
            string sourcePath,
            string contentHash,
            Assembly ownerAssembly)
        {
            Namespace = @namespace;
            LogicalId = logicalId;
            RuntimeKey = runtimeKey;
            CommandText = commandText;
            SourcePath = sourcePath;
            ContentHash = contentHash;
            OwnerAssembly = ownerAssembly;
        }
    }
}
