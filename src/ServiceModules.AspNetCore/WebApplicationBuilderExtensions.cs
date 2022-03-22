using System.Reflection;
using Microsoft.AspNetCore.Builder;
using ServiceModules.AspNetCore;

namespace ServiceModules;
public static class WebApplicationBuilderExtensions {
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
    public static void ApplyModules(this WebApplicationBuilder builder, params Assembly[] assemblies)
        => builder.ApplyModules(config => {
            if (assemblies.Any()) {
                config.FromAssemblies(assemblies);
            }
        });

    /// <summary>
    /// Applies the <see cref="IRegistryModule"/>s according to the <see cref="WebApplicationModuleConfiguration"/>.
    /// If the configuration does not specify any modules, module types, or module assemblies, 
    /// the entry assembly will be scanned for <see cref="IRegistryModule"/> implementations to apply.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="moduleConfiguration">Configuration for which modules to apply</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">
    /// When unable to instantiate an <see cref="IRegistryModule"/> implementation.
    /// </exception>
    public static void ApplyModules(this WebApplicationBuilder builder, Action<WebApplicationModuleConfiguration> moduleConfiguration)
        => builder.Services.ApplyModules(config => {
            config.WithEnvironment(builder.Environment)
            .WithConfiguration(builder.Configuration);

            var webAppConfig = new WebApplicationModuleConfiguration(config);
            moduleConfiguration(webAppConfig);
        });
}
