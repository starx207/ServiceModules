namespace ServiceRegistryModules.Internal;

internal interface IRegistryConfigApplicator {
    void ApplyRegistryConfiguration(IRegistryModule registry);
    void InitializeFrom(RegistryOptions options);
}
