using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ServiceModules.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ServiceModules;
public static class ServiceCollectionExtensions {
    public static IServiceCollection ApplyModules(this IServiceCollection services, params Assembly[] assemblies)
        => services.ApplyModules(config => { 
            if (assemblies.Any()) {
                config.FromAssemblies(assemblies);
            } 
        });

    /// <summary>
    /// Applies the <see cref="IRegistryModule"/>s according to the <see cref="ServiceCollectionModuleConfiguration"/>.
    /// If no configuration provided, searches the entry assembly for <see cref="IRegistryModule"/> implementations and runs them.
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

        var modules = InstantiateModules(options);
        IHostEnvironment? environment = null;
        if (options.Environment is { } env) {
            if (!typeof(IHostEnvironment).IsAssignableFrom(env.GetType())) {
                throw new InvalidOperationException($"{nameof(options.Environment)} must implement {nameof(IHostEnvironment)}");
            }
            environment = (IHostEnvironment)env;
        }

        foreach (var module in modules) {
            if (module.TargetEnvironments.Count == 0
                || environment is null
                || module.TargetEnvironments.Any(target
                    => target.Equals(environment.EnvironmentName, StringComparison.OrdinalIgnoreCase))
            ) {
                module.ConfigureServices(services);
            }
        }

        return services;
    }

    private static IEnumerable<IRegistryModule> InstantiateModules(ModuleOptions options) {
        var modules = options.Modules.AsEnumerable();
        var typesToCreate = options.ModuleTypes.Except(modules.Select(m => m.GetType()));
        if (options.OnlyPublicModules) {
            typesToCreate = typesToCreate.Where(t => t.IsPublic);
        }

        if (typesToCreate.Any()) {
            modules = modules.Concat(
                typesToCreate.Select(t => FindConstructorWithArgsThatSatisfy(t, options.AllowedModuleArgTypes) is { } ctor
                    ? CreateModuleInstance(ctor, options)
                    : throw new InvalidOperationException($"Unable to activate {nameof(IRegistryModule)} of type '{t.Name}' " +
                    $"-- no suitable constructor found. " +
                    $"Allowable constructor parameters are: {string.Join(", ", options.AllowedModuleArgTypes)}"))
            );
        }

        if (!modules.Any()) {
            return Enumerable.Empty<IRegistryModule>();
        }

        modules = modules.OrderByDescending(m => m.Priority);

        return modules;
    }

    // TODO: provide an option for supressing errors
    private static ConstructorInfo? FindConstructorWithArgsThatSatisfy(Type t, IEnumerable<Type> availableArgs) {
        var availableCount = availableArgs.Count();

        return t.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Select(ctor => new { ctor, @params = ctor.GetParameters() })
            .Where(info => info.@params.Length <= availableCount) // Remove constructors with more parameters than the available args
            .Where(info => info.@params.All(p => availableArgs.Any(arg => p.ParameterType.IsAssignableFrom(arg)))) // Remove constructors with the wrong types of args
            .OrderByDescending(info => info.@params.Length) // Get constructor with longest parameter list satisfied by the available args
            .Select(info => info.ctor)
            .FirstOrDefault();
    }

    private static IRegistryModule CreateModuleInstance(
        ConstructorInfo ctor,
        ModuleOptions options
    ) {

        var ctorParams = ctor.GetParameters();
        var paramInstances = new object[ctorParams.Length];

        for (var i = 0; i < ctorParams.Length; i++) {
            paramInstances[i] = options.Providers.FirstOrDefault(provider => provider.GetType() == ctorParams[i].ParameterType)
                ?? options.Providers.FirstOrDefault(provider => ctorParams[i].ParameterType.IsAssignableFrom(provider.GetType()))
                ?? throw new InvalidOperationException($"Parameter type {ctorParams[i].ParameterType.Name} not supported for {nameof(IRegistryModule)}");
        }

        return (IRegistryModule)ctor.Invoke(paramInstances);
    }
}
