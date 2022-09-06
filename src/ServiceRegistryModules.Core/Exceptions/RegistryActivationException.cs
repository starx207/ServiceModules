using System;
using System.Runtime.Serialization;

namespace ServiceRegistryModules.Exceptions;

[Serializable]
public class RegistryActivationException : RegistryModuleException {
    public RegistryActivationException() { }
    public RegistryActivationException(string message) : base(message) { }
    public RegistryActivationException(string message, Exception inner) : base(message, inner) { }
    protected RegistryActivationException(
      SerializationInfo info,
      StreamingContext context) : base(info, context) { }
}
