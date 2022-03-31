namespace ServiceModules.Internal;

internal interface IModuleConfigLoader {
    ModuleConfiguration? LoadFrom(ModuleOptions options);
}
