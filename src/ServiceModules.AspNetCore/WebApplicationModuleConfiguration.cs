using ServiceModules.Internal;
using System.Reflection;

namespace ServiceModules.AspNetCore;
// TODO: I don't really like doing it like this. How else might this be achieved?
public class WebApplicationModuleConfiguration {
    private readonly ServiceCollectionModuleConfiguration _configBuilder;

    public WebApplicationModuleConfiguration(ServiceCollectionModuleConfiguration configBuilder) => _configBuilder = configBuilder;

    /// <inheritdoc cref="ServiceCollectionModuleConfiguration.PublicOnly"/>
    public WebApplicationModuleConfiguration PublicOnly() {
        _configBuilder.PublicOnly();
        return this;
    }

    /// <inheritdoc cref="ServiceCollectionModuleConfiguration.FromAssemblies(Type[])"/>
    public WebApplicationModuleConfiguration FromAssemblies(params Type[] assemblyMarkers) {
        _configBuilder.FromAssemblies(assemblyMarkers);
        return this;
    }

    /// <inheritdoc cref="ServiceCollectionModuleConfiguration.FromAssemblies(Assembly[])"/>
    public WebApplicationModuleConfiguration FromAssemblies(params Assembly[] assemblies) {
        _configBuilder.FromAssemblies(assemblies);
        return this;
    }

    /// <inheritdoc cref="ServiceCollectionModuleConfiguration.WithProviders(object[])"/>
    public WebApplicationModuleConfiguration WithProviders(params object[] providers) {
        _configBuilder.WithProviders(providers);
        return this;
    }

    /// <inheritdoc cref="ServiceCollectionModuleConfiguration.UsingModules(Type[])"/>
    public WebApplicationModuleConfiguration UsingModules(params Type[] moduleTypes) {
        _configBuilder.UsingModules(moduleTypes);
        return this;
    }

    /// <inheritdoc cref="ServiceCollectionModuleConfiguration.UsingModules(IRegistryModule[])"/>
    public WebApplicationModuleConfiguration UsingModules(params IRegistryModule[] modules) {
        _configBuilder.UsingModules(modules);
        return this;
    }

    internal ModuleOptions GetOptions() => _configBuilder.GetOptions();
}
