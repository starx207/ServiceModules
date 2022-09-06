using System;
using System.Runtime.Serialization;

namespace ServiceRegistryModules.Exceptions;

[Serializable]
public abstract class RegistryModuleException : Exception {
    public RegistryModuleException() { }
    public RegistryModuleException(string message) : base(message) { }
    public RegistryModuleException(string message, Exception inner) : base(message, inner) { }
    protected RegistryModuleException(
      SerializationInfo info,
      StreamingContext context) : base(info, context) { }
}
