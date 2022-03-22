using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ServiceModules.Internal;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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

        var moduleConfig = options.Configuration?.GetModuleConfig(options.ModuleConfigSectionKey);

        var modules = InstantiateModules(options);
        if (options.Environment is { } environment) {
            if (!typeof(IHostEnvironment).IsAssignableFrom(environment.GetType())) {
                throw new InvalidOperationException($"{nameof(options.Environment)} must implement {nameof(IHostEnvironment)}");
            }
            var envName = ((IHostEnvironment)environment).EnvironmentName;

            modules = modules.Where(module => module.TargetEnvironments.Count == 0
                || module.TargetEnvironments.Any(target
                    => target.Equals(envName, StringComparison.OrdinalIgnoreCase)));
        }

        foreach (var module in modules) {
            if (moduleConfig.TryGetConfigForModule(module, out var config)) {
                ApplyModuleConfiguration(module, config, options);
            }

            module.ConfigureServices(services);
        }

        return services;
    }

    private static bool TryGetConfigForModule(this Dictionary<string, IReadOnlyDictionary<string, string>>? moduleConfig, IRegistryModule module, out IReadOnlyDictionary<string, string> config) {
        config = new Dictionary<string, string>();
        if (moduleConfig is null) {
            return false;
        }

        var moduleType = module.GetType();

        // Namespace + type-name first
        if (moduleConfig.TryGetValue(moduleType.FullName, out config)) {
            return true;
        }

        // type-name without namespace
        if (moduleConfig.TryGetValue(moduleType.Name, out config)) {
            return true;
        }

        // Wildcard matching
        var wildcard = '*';
        var wildcardMatch = moduleConfig.Where(entry => entry.Key.Contains(wildcard) && entry.Key.Length > 1)
            .OrderByDescending(entry => entry.Key.Replace(wildcard.ToString(), string.Empty).Length) // Get the most specific wildcard match
                .ThenBy(entry => entry.Key.Count(c => c == wildcard)) // Favor fewer wildcards when the lengths (without wildcards) match
            .FirstOrDefault(entry => moduleType.FullName.MatchWildcard(entry.Key, wildcard, comparison: StringComparison.OrdinalIgnoreCase))
            .Value;

        if (wildcardMatch is not null) {
            config = wildcardMatch;
            return true;
        }

        return false;
    }

    private static void ApplyModuleConfiguration(IRegistryModule module, IReadOnlyDictionary<string, string> config, ModuleOptions options) {
        var modType = module.GetType();
        var bindingFlags = BindingFlags.Instance | BindingFlags.Public;
        if (!options.PublicOnly) {
            bindingFlags |= BindingFlags.NonPublic;
        }

        var propertiesToSet = modType.GetProperties(bindingFlags)
            .Where(prop => config.ContainsKey(prop.Name));

        var extraConfigs = config.Keys.Except(propertiesToSet.Select(prop => prop.Name), StringComparer.OrdinalIgnoreCase);
        if (extraConfigs.Any()) {
            var msg = "The following properties do not exist";
            if (options.PublicOnly) {
                msg += " or are not public";
            }
            msg += $" on module {modType.Name}: {string.Join(", ", extraConfigs)}";
            throw new InvalidOperationException(msg);
        }

        var unsettableProps = propertiesToSet.Where(prop 
            => (options.PublicOnly ? prop.GetSetMethod() : prop.SetMethod) == null).Select(prop => prop.Name);
        if (unsettableProps.Any()) {
            var msg = "The following properties have no";
            if (options.PublicOnly) {
                msg += " public";
            }
            msg += $" setter and cannot be configured on module {modType.Name}: {string.Join(", ", unsettableProps)}";
            throw new InvalidOperationException(msg);
        }

        foreach (var prop in propertiesToSet) {
            var converter = TypeDescriptor.GetConverter(prop.PropertyType);
            // TODO: This will throw a NotSupportedException if the conversion can't be completed.
            //       Do I want that? Should I handle the exception? Should I throw my own exception?
            prop.SetValue(module, converter.ConvertFrom(config[prop.Name]));
        }
    }

    private static IEnumerable<IRegistryModule> InstantiateModules(ModuleOptions options) {
        var modules = options.Modules.AsEnumerable();
        var typesToCreate = options.ModuleTypes.Except(modules.Select(m => m.GetType()));
        if (options.PublicOnly) {
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

    private static bool MatchWildcard(this string value, string check, char wildcard, StringComparison comparison = StringComparison.Ordinal) {
        var trimmedCheck = check.Trim(wildcard);
        return (check.StartsWith(wildcard), check.EndsWith(wildcard), trimmedCheck.Contains(wildcard)) switch {
            (var wildcardStart, var wildcardEnd, true) => MatchInnerWildcard(value, trimmedCheck.Split(wildcard), !wildcardStart, !wildcardEnd, comparison),
            (true, true, _) => value.Contains(trimmedCheck, comparison),
            (false, true, _) => value.StartsWith(trimmedCheck, comparison),
            (true, false, _) => value.EndsWith(trimmedCheck, comparison),
            (false, false, _) => value.Equals(check, comparison)
        };
    }

    private static bool MatchInnerWildcard(string value, string[] checkSegments, bool shouldMatchStart, bool shouldMatchEnd, StringComparison comparison) {
        if (checkSegments.Length == 0) {
            return false;
        }

        if (shouldMatchStart && !value.StartsWith(checkSegments.First(), comparison)) {
            return false;
        }
        if (shouldMatchEnd && !value.EndsWith(checkSegments.Last(), comparison)) {
            return false;
        }

        var valueIndex = 0;
        foreach (var segment in checkSegments) {
            valueIndex = value.IndexOf(segment, valueIndex, comparison);
            if (valueIndex < 0) {
                break;            
            }
            valueIndex += segment.Length;
        }
        return valueIndex >= 0;
    }
}
