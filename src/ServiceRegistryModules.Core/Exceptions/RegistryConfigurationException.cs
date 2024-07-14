using System;

namespace ServiceRegistryModules.Exceptions;

[Serializable]
public class RegistryConfigurationException : RegistryModuleException {
    public RegistryConfigurationException() { }
    public RegistryConfigurationException(string message) : base(message) { }
    public RegistryConfigurationException(string message, Exception inner) : base(message, inner) { }
#if !NET8_0_OR_GREATER
    protected RegistryConfigurationException(
      System.Runtime.Serialization.SerializationInfo info,
      System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
#endif
}
