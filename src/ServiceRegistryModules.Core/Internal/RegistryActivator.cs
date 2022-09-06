using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ServiceRegistryModules.Exceptions;

namespace ServiceRegistryModules.Internal;
internal class RegistryActivator : IRegistryActivator {
    public IEnumerable<IRegistryModule> InstantiateRegistries(RegistryOptions options) {
        var typesToCreate = options.RegistryTypes.AsEnumerable();
        if (options.PublicOnly) {
            typesToCreate = typesToCreate.Where(IsTypePublic);
        }

        var createdRegistries = !typesToCreate.Any()
            ? Enumerable.Empty<IRegistryModule>()
            : typesToCreate.Select(t => FindConstructorWithArgsThatSatisfy(t, options.AllowedRegistryCtorArgTypes) is { } ctor
                    ? CreateRegistryInstance(ctor, options)
                    : throw new RegistryActivationException($"Unable to activate {nameof(IRegistryModule)} of type '{t.Name}' " +
                    $"-- no suitable constructor found. " +
                    $"Allowable constructor parameters are: {string.Join(", ", options.AllowedRegistryCtorArgTypes)}"));

        return createdRegistries.Concat(options.Registries)
            .OrderByDescending(m => m.Priority)
            .ToArray();
    }

    private bool IsTypePublic(Type type) {
        if (type.IsPublic) {
            return true;
        }

        if (!type.IsNested || !type.IsNestedPublic) {
            return false;
        }

        // Return the declaring type's access level
        return IsTypePublic(type.DeclaringType);
    }

    private IRegistryModule CreateRegistryInstance(ConstructorInfo ctor, RegistryOptions options) {
        var ctorParams = ctor.GetParameters();
        var paramInstances = new object[ctorParams.Length];

        for (var i = 0; i < ctorParams.Length; i++) {
            paramInstances[i] = options.Providers.FirstOrDefault(provider => provider.GetType() == ctorParams[i].ParameterType)
                ?? options.Providers.FirstOrDefault(provider => ctorParams[i].ParameterType.IsAssignableFrom(provider.GetType()));
        }

        return (IRegistryModule)ctor.Invoke(paramInstances);
    }

    private ConstructorInfo? FindConstructorWithArgsThatSatisfy(Type registryType, IEnumerable<Type> availableArgs) {
        var availableCount = availableArgs.Count();

        return registryType.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Select(ctor => new { ctor, @params = ctor.GetParameters() })
            .Where(info => info.@params.Length <= availableCount) // Remove constructors with more parameters than the available args
            .Where(info => info.@params.All(p => availableArgs.Any(arg => p.ParameterType.IsAssignableFrom(arg)))) // Remove constructors with the wrong types of args
            .OrderByDescending(info => info.@params.Length) // Get constructor with longest parameter list satisfied by the available args
            .Select(info => info.ctor)
            .FirstOrDefault();
    }
}
