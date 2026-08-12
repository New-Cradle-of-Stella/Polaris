using System.Reflection;

namespace Polaris.Event
{
    /// <summary>
    /// Registration handle the scanner passes to a generated registrar, tracking the registering mod/namespace.
    /// The constructor is internal so generated code cannot forge an owner assembly.
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
