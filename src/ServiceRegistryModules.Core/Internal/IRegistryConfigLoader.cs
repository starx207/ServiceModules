namespace ServiceRegistryModules.Internal;

internal interface IRegistryConfigLoader {
    RegistryConfiguration? LoadFrom(RegistryOptions options);
}
