namespace ServiceModules.Internal;

internal interface IModuleConfigApplicator {
    void ApplyModuleConfiguration(IRegistryModule module);
    void InitializeFrom(ModuleOptions options);
}