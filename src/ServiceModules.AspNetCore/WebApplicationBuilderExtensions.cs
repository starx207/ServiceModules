using System.Reflection;
using Microsoft.AspNetCore.Builder;
using ServiceRegistryModules.AspNetCore;

namespace ServiceRegistryModules;
public static class WebApplicationBuilderExtensions {
    /// <summary>
    /// Applies the <see cref="IRegistryModule"/>s from the given assemblies.
    /// If no assemblies provided, the calling assembly will be scanned for <see cref="IRegistryModule"/> implementations to apply.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="assemblies">The assemblies to scan</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">
    /// When unable to instantiate an <see cref="IRegistryModule"/> implementation.
    /// </exception>
    public static void ApplyRegistries(this WebApplicationBuilder builder, params Assembly[] assemblies)
        => builder.ApplyRegistries(config => {
            if (assemblies.Any()) {
                config.FromAssemblies(assemblies);
            }
        }, Assembly.GetCallingAssembly());

    /// <summary>
    /// Applies the <see cref="IRegistryModule"/>s according to the <see cref="WebApplicationRegistryConfiguration"/>.
    /// If the configuration does not specify any registries, registry types, or registry assemblies, 
    /// the calling assembly will be scanned for <see cref="IRegistryModule"/> implementations to apply.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="registryConfiguration">Configuration for which registries to apply</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">
    /// When unable to instantiate an <see cref="IRegistryModule"/> implementation.
    /// </exception>
    public static void ApplyRegistries(this WebApplicationBuilder builder, Action<WebApplicationRegistryConfiguration> registryConfiguration)
        => builder.ApplyRegistries(registryConfiguration, Assembly.GetCallingAssembly());

    private static void ApplyRegistries(this WebApplicationBuilder builder, Action<WebApplicationRegistryConfiguration> registryConfiguration, Assembly callingAssembly)
        => builder.Services.ApplyRegistries(config => {
            config.WithDefaultAssembly(callingAssembly)
            .UsingConfigurationProvider(builder.Configuration)
            .UsingEnvironment(builder.Environment);

            var webAppConfig = new WebApplicationRegistryConfiguration(config);
            registryConfiguration(webAppConfig);
        });
}
