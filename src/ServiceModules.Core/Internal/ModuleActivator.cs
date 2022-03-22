using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ServiceModules.Internal;
internal class ModuleActivator : IModuleActivator {
    public IEnumerable<IRegistryModule> InstantiateModules(ModuleOptions options) {
        var typesToCreate = options.ModuleTypes.AsEnumerable();
        if (options.PublicOnly) {
            typesToCreate = typesToCreate.Where(m => m.IsPublic);
        }

        return !typesToCreate.Any()
            ? Enumerable.Empty<IRegistryModule>()
            : typesToCreate.Select(t => FindConstructorWithArgsThatSatisfy(t, options.AllowedModuleArgTypes) is { } ctor
                    ? CreateModuleInstance(ctor, options)
                    : throw new InvalidOperationException($"Unable to activate {nameof(IRegistryModule)} of type '{t.Name}' " +
                    $"-- no suitable constructor found. " +
                    $"Allowable constructor parameters are: {string.Join(", ", options.AllowedModuleArgTypes)}"))
            .OrderByDescending(m => m.Priority);
    }

    private IRegistryModule CreateModuleInstance(ConstructorInfo ctor, ModuleOptions options) {
        var ctorParams = ctor.GetParameters();
        var paramInstances = new object[ctorParams.Length];

        for (var i = 0; i < ctorParams.Length; i++) {
            paramInstances[i] = options.Providers.FirstOrDefault(provider => provider.GetType() == ctorParams[i].ParameterType)
                ?? options.Providers.FirstOrDefault(provider => ctorParams[i].ParameterType.IsAssignableFrom(provider.GetType()))
                ?? throw new InvalidOperationException($"Parameter type {ctorParams[i].ParameterType.Name} not supported for {nameof(IRegistryModule)}");
        }

        return (IRegistryModule)ctor.Invoke(paramInstances);
    }

    private ConstructorInfo? FindConstructorWithArgsThatSatisfy(Type moduleType, IEnumerable<Type> availableArgs) {
        var availableCount = availableArgs.Count();

        return moduleType.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Select(ctor => new { ctor, @params = ctor.GetParameters() })
            .Where(info => info.@params.Length <= availableCount) // Remove constructors with more parameters than the available args
            .Where(info => info.@params.All(p => availableArgs.Any(arg => p.ParameterType.IsAssignableFrom(arg)))) // Remove constructors with the wrong types of args
            .OrderByDescending(info => info.@params.Length) // Get constructor with longest parameter list satisfied by the available args
            .Select(info => info.ctor)
            .FirstOrDefault();
    }
}
