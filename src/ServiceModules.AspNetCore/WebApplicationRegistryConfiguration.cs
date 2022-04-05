using System.Reflection;
using ServiceRegistryModules.Internal;

namespace ServiceRegistryModules.AspNetCore;
// TODO: I don't really like doing it like this. How else might this be achieved?
public class WebApplicationRegistryConfiguration {
    private readonly ServiceCollectionRegistryConfiguration _configBuilder;

    public WebApplicationRegistryConfiguration(ServiceCollectionRegistryConfiguration configBuilder) => _configBuilder = configBuilder;

    /// <inheritdoc cref="ServiceCollectionRegistryConfiguration.WithConfigurationsFromSection(string)"/>
    public WebApplicationRegistryConfiguration WithConfigurationsFromSection(string sectionKey) {
        _configBuilder.WithConfigurationsFromSection(sectionKey);
        return this;
    }

    /// <inheritdoc cref="ServiceCollectionRegistryConfiguration.PublicOnly"/>
    public WebApplicationRegistryConfiguration PublicOnly() {
        _configBuilder.PublicOnly();
        return this;
    }

    /// <inheritdoc cref="ServiceCollectionRegistryConfiguration.FromAssemblies(Type[])"/>
    public WebApplicationRegistryConfiguration FromAssemblies(params Type[] assemblyMarkers) {
        _configBuilder.FromAssemblies(assemblyMarkers);
        return this;
    }

    /// <inheritdoc cref="ServiceCollectionRegistryConfiguration.FromAssemblies(Assembly[])"/>
    public WebApplicationRegistryConfiguration FromAssemblies(params Assembly[] assemblies) {
        _configBuilder.FromAssemblies(assemblies);
        return this;
    }

    /// <inheritdoc cref="ServiceCollectionRegistryConfiguration.WithProviders(object[])"/>
    public WebApplicationRegistryConfiguration WithProviders(params object[] providers) {
        _configBuilder.WithProviders(providers);
        return this;
    }

    /// <inheritdoc cref="ServiceCollectionRegistryConfiguration.UsingRegistries(Type[])"/>
    public WebApplicationRegistryConfiguration UsingRegistries(params Type[] registryTypes) {
        _configBuilder.UsingRegistries(registryTypes);
        return this;
    }

    /// <inheritdoc cref="ServiceCollectionRegistryConfiguration.UsingRegistries(IRegistryModule[])"/>
    public WebApplicationRegistryConfiguration UsingRegistries(params IRegistryModule[] registries) {
        _configBuilder.UsingRegistries(registries);
        return this;
    }

    internal RegistryOptions GetOptions() => _configBuilder.GetOptions();
}
