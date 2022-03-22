using System;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using ServiceModules.Internal;

namespace ServiceModules;
public class ServiceCollectionModuleConfiguration {
    private readonly ModuleOptions _options;
    private bool _entryAssemblyAttempted = false;

    internal ServiceCollectionModuleConfiguration() : this(new ModuleOptions()) { }
    internal ServiceCollectionModuleConfiguration(ModuleOptions options) => _options = options;

    /// <summary>
    /// Set the key used to load the module configurations from <see cref="IConfiguration"/>
    /// </summary>
    /// <param name="sectionKey">The configuration key</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public ServiceCollectionModuleConfiguration UsingModuleConfigurationSection(string sectionKey) {
        if (string.IsNullOrWhiteSpace(sectionKey)) {
            throw new ArgumentException($"'{nameof(sectionKey)}' cannot be null or whitespace.", nameof(sectionKey));
        }
        _options.ModuleConfigSectionKey = sectionKey;
        return this;
    }

    /// <summary>
    /// Only run public <see cref="IRegistryModule"/> implementations
    /// </summary>
    /// <returns></returns>
    public ServiceCollectionModuleConfiguration PublicOnly() {
        _options.PublicOnly = true;
        return this;
    }

    /// <summary>
    /// The assemblies to scan for <see cref="IRegistryModule"/> implementations.
    /// </summary>
    /// <param name="assemblyMarkers">Types from the assemblies to scan</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public ServiceCollectionModuleConfiguration FromAssemblies(params Type[] assemblyMarkers)
        => FromAssemblies(assemblyMarkers.Select(marker => marker.Assembly).ToArray());

    /// <summary>
    /// The assemblies to scan for <see cref="IRegistryModule"/> implementations.
    /// </summary>
    /// <param name="assemblies">The assemblies to scan</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public ServiceCollectionModuleConfiguration FromAssemblies(params Assembly[] assemblies) {
        if (assemblies is null) {
            throw new ArgumentNullException(nameof(assemblies));
        }
        if (assemblies.Length == 0) {
            throw new ArgumentException("No assemblies given to scan", nameof(assemblies));
        }

        var moduleTypes = assemblies.Distinct()
            .SelectMany(assm => assm.GetTypes())
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => t.GetInterface(nameof(IRegistryModule)) != null)
            .Where(t => !_options.ModuleTypes.Contains(t))
            .Distinct()
            .ToArray();

        if (moduleTypes.Length > 0) {
            _options.ModuleTypes.AddRange(moduleTypes);
        }
        return this;
    }

    /// <summary>
    /// Additional providers to use when activating modules
    /// </summary>
    /// <param name="providers"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public ServiceCollectionModuleConfiguration WithProviders(params object[] providers) {
        if (providers is null) {
            throw new ArgumentNullException(nameof(providers));
        }

        foreach (var provider in providers) {
            if (!_options.Providers.Any(p => p.GetType() == provider.GetType())) {
                _options.Providers.Add(provider);
                _options.AllowedModuleArgTypes.Add(provider.GetType());
            }
        }

        return this;
    }

    /// <summary>
    /// Individual module types to run
    /// </summary>
    /// <param name="moduleTypes"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public ServiceCollectionModuleConfiguration UsingModules(params Type[] moduleTypes) {
        if (moduleTypes is null) {
            throw new ArgumentNullException(nameof(moduleTypes));
        }

        foreach (var modType in moduleTypes) {
            if (!_options.ModuleTypes.Contains(modType)) {
                _options.ModuleTypes.Add(modType);
            }
        }

        return this;
    }

    /// <summary>
    /// Any explicitly created module to run
    /// </summary>
    /// <param name="modules"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public ServiceCollectionModuleConfiguration UsingModules(params IRegistryModule[] modules) {
        if (modules is null) {
            throw new ArgumentNullException(nameof(modules));
        }

        foreach (var module in modules) {
            if (!_options.Modules.Contains(module)) {
                _options.Modules.Add(module);
            }
        }

        return this;
    }

    /// <summary>
    /// Sets the environment to use when registering modules.
    /// Required to enforce <see cref="IRegistryModule.TargetEnvironment"/>.
    /// </summary>
    /// <param name="environment">The environment to use</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="InvalidOperationException">When the <paramref name="environment"/> does not implement <see cref="IHostEnvironment"/></exception>
    public virtual ServiceCollectionModuleConfiguration WithEnvironment(object environment) {
        if (environment is null) {
            throw new ArgumentNullException(nameof(environment));
        }

        var baseEnvType = typeof(IHostEnvironment);
        if (!baseEnvType.IsAssignableFrom(environment.GetType())) {
            throw new InvalidOperationException($"Environment object must implement {nameof(IHostEnvironment)}");
        }

        AddAllowedArgType(baseEnvType);
        AddAllowedArgType(environment.GetType());

        // TODO: unify this with the AddProviders method. Don't want to get duplicates
        if (_options.Environment is not null) {
            _options.Providers.Remove(_options.Environment);
        }
        _options.Environment = environment;
        _options.Providers.Add(_options.Environment);

        return this;
    }

    /// <summary>
    /// The <see cref="IConfiguration"/> to provide when registering modules.
    /// Only needed if using it as a module provider
    /// </summary>
    /// <param name="configuration">The configuration to use</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public virtual ServiceCollectionModuleConfiguration WithConfiguration(IConfiguration configuration) {
        if (configuration is null) {
            throw new ArgumentNullException(nameof(configuration));
        }

        AddAllowedArgType(typeof(IConfiguration));

        // TODO: unify this with the AddProviders method. Don't want to get duplicates
        if (_options.Configuration is not null) {
            _options.Providers.Remove(_options.Configuration);
        }
        _options.Configuration = configuration;
        _options.Providers.Add(_options.Configuration);

        return this;
    }

    internal ModuleOptions GetOptions() {
        if (!_options.Modules.Any() && !_options.ModuleTypes.Any()) {
            AttemptToLoadFromEntryAssembly();
        }
        RemoveModuleTypesWithConcreteImplementations();
        return _options;
    }

    private void RemoveModuleTypesWithConcreteImplementations() {
        if (_options.Modules.Any() && _options.ModuleTypes.Any()) {
            var concreteTypes = _options.Modules.Select(m => m.GetType());
            _options.ModuleTypes.RemoveAll(mt => concreteTypes.Contains(mt));
        }
    }

    private void AddAllowedArgType(Type type) {
        if (!_options.AllowedModuleArgTypes.Contains(type)) {
            _options.AllowedModuleArgTypes.Add(type);
        }
    }

    private void AttemptToLoadFromEntryAssembly() {
        if (_entryAssemblyAttempted) {
            return;
        }
        _entryAssemblyAttempted = true;
        if (Assembly.GetEntryAssembly() is { } entry) {
            FromAssemblies(entry);
        }
    }
}
