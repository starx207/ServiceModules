using System;
using System.Runtime.Serialization;

namespace ServiceRegistryModules.Exceptions;

[Serializable]
public class RegistryConfigurationException : RegistryModuleException {
    public RegistryConfigurationException() { }
    public RegistryConfigurationException(string message) : base(message) { }
    public RegistryConfigurationException(string message, Exception inner) : base(message, inner) { }
    protected RegistryConfigurationException(
      SerializationInfo info,
      StreamingContext context) : base(info, context) { }
}
