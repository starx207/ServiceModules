using System;

namespace ServiceRegistryModules.Exceptions;

[Serializable]
public abstract class RegistryModuleException : Exception {
    public RegistryModuleException() { }
    public RegistryModuleException(string message) : base(message) { }
    public RegistryModuleException(string message, Exception inner) : base(message, inner) { }
#if !NET8_0_OR_GREATER
    [Obsolete("This constructor is obsolete and will be removed in a future version. Use the constructor with the 'message' and 'inner' parameters instead.")]
    protected RegistryModuleException(
      System.Runtime.Serialization.SerializationInfo info,
      System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
#endif
}
