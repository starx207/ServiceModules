using Microsoft.AspNetCore.Builder;
using ServiceModules.AspNetCore;
using System.Reflection;

namespace ServiceModules;
public static class WebApplicationBuilderExtensions {
    public static void ApplyModules(this WebApplicationBuilder builder, params Assembly[] assemblies)
        => builder.ApplyModules(config => {
            if (assemblies.Any()) {
                config.FromAssemblies(assemblies);
            }
        });

    /// <summary>
    /// Applies the <see cref="IRegistryModule"/>s according to the <see cref="ServiceCollectionModuleConfiguration"/>.
    /// If no configuration provided, searches the entry assembly for <see cref="IRegistryModule"/> implementations and runs them.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="moduleConfiguration">Configuration for which modules to apply</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">
    /// When the provided host environment does not implement <see cref="IHostEnvironment"/>,
    /// or when unable to instantiate an <see cref="IRegistryModule"/> implementation.
    /// </exception>
    public static void ApplyModules(this WebApplicationBuilder builder, Action<WebApplicationModuleConfiguration> moduleConfiguration) 
        => builder.Services.ApplyModules(config => {
            config.WithEnvironment(builder.Environment)
            .WithConfiguration(builder.Configuration);

            var webAppConfig = new WebApplicationModuleConfiguration(config);
            moduleConfiguration(webAppConfig);
        });
}
