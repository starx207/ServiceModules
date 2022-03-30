using System.Reflection;
using Microsoft.AspNetCore.Builder;
using ServiceModules.AspNetCore;

namespace ServiceModules;
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
    public static void ApplyModules(this WebApplicationBuilder builder, params Assembly[] assemblies)
        => builder.ApplyModules(config => {
            if (assemblies.Any()) {
                config.FromAssemblies(assemblies);
            }
        }, Assembly.GetCallingAssembly());

    /// <summary>
    /// Applies the <see cref="IRegistryModule"/>s according to the <see cref="WebApplicationModuleConfiguration"/>.
    /// If the configuration does not specify any modules, module types, or module assemblies, 
    /// the calling assembly will be scanned for <see cref="IRegistryModule"/> implementations to apply.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="moduleConfiguration">Configuration for which modules to apply</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">
    /// When unable to instantiate an <see cref="IRegistryModule"/> implementation.
    /// </exception>
    public static void ApplyModules(this WebApplicationBuilder builder, Action<WebApplicationModuleConfiguration> moduleConfiguration)
        => builder.ApplyModules(moduleConfiguration, Assembly.GetCallingAssembly());

    private static void ApplyModules(this WebApplicationBuilder builder, Action<WebApplicationModuleConfiguration> moduleConfiguration, Assembly callingAssembly)
        => builder.Services.ApplyModules(config => {
            config.WithDefaultAssembly(callingAssembly)
            .WithConfiguration(builder.Configuration)
            .WithEnvironment(builder.Environment);

            var webAppConfig = new WebApplicationModuleConfiguration(config);
            moduleConfiguration(webAppConfig);
        });
}
