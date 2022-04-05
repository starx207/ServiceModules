using System;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using ServiceRegistryModules.Internal;

namespace ServiceRegistryModules;
public class ServiceCollectionRegistryConfiguration {
    private static readonly Type _hostEnvironmentType = typeof(IHostEnvironment);
    private static readonly Type _configurationType = typeof(IConfiguration);

    private readonly RegistryOptions _options;
    private bool _entryAssemblyAttempted = false;
    private Assembly? _defaultAssembly;

    internal ServiceCollectionRegistryConfiguration() : this(new RegistryOptions()) { }
    internal ServiceCollectionRegistryConfiguration(RegistryOptions options) => _options = options;

    /// <summary>
    /// Set the key used to load the registry configurations from <see cref="IConfiguration"/>
    /// </summary>
    /// <param name="sectionKey">The configuration key</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public ServiceCollectionRegistryConfiguration WithConfigurationsFromSection(string sectionKey) {
        if (string.IsNullOrWhiteSpace(sectionKey)) {
            throw new ArgumentException($"'{nameof(sectionKey)}' cannot be null or whitespace.", nameof(sectionKey));
        }
        _options.RegistryConfigSectionKey = sectionKey;
        return this;
    }

    /// <summary>
    /// Only run public <see cref="IRegistryModule"/> implementations
    /// </summary>
    /// <returns></returns>
    public ServiceCollectionRegistryConfiguration PublicOnly() {
        _options.PublicOnly = true;
        return this;
    }

    /// <summary>
    /// The assemblies to scan for <see cref="IRegistryModule"/> implementations.
    /// </summary>
    /// <param name="assemblyMarkers">Types from the assemblies to scan</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public ServiceCollectionRegistryConfiguration FromAssemblies(params Type[] assemblyMarkers)
        => FromAssemblies(assemblyMarkers.Select(marker => marker.Assembly).ToArray());

    /// <summary>
    /// The assemblies to scan for <see cref="IRegistryModule"/> implementations.
    /// </summary>
    /// <param name="assemblies">The assemblies to scan</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public ServiceCollectionRegistryConfiguration FromAssemblies(params Assembly[] assemblies) {
        if (assemblies is not { Length: > 0 }) {
            throw new ArgumentException("No assemblies given to scan", nameof(assemblies));
        }

        var registryTypes = assemblies.Distinct()
            .SelectMany(assm => assm.GetTypes())
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => t.GetInterface(nameof(IRegistryModule)) != null)
            .Where(t => !_options.RegistryTypes.Contains(t))
            .Distinct()
            .ToArray();

        if (registryTypes.Length > 0) {
            _options.RegistryTypes.AddRange(registryTypes);
        }
        return this;
    }

    /// <summary>
    /// Additional providers to use when activating registries
    /// </summary>
    /// <param name="providers"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public ServiceCollectionRegistryConfiguration WithProviders(params object[] providers) {
        if (providers is null) {
            throw new ArgumentNullException(nameof(providers));
        }

        foreach (var provider in providers) {
            if (_hostEnvironmentType.IsAssignableFrom(provider.GetType())) {
                WithEnvironment(provider);
            } else if (_configurationType.IsAssignableFrom(provider.GetType())) {
                WithConfiguration((IConfiguration)provider);
            } else {
                AddProvider(provider, false);
            }
        }

        return this;
    }

    /// <summary>
    /// Individual <see cref="IRegistryModule"/> types to run
    /// </summary>
    /// <param name="registryTypes"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public ServiceCollectionRegistryConfiguration UsingRegistries(params Type[] registryTypes) {
        if (registryTypes is null) {
            throw new ArgumentNullException(nameof(registryTypes));
        }
        var invalidRegistryTypes = registryTypes.Where(t => !typeof(IRegistryModule).IsAssignableFrom(t)).Select(t => t.Name);
        if (invalidRegistryTypes.Any()) {
            throw new InvalidOperationException($"The following registry types do not implement {nameof(IRegistryModule)}: {string.Join(", ", invalidRegistryTypes)}");
        }

        foreach (var regType in registryTypes) {
            if (!_options.RegistryTypes.Contains(regType)) {
                _options.RegistryTypes.Add(regType);
            }
        }

        return this;
    }

    /// <summary>
    /// Any explicitly created registry instance to run
    /// </summary>
    /// <param name="registries"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public ServiceCollectionRegistryConfiguration UsingRegistries(params IRegistryModule[] registries) {
        if (registries is null) {
            throw new ArgumentNullException(nameof(registries));
        }

        foreach (var registry in registries) {
            if (!_options.Registries.Contains(registry)) {
                _options.Registries.Add(registry);
            }
        }

        return this;
    }

    /// <summary>
    /// Sets the environment to use when applying registries.
    /// Required to enforce <see cref="IRegistryModule.TargetEnvironment"/>.
    /// </summary>
    /// <param name="environment">The environment to use</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="InvalidOperationException">When the <paramref name="environment"/> does not implement <see cref="IHostEnvironment"/></exception>
    public virtual ServiceCollectionRegistryConfiguration WithEnvironment(object environment) {
        if (environment is null) {
            throw new ArgumentNullException(nameof(environment));
        }

        if (!_hostEnvironmentType.IsAssignableFrom(environment.GetType())) {
            throw new InvalidOperationException($"Environment object must implement {nameof(IHostEnvironment)}");
        }

        AddProvider(environment, true, _hostEnvironmentType);
        _options.Environment = environment;

        return this;
    }

    /// <summary>
    /// The <see cref="IConfiguration"/> to provide when applying registries.
    /// Only needed if it is used by a registry
    /// </summary>
    /// <param name="configuration">The configuration to use</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public virtual ServiceCollectionRegistryConfiguration WithConfiguration(IConfiguration configuration) {
        if (configuration is null) {
            throw new ArgumentNullException(nameof(configuration));
        }

        AddProvider(configuration, true, _configurationType);
        _options.Configuration = configuration;

        return this;
    }

    internal ServiceCollectionRegistryConfiguration WithDefaultAssembly(Assembly assembly) {
        _defaultAssembly = assembly;
        return this;
    }

    internal RegistryOptions GetOptions() {
        if (!_options.Registries.Any() && !_options.RegistryTypes.Any()) {
            AttemptToLoadFromDefaultAssembly();
        }
        RemoveRegistryTypesWithConcreteImplementations();
        return _options;
    }

    private void RemoveRegistryTypesWithConcreteImplementations() {
        if (_options.Registries.Any() && _options.RegistryTypes.Any()) {
            var concreteTypes = _options.Registries.Select(m => m.GetType());
            _options.RegistryTypes.RemoveAll(mt => concreteTypes.Contains(mt));
        }
    }

    private void AddAllowedArgType(Type type) {
        if (!_options.AllowedRegistryCtorArgTypes.Contains(type)) {
            _options.AllowedRegistryCtorArgTypes.Add(type);
        }
    }

    private void AttemptToLoadFromDefaultAssembly() {
        if (_entryAssemblyAttempted) {
            return;
        }
        _entryAssemblyAttempted = true;
        if (_defaultAssembly is { } assm) {
            FromAssemblies(assm);
        }
    }

    private void AddProvider(object provider, bool replaceExisting, params Type[] additionalArgsTypes) {
        var existingProvider = _options.Providers.FirstOrDefault(p
            => p.GetType() == provider.GetType()
            || additionalArgsTypes.Contains(p.GetType())
            || additionalArgsTypes.Any(arg => arg.IsAssignableFrom(p.GetType())));
        var providerExists = existingProvider is not null;

        if (providerExists && replaceExisting) {
            _options.Providers.Remove(existingProvider!);
            _options.AllowedRegistryCtorArgTypes.Remove(existingProvider!.GetType());
            providerExists = false;
        }

        if (!providerExists) {
            _options.Providers.Add(provider);
            _options.AllowedRegistryCtorArgTypes.Add(provider.GetType());
            var newArgTypes = additionalArgsTypes.Except(_options.AllowedRegistryCtorArgTypes).ToArray();
            _options.AllowedRegistryCtorArgTypes.AddRange(newArgTypes);
        }
    }
}
