using System;
using System.Collections.Generic;
#if !NETSTANDARD2_0
using System.Diagnostics.CodeAnalysis;
#endif
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using ServiceRegistryModules.Exceptions;
using ServiceRegistryModules.Internal;

namespace ServiceRegistryModules;

public class FullServiceCollectionRegistryConfiguration : ServiceCollectionRegistryConfiguration {
    
    internal FullServiceCollectionRegistryConfiguration() { }
    internal FullServiceCollectionRegistryConfiguration(RegistryOptions options) : base(options) { }

    /// <inheritdoc cref="ServiceCollectionRegistryConfiguration.UsingProviders(object[])"/>
    public new FullServiceCollectionRegistryConfiguration UsingProviders(params object[] providers) {
#if NET6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(providers);
#else
        if (providers is null) {
            throw new ArgumentNullException(nameof(providers));
        }
#endif

        foreach (var provider in providers) {
            if (HostEnvironmentType.IsAssignableFrom(provider.GetType())) {
                UsingEnvironment(provider);
            } else if (ConfigurationType.IsAssignableFrom(provider.GetType())) {
                UsingConfiguration((IConfiguration)provider);
            } else {
                AddProvider(provider, false);
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
    public FullServiceCollectionRegistryConfiguration UsingEnvironment(object environment) {
#if NET6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(environment);
#else
        if (environment is null) {
            throw new ArgumentNullException(nameof(environment));
        }
#endif

        if (!HostEnvironmentType.IsAssignableFrom(environment.GetType())) {
            throw new RegistryConfigurationException($"Environment object must implement {nameof(IHostEnvironment)}");
        }

        AddProvider(environment, true, HostEnvironmentType);
        Options.Environment = environment;

        return this;
    }

    [Obsolete("Use 'UsingConfiguration' instead")]
    public FullServiceCollectionRegistryConfiguration UsingConfigurationProvider(IConfiguration configuration) => UsingConfiguration(configuration);

    /// <summary>
    /// The <see cref="IConfiguration"/> to provide when applying registries.
    /// Only needed if it is used by a registry
    /// </summary>
    /// <param name="configuration">The configuration to use</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public FullServiceCollectionRegistryConfiguration UsingConfiguration(IConfiguration configuration) {
#if NET6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(configuration);
#else
        if (configuration is null) {
            throw new ArgumentNullException(nameof(configuration));
        }
#endif

        AddProvider(configuration, true, ConfigurationType);
        Options.Configuration = configuration;

        return this;
    }

#region New methods with different return type

    /// <inheritdoc cref="ServiceCollectionRegistryConfiguration.WithConfigurationsFromSection(string)"/>
    public new FullServiceCollectionRegistryConfiguration WithConfigurationsFromSection(string sectionKey) {
        base.WithConfigurationsFromSection(sectionKey);
        return this;
    }

    /// <inheritdoc cref="ServiceCollectionRegistryConfiguration.PublicOnly"/>
    public new FullServiceCollectionRegistryConfiguration PublicOnly() {
        base.PublicOnly();
        return this;
    }

    /// <inheritdoc cref="ServiceCollectionRegistryConfiguration.FromAssemblyOf{T}"/>
    public new FullServiceCollectionRegistryConfiguration FromAssemblyOf<T>() {
        base.FromAssemblyOf<T>();
        return this;
    }

    /// <inheritdoc cref="ServiceCollectionRegistryConfiguration.FromAssembliesOf(Type[])"/>
    public new FullServiceCollectionRegistryConfiguration FromAssembliesOf(params Type[] assemblyMarkers) {
        base.FromAssembliesOf(assemblyMarkers);
        return this;
    }

    /// <inheritdoc cref="ServiceCollectionRegistryConfiguration.FromAssemblies(Assembly[])"/>
    public new FullServiceCollectionRegistryConfiguration FromAssemblies(params Assembly[] assemblies) {
        base.FromAssemblies(assemblies);
        return this;
    }

    /// <inheritdoc cref="ServiceCollectionRegistryConfiguration.OfTypes(Type[])"/>
    public new FullServiceCollectionRegistryConfiguration OfTypes(params Type[] registryTypes) {
        base.OfTypes(registryTypes);
        return this;
    }

    /// <inheritdoc cref="ServiceCollectionRegistryConfiguration.From(IRegistryModule[])"/>
    public new FullServiceCollectionRegistryConfiguration From(params IRegistryModule[] registries) {
        base.From(registries);
        return this;
    } 

#endregion
}

public class ServiceCollectionRegistryConfiguration {
    private protected static readonly Type HostEnvironmentType = typeof(IHostEnvironment);
    private protected static readonly Type ConfigurationType = typeof(IConfiguration);

    private protected readonly RegistryOptions Options;
    private bool _entryAssemblyAttempted = false;
    private Assembly? _defaultAssembly;

    internal ServiceCollectionRegistryConfiguration() : this(new RegistryOptions()) { }
    internal ServiceCollectionRegistryConfiguration(RegistryOptions options) => Options = options;

    /// <summary>
    /// Set the key used to load the registry configurations from <see cref="IConfiguration"/>
    /// </summary>
    /// <param name="sectionKey">The configuration key</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public ServiceCollectionRegistryConfiguration WithConfigurationsFromSection(string sectionKey) {
        if (string.IsNullOrWhiteSpace(sectionKey)) {
            throw new RegistryConfigurationException($"'{nameof(sectionKey)}' cannot be null or whitespace.");
        }
        Options.RegistryConfigSectionKey = sectionKey;
        return this;
    }

    /// <summary>
    /// Only run public <see cref="IRegistryModule"/> implementations
    /// </summary>
    /// <returns></returns>
    public ServiceCollectionRegistryConfiguration PublicOnly() {
        Options.PublicOnly = true;
        return this;
    }

    /// <summary>
    /// The assembly to scan for <see cref="IRegistryModule"/> implementations.
    /// </summary>
    /// <typeparam name="T">A type defined in the assembly to scan</typeparam>
    /// <returns></returns>
    public ServiceCollectionRegistryConfiguration FromAssemblyOf<T>() => FromAssembliesOf(typeof(T));

    /// <summary>
    /// The assemblies to scan for <see cref="IRegistryModule"/> implementations.
    /// </summary>
    /// <param name="assemblyMarkers">Types defined in the assemblies to scan</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public ServiceCollectionRegistryConfiguration FromAssembliesOf(params Type[] assemblyMarkers)
        => FromAssemblies(assemblyMarkers.Select(marker => marker.Assembly).ToArray());

    /// <summary>
    /// The assemblies to scan for <see cref="IRegistryModule"/> implementations.
    /// </summary>
    /// <param name="assemblies">The assemblies to scan</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public ServiceCollectionRegistryConfiguration FromAssemblies(params Assembly[] assemblies) {
        if (assemblies is not { Length: > 0 }) {
            throw new RegistryConfigurationException("No assemblies given to scan");
        }

        var registryTypes = assemblies.Distinct()
            .SelectMany(assm => assm.GetTypes())
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => t.GetInterface(nameof(IRegistryModule)) != null)
            .Where(t => !Options.RegistryTypes.Contains(t))
            .Distinct()
            .ToArray();

