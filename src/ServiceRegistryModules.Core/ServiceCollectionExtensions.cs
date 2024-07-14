using System;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ServiceRegistryModules.Exceptions;
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
    /// <exception cref="RegistryActivationException">
    /// When unable to instantiate an <see cref="IRegistryModule"/> implementation
    /// or when any registry configurations are invalid.
    /// </exception>
    /// <exception cref="RegistryConfigurationException">When there is a problem with the registry configuration</exception>
    public static IServiceCollection ApplyRegistries(this IServiceCollection services, params Assembly[] assemblies)
        => services.ApplyRegistries(config => {
            if (assemblies.Length > 0) {
                config.FromAssemblies(assemblies);
            }
        }, Assembly.GetCallingAssembly());

    /// <summary>
    /// Applies the <see cref="IRegistryModule"/>s according to the <see cref="FullServiceCollectionRegistryConfiguration"/>.
    /// If the configuration does not specify any registries, registry types, or registry assemblies, 
    /// the calling assembly will be scanned for <see cref="IRegistryModule"/> implementations to apply.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="registryConfiguration">Configuration for which registries to apply</param>
    /// <returns></returns>
    /// <exception cref="RegistryActivationException">
    /// When unable to instantiate an <see cref="IRegistryModule"/> implementation
    /// or when any registry configurations are invalid.
    /// </exception>
    /// <exception cref="RegistryConfigurationException">When there is a problem with the registry configuration</exception>
    public static IServiceCollection ApplyRegistries(this IServiceCollection services, Action<FullServiceCollectionRegistryConfiguration> registryConfiguration)
        => services.ApplyRegistries(registryConfiguration, Assembly.GetCallingAssembly());

    /// <summary>
    /// Applies the <see cref="IRegistryModule"/>s from the given assemblies.
    /// If no assemblies provided, the calling assembly will be scanned for <see cref="IRegistryModule"/> implementations to apply.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="context">The context from which to obtain the <see cref="IHostingEnvironment"/> and <see cref="IConfiguration"/></param>
    /// <param name="assemblies">The assemblies to scan</param>
    /// <returns></returns>
    /// <exception cref="RegistryActivationException">
    /// When unable to instantiate an <see cref="IRegistryModule"/> implementation
    /// or when any registry configurations are invalid.
    /// </exception>
    /// <exception cref="RegistryConfigurationException">When there is a problem with the registry configuration</exception>
    public static IServiceCollection ApplyRegistries(this IServiceCollection services, HostBuilderContext context, params Assembly[] assemblies)
        => services.ApplyRegistries(config => {
            if (assemblies.Length > 0) {
                config.FromAssemblies(assemblies);
            }
            config.UsingConfiguration(context.Configuration);
            config.UsingEnvironment(context.HostingEnvironment);
        }, Assembly.GetCallingAssembly());

    /// <summary>
    /// Applies the <see cref="IRegistryModule"/>s according to the <see cref="ServiceCollectionRegistryConfiguration"/>.
    /// If the configuration does not specify any registries, registry types, or registry assemblies, 
    /// the calling assembly will be scanned for <see cref="IRegistryModule"/> implementations to apply.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="context">The context from which to obtain the <see cref="IHostingEnvironment"/> and <see cref="IConfiguration"/></param>
    /// <param name="registryConfiguration">Configuration for which registries to apply</param>
    /// <returns></returns>
    /// <exception cref="RegistryActivationException">
    /// When unable to instantiate an <see cref="IRegistryModule"/> implementation
    /// or when any registry configurations are invalid.
    /// </exception>
    /// <exception cref="RegistryConfigurationException">When there is a problem with the registry configuration</exception>
    public static IServiceCollection ApplyRegistries(this IServiceCollection services, HostBuilderContext context, Action<ServiceCollectionRegistryConfiguration> registryConfiguration) 
        => services.ApplyRegistries(config => {
            config.UsingConfiguration(context.Configuration);
            config.UsingEnvironment(context.HostingEnvironment);
            registryConfiguration(config);
        }, Assembly.GetCallingAssembly());

    internal static IServiceCollection ApplyRegistries(this IServiceCollection services, Action<FullServiceCollectionRegistryConfiguration> registryConfiguration, Assembly callingAssembly) {
        var runnerConfig = new FullServiceCollectionRegistryConfiguration();
        runnerConfig.WithDefaultAssembly(callingAssembly);
        registryConfiguration(runnerConfig);
        var options = runnerConfig.GetOptions();

        InternalServiceProvider.GetRegistryRunner().ApplyRegistries(services, options);

        return services;
    }
}
