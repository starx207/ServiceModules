using System;

namespace ServiceRegistryModules.Exceptions;

[Serializable]
public class RegistryActivationException : RegistryModuleException {
    public RegistryActivationException() { }
    public RegistryActivationException(string message) : base(message) { }
    public RegistryActivationException(string message, Exception inner) : base(message, inner) { }
#if !NET8_0_OR_GREATER
    protected RegistryActivationException(
      System.Runtime.Serialization.SerializationInfo info,
      System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
#endif
}
