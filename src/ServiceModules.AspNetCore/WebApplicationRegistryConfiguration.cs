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

    /// <inheritdoc cref="ServiceCollectionRegistryConfiguration.FromAssemblyOf{T}"/>
    public WebApplicationRegistryConfiguration FromAssemblyOf<T>() {
        _configBuilder.FromAssemblyOf<T>();
        return this;
    }

    /// <inheritdoc cref="ServiceCollectionRegistryConfiguration.FromAssembliesOf(Type[])"/>
    public WebApplicationRegistryConfiguration FromAssembliesOf(params Type[] assemblyMarkers) {
        _configBuilder.FromAssembliesOf(assemblyMarkers);
        return this;
    }

    /// <inheritdoc cref="ServiceCollectionRegistryConfiguration.FromAssemblies(Assembly[])"/>
    public WebApplicationRegistryConfiguration FromAssemblies(params Assembly[] assemblies) {
        _configBuilder.FromAssemblies(assemblies);
        return this;
    }

    /// <inheritdoc cref="ServiceCollectionRegistryConfiguration.UsingProviders(object[])"/>
    public WebApplicationRegistryConfiguration UsingProviders(params object[] providers) {
        _configBuilder.UsingProviders(providers);
        return this;
    }

    /// <inheritdoc cref="ServiceCollectionRegistryConfiguration.OfTypes(Type[])"/>
    public WebApplicationRegistryConfiguration OfTypes(params Type[] registryTypes) {
        _configBuilder.OfTypes(registryTypes);
        return this;
    }

    /// <inheritdoc cref="ServiceCollectionRegistryConfiguration.From(IRegistryModule[])"/>
    public WebApplicationRegistryConfiguration From(params IRegistryModule[] registries) {
        _configBuilder.From(registries);
        return this;
    }

    internal RegistryOptions GetOptions() => _configBuilder.GetOptions();
}
