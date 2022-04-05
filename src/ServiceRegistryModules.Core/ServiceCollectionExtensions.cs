using System;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using ServiceRegistryModules.Internal;

namespace ServiceRegistryModules;
public static class ServiceCollectionExtensions {
    /// <summary>
    /// Applies the <see cref="IRegistryModule"/>s from the given assemblies.
    /// If no assemblies provided, the calling assembly will be scanned for <see cref="IRegistryModule"/> implementations to apply.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="assemblies">The assemblies to scan</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">
    /// When unable to instantiate an <see cref="IRegistryModule"/> implementation
    /// or when any registry configurations are invalid.
    /// </exception>
    /// <exception cref="ArgumentException">When registry configuration entry has an invalid value for a property</exception>
    public static IServiceCollection ApplyRegistries(this IServiceCollection services, params Assembly[] assemblies)
        => services.ApplyRegistries(config => {
            if (assemblies.Any()) {
                config.FromAssemblies(assemblies);
            }
        }, Assembly.GetCallingAssembly());

    /// <summary>
    /// Applies the <see cref="IRegistryModule"/>s according to the <see cref="ServiceCollectionRegistryConfiguration"/>.
    /// If the configuration does not specify any registries, registry types, or registry assemblies, 
    /// the calling assembly will be scanned for <see cref="IRegistryModule"/> implementations to apply.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="registryConfiguration">Configuration for which registries to apply</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">
    /// When unable to instantiate an <see cref="IRegistryModule"/> implementation
    /// or when any registry configurations are invalid.
    /// </exception>
    /// <exception cref="ArgumentException">When registry configuration entry has an invalid value for a property</exception>
    public static IServiceCollection ApplyRegistries(this IServiceCollection services, Action<ServiceCollectionRegistryConfiguration> registryConfiguration)
        => services.ApplyRegistries(registryConfiguration, Assembly.GetCallingAssembly());

    private static IServiceCollection ApplyRegistries(this IServiceCollection services, Action<ServiceCollectionRegistryConfiguration> registryConfiguration, Assembly callingAssembly) {
        var runnerConfig = new ServiceCollectionRegistryConfiguration();
        runnerConfig.WithDefaultAssembly(callingAssembly);
        registryConfiguration(runnerConfig);
        var options = runnerConfig.GetOptions();

        InternalServiceProvider.GetRegistryRunner().ApplyRegistries(services, options);

        return services;
    }
}
