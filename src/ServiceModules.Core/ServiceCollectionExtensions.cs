using System;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ServiceModules.Internal;

namespace ServiceModules;
public static class ServiceCollectionExtensions {
    /// <summary>
    /// Applies the <see cref="IRegistryModule"/>s from the given assemblies.
    /// If no assemblies provided, the entry assembly will be scanned for <see cref="IRegistryModule"/> implementations to apply.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="assemblies">The assemblies to scan</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">
    /// When unable to instantiate an <see cref="IRegistryModule"/> implementation.
    /// </exception>
    public static IServiceCollection ApplyModules(this IServiceCollection services, params Assembly[] assemblies)
        => services.ApplyModules(config => {
            if (assemblies.Any()) {
                config.FromAssemblies(assemblies);
            }
        });

    /// <summary>
    /// Applies the <see cref="IRegistryModule"/>s according to the <see cref="ServiceCollectionModuleConfiguration"/>.
    /// If the configuration does not specify any modules, module types, or module assemblies, 
    /// the entry assembly will be scanned for <see cref="IRegistryModule"/> implementations to apply.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="moduleConfiguration">Configuration for which modules to apply</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">
    /// When the provided host environment does not implement <see cref="IHostEnvironment"/>,
    /// or when unable to instantiate an <see cref="IRegistryModule"/> implementation.
    /// </exception>
    public static IServiceCollection ApplyModules(this IServiceCollection services, Action<ServiceCollectionModuleConfiguration> moduleConfiguration) {
        var runnerConfig = new ServiceCollectionModuleConfiguration();
        moduleConfiguration(runnerConfig);
        var options = runnerConfig.GetOptions();

        InternalServiceProvider.GetModuleRunner(services).ApplyRegistries(services, options);

        return services;
    }
}
