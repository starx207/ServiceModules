using System;

namespace ServiceRegistryModules.Exceptions;

[Serializable]
public abstract class RegistryModuleException : Exception {
    public RegistryModuleException() { }
    public RegistryModuleException(string message) : base(message) { }
    public RegistryModuleException(string message, Exception inner) : base(message, inner) { }
#if !NET8_0_OR_GREATER
    protected RegistryModuleException(
      System.Runtime.Serialization.SerializationInfo info,
      System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
#endif
}
