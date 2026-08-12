using System;

namespace Polaris.Event
{
    /// <summary>
    /// Marks a generated registrar class with the owning project's <c>PolarisEventNamespace</c>,
    /// keeping the namespace out of <see cref="IPolarisEventRegistrar.Register"/>'s signature.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class PolarisEventAutoRegistrationAttribute : Attribute
    {
        public string Namespace { get; }

        public PolarisEventAutoRegistrationAttribute(string @namespace)
        {
            Namespace = @namespace;
        }
    }
}