        if (registryTypes.Length > 0) {
            Options.RegistryTypes.AddRange(registryTypes);
        }
        return this;
    }

    /// <summary>
    /// Additional providers to use when activating registries
    /// </summary>
    /// <param name="providers"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public ServiceCollectionRegistryConfiguration UsingProviders(params object[] providers) {
#if NET6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(providers);
#else
        if (providers is null) {
            throw new ArgumentNullException(nameof(providers));
        }
#endif

        foreach (var provider in providers) {
            AddProvider(provider, false);
        }

        return this;
    }

    /// <summary>
    /// Individual <see cref="IRegistryModule"/> types to run
    /// </summary>
    /// <param name="registryTypes"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="InvalidOperationException">When any of the provided <paramref name="registryTypes"/> do not implement <see cref="IRegistryModule"/></exception>
    public ServiceCollectionRegistryConfiguration OfTypes(params Type[] registryTypes) {
#if NET6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(registryTypes);
#else
        if (registryTypes is null) {
            throw new ArgumentNullException(nameof(registryTypes));
        }
#endif
        var invalidRegistryTypes = registryTypes.Where(t => !typeof(IRegistryModule).IsAssignableFrom(t)).Select(t => t.Name);
        if (invalidRegistryTypes.Any()) {
            throw new RegistryConfigurationException($"The following registry types do not implement {nameof(IRegistryModule)}: {string.Join(", ", invalidRegistryTypes)}");
        }

        foreach (var regType in registryTypes) {
            if (!Options.RegistryTypes.Contains(regType)) {
                Options.RegistryTypes.Add(regType);
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
    public ServiceCollectionRegistryConfiguration From(params IRegistryModule[] registries) {
#if NET6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(registries);
#else
        if (registries is null) {
            throw new ArgumentNullException(nameof(registries));
        }
#endif

        foreach (var registry in registries) {
            if (!Options.Registries.Contains(registry)) {
                Options.Registries.Add(registry);
            }
        }

        return this;
    }

    internal ServiceCollectionRegistryConfiguration WithDefaultAssembly(Assembly assembly) {
        _defaultAssembly = assembly;
        return this;
    }

    internal RegistryOptions GetOptions() {
        LoadAdditionalRegistriesFromConfig();

        if (Options.Registries.Count == 0 && Options.RegistryTypes.Count == 0) {
            AttemptToLoadFromDefaultAssembly();
        }
        RemoveRegistryTypesWithConcreteImplementations();
        RemoveRegistriesSkippedInConfig();
        return Options;
    }

    private void RemoveRegistryTypesWithConcreteImplementations() {
        if (Options.Registries.Count > 0 && Options.RegistryTypes.Count > 0) {
            var concreteTypes = Options.Registries.Select(m => m.GetType());
            Options.RegistryTypes.RemoveAll(mt => concreteTypes.Contains(mt));
        }
    }

    private void AddAllowedArgType(Type type) {
        if (!Options.AllowedRegistryCtorArgTypes.Contains(type)) {
            Options.AllowedRegistryCtorArgTypes.Add(type);
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

    private void LoadAdditionalRegistriesFromConfig() {
        if (Options.Configuration is null) {
            return;
        }

        var addKey = $"{Options.RegistryConfigSectionKey}:{ServiceRegistryModulesDefaults.ADD_MODULES_KEY}";
        var additionalRegistries = new List<AddRegistryConfig>();
        var configSection = Options.Configuration.GetSection(addKey);

        foreach (var addSection in configSection.GetChildren()) {
            if (addSection.Value is { }) {
                additionalRegistries.Add(new() { FullName = addSection.Value });
            } else {
                var fullName = addSection.GetSection(nameof(AddRegistryConfig.FullName)).Value;
                bool.TryParse(addSection.GetSection(nameof(AddRegistryConfig.SuppressErrors)).Value, out var suppressErr);
                var hintPath = addSection.GetSection(nameof(AddRegistryConfig.HintPath)).Value;

                additionalRegistries.Add(new() {
                    FullName = fullName!,
                    SuppressErrors = suppressErr,
                    HintPath = hintPath
                });
            }
        }

        if (additionalRegistries.Count == 0) {
            return;
        }

        var unresolvedTypes = new List<string>();
        var resolvedTypes = new List<Type>();
        foreach (var additionalReg in additionalRegistries) {
            if (!ServiceCollectionRegistryConfiguration.TryGetType(additionalReg, out var additionalType)) {
                if (!additionalReg.SuppressErrors) {
                    unresolvedTypes.Add(additionalReg.FullName);
                }
            } else {
                resolvedTypes.Add(additionalType!);
            }
        }

        if (unresolvedTypes.Count > 0) {
            throw new RegistryConfigurationException($"Unable to find additional configured registries: {string.Join(", ", unresolvedTypes)}");
        }
        if (resolvedTypes.Count == 0) {
            return;
        }
        OfTypes(resolvedTypes.ToArray());
    }

    private void RemoveRegistriesSkippedInConfig() {
        if (Options.Configuration is null) {
            return;
        }

        var skipKey = $"{Options.RegistryConfigSectionKey}:{ServiceRegistryModulesDefaults.SKIP_MODULES_KEY}";
        var configSection = Options.Configuration.GetSection(skipKey);
        var skipRegistries = configSection.GetChildren().Select(child => child.Value);

        foreach (var skippedRegistry in skipRegistries) {
            if (string.IsNullOrWhiteSpace(skippedRegistry)) {
                continue;
            }

            var matchIdx = Options.RegistryTypes.FindIndex(t => skippedRegistry!.Equals(t.FullName, StringComparison.OrdinalIgnoreCase));
            if (matchIdx >= 0) {
                Options.RegistryTypes.RemoveAt(matchIdx);
                continue; // At this point we've already removed types that have an instance, so no need to continue
            }

            matchIdx = Options.Registries.FindIndex(r => skippedRegistry!.Equals(r.GetType().FullName, StringComparison.OrdinalIgnoreCase));
            if (matchIdx >= 0) {
                Options.Registries.RemoveAt(matchIdx);
            }
        }
    }

#if !NETSTANDARD2_0
    private static bool TryGetType(AddRegistryConfig addConfig, [NotNullWhen(true)] out Type? foundType) {
#else
    private static bool TryGetType(AddRegistryConfig addConfig, out Type? foundType) {
#endif
        foundType = null;

        var fullTypeName = addConfig.FullName;
        var hintPath = addConfig.HintPath;

        var lastIndex = fullTypeName.LastIndexOf('.');
        if (lastIndex < 0) {
            return false;
        }

#if !NETSTANDARD2_0
        var typeName = fullTypeName[(lastIndex + 1)..];
        var assemblyName = fullTypeName[..lastIndex];
#else
        var typeName = fullTypeName.Substring(lastIndex + 1);
        var assemblyName = fullTypeName.Substring(0, lastIndex);
#endif

        Assembly assembly;
        try {
            assembly = string.IsNullOrEmpty(hintPath) ? Assembly.Load(new AssemblyName(assemblyName)) : Assembly.LoadFrom(hintPath);
        } catch (FileNotFoundException) {
            return false;
        }

        foundType = assembly.GetType($"{assemblyName}.{typeName}", throwOnError: false);
        return foundType is not null;
    }

    private protected void AddProvider(object provider, bool replaceExisting, params Type[] additionalArgsTypes) {
        var existingProvider = Options.Providers.FirstOrDefault(p
            => p.GetType() == provider.GetType()
            || additionalArgsTypes.Contains(p.GetType())
            || additionalArgsTypes.Any(arg => arg.IsAssignableFrom(p.GetType())));
        var providerExists = existingProvider is not null;

        if (providerExists && replaceExisting) {
            Options.Providers.Remove(existingProvider!);
            Options.AllowedRegistryCtorArgTypes.Remove(existingProvider!.GetType());
            providerExists = false;
        }

        if (!providerExists) {
            Options.Providers.Add(provider);
            Options.AllowedRegistryCtorArgTypes.Add(provider.GetType());
            var newArgTypes = additionalArgsTypes.Except(Options.AllowedRegistryCtorArgTypes).ToArray();
            Options.AllowedRegistryCtorArgTypes.AddRange(newArgTypes);
        }
    }
}
