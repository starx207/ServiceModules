using System;
using System.Collections.Generic;
using System.ComponentModel;
#if !NETSTANDARD2_0
using System.Diagnostics.CodeAnalysis;
#endif
using System.IO;
using System.Linq;
using System.Reflection;
using ServiceRegistryModules.Exceptions;

namespace ServiceRegistryModules.Internal;
internal class RegistryConfigApplicator : IRegistryConfigApplicator {
    private RegistryConfiguration? _registryConfig;
    private bool _publicOnly;
    private readonly IRegistryConfigLoader _configLoader;

    public RegistryConfigApplicator(IRegistryConfigLoader configLoader) => _configLoader = configLoader;

    public void InitializeFrom(RegistryOptions options) {
        _registryConfig = _configLoader.LoadFrom(options);
        _publicOnly = options.PublicOnly;
    }

    public void ApplyRegistryConfiguration(IRegistryModule registry) {
        if (_registryConfig is null) {
            return;
        }

        var registryType = registry.GetType();
        if (!TryExtractConfigForRegistry(registryType, out var config)) {
            return;
        }

        var allMemberInfo = GetMembersToConfigure(registryType, config);
        GuardUndefinedMembers(registryType, allMemberInfo, config);

        ApplyPropertyConfigurations(registry, registryType, config, allMemberInfo);
        ApplyEventConfigurations(registry, registryType, config, allMemberInfo);
    }

    private void ApplyPropertyConfigurations(IRegistryModule registry, Type registryType, IReadOnlyDictionary<string, RegistryPropertyConfig> allConfig, IEnumerable<MemberInfo> allMemberInfo) {
        var propertiesToSet = allMemberInfo.Where(m => m.MemberType == MemberTypes.Property).Cast<PropertyInfo>();
        var config = FilterConfigType(allConfig, ConfigurationType.Property, propertiesToSet.Select(p => p.Name));
        propertiesToSet = FilterUnsettablePropertiesOrThrow(registryType, propertiesToSet, config);

        foreach (var prop in propertiesToSet) {
            var converter = TypeDescriptor.GetConverter(prop.PropertyType);
            try {
                prop.SetValue(registry, converter.ConvertFrom(config[prop.Name].Value!));
            } catch (Exception ex) {
                if (!config.TryGetValue(prop.Name, out var value) || !value.SuppressErrors) {
                    throw new RegistryConfigurationException($"Unable to set {prop.Name} value to configured value.", ex);
                }
            }
        }
    }

    private void ApplyEventConfigurations(IRegistryModule registry, Type registryType, IReadOnlyDictionary<string, RegistryPropertyConfig> allConfig, IEnumerable<MemberInfo> allMemberInfo) {
        var eventsToSet = allMemberInfo.Where(m => m.MemberType == MemberTypes.Event).Cast<EventInfo>();
        var config = FilterConfigType(allConfig, ConfigurationType.Event, eventsToSet.Select(e => e.Name));

        foreach (var evt in eventsToSet) {
            var suppressErrs = config[evt.Name].SuppressErrors;
            var (assmName, typName, mthdName) = UnpackStaticMethod(config[evt.Name].Value!.ToString(), suppressErrs);
            if (assmName is null || typName is null || mthdName is null) {
                continue;
            }

            Assembly assembly;
            try {
                var hintPath = config[evt.Name].HintPath;
                assembly = string.IsNullOrEmpty(hintPath) ? Assembly.Load(new AssemblyName(assmName)) : Assembly.LoadFrom(hintPath);
            } catch (FileNotFoundException ex) {
                if (!suppressErrs) {
                    throw new RegistryConfigurationException($"'{evt.Name}' event handler could not be loaded from assembly '{assmName}'.", ex);
                }
                continue;
            }

            Type? typeInfo;
            try {
                typeInfo = assembly.GetType($"{assmName}.{typName}", throwOnError: !suppressErrs);
            } catch (TypeLoadException ex) {
                throw new RegistryConfigurationException($"'{evt.Name}' event handler could not be found in type '{typName}'.", ex);
            }

            if (typeInfo is null) {
                continue;
            }

            var methodInfo = typeInfo.GetMethod(mthdName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (methodInfo is null) {
                if (suppressErrs) {
                    continue;
                }
                throw new RegistryConfigurationException($"'{evt.Name}' event handler could not be set from method '{mthdName}'. No such static method found.");
            }

            var tDelegate = evt.EventHandlerType;
            Delegate @delegate;
            try {
                @delegate = Delegate.CreateDelegate(tDelegate!, methodInfo);
            } catch (ArgumentException ex) {
                if (!suppressErrs) {
                    throw new RegistryConfigurationException($"'{typName}.{mthdName}' is not a compatible event handler for '{evt.Name}'.", ex);
                }
                continue;
            }
            var addHandler = evt.GetAddMethod();
            var addHandlerArgs = new[] { @delegate };

            addHandler?.Invoke(registry, addHandlerArgs);
        }
    }

    private (string? assemblyName, string? typeName, string? methodName) UnpackStaticMethod(string? fullMethodName, bool suppressErrs) {
#if NET6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(fullMethodName);
#else
        if (fullMethodName is null) {
            throw new ArgumentNullException(nameof(fullMethodName));
        }
#endif
        var errMsg = $"Invalid handler name ({fullMethodName}). Please use the fully qualified handler name.";

        var lastIndex = fullMethodName.LastIndexOf('.');
        if (lastIndex < 0) {
            return suppressErrs ? default : throw new RegistryConfigurationException(errMsg);
        }
        var methodName = fullMethodName.Substring(lastIndex + 1);
        fullMethodName = fullMethodName.Substring(0, lastIndex);

        lastIndex = fullMethodName.LastIndexOf('.');
        if (lastIndex < 0) {
            return suppressErrs ? default : throw new RegistryConfigurationException(errMsg);
        }
        var typeName = fullMethodName.Substring(lastIndex + 1);

        var assemblyName = fullMethodName.Substring(0, lastIndex);

        return (assemblyName, typeName, methodName);
    }

    private IReadOnlyDictionary<string, RegistryPropertyConfig> FilterConfigType(IReadOnlyDictionary<string, RegistryPropertyConfig> allConfig, ConfigurationType type, IEnumerable<string> memberKeys)
        => allConfig.Where(cfg => (type | ConfigurationType.Auto).HasFlag(cfg.Value.Type) && memberKeys.Contains(cfg.Key))
        .ToDictionary(cfg => cfg.Key, cfg => cfg.Value);

    private void GuardUndefinedMembers(Type registryType, IEnumerable<MemberInfo> membersToSet, IReadOnlyDictionary<string, RegistryPropertyConfig> config) {
        var undefinedConfigs = config.Where(cfg => !cfg.Value.SuppressErrors)
            .Select(cfg => cfg.Key)
            .Except(membersToSet.Select(prop => prop.Name), StringComparer.OrdinalIgnoreCase);

        if (undefinedConfigs.Any()) {
            var msg = "Configuration failed for the following non-existant";
            if (_publicOnly) {
                msg += " or non-public";
            }
            msg += $" {registryType.Name} members: {string.Join(", ", undefinedConfigs)}";
            throw new RegistryConfigurationException(msg);
        }
    }

    private IEnumerable<PropertyInfo> FilterUnsettablePropertiesOrThrow(Type registryType, IEnumerable<PropertyInfo> propertiesToSet, IReadOnlyDictionary<string, RegistryPropertyConfig> config) {
        var ignoreProps = config.Where(cfg => cfg.Value.SuppressErrors).Select(cfg => cfg.Key).ToArray();
        var settableProps = propertiesToSet.Where(HasValidSetter).ToArray();

        var unsettableProps = propertiesToSet
            .Where(prop => !ignoreProps.Contains(prop.Name, StringComparer.OrdinalIgnoreCase))
            .Except(settableProps)
            .Select(prop => prop.Name);

        if (unsettableProps.Any()) {
            var msg = $"Failed to configure {registryType.Name} because no";
            if (_publicOnly) {
                msg += " public";
            }
            msg += $" setter found for the following properties: {string.Join(", ", unsettableProps)}";
            throw new RegistryConfigurationException(msg);
        }

        return settableProps;
    }

    private bool HasValidSetter(PropertyInfo property) => (_publicOnly ? property.GetSetMethod() : property.SetMethod) != null;

    private IEnumerable<MemberInfo> GetMembersToConfigure(Type registryType, IReadOnlyDictionary<string, RegistryPropertyConfig> config) {
        var bindingFlags = BindingFlags.Instance | BindingFlags.Public;
        if (!_publicOnly) {
            bindingFlags |= BindingFlags.NonPublic;
        }

        return registryType.GetMembers(bindingFlags).Where(member => config.ContainsKey(member.Name));
    }

#if !NETSTANDARD2_0
    private bool TryExtractConfigForRegistry(Type registryType, [MaybeNullWhen(false)] out IReadOnlyDictionary<string, RegistryPropertyConfig> config) {
#else
    private bool TryExtractConfigForRegistry(Type registryType, out IReadOnlyDictionary<string, RegistryPropertyConfig> config) {
#endif
        config = new Dictionary<string, RegistryPropertyConfig>(StringComparer.OrdinalIgnoreCase);

        // Namespace + type-name first
        if (_registryConfig!.TryGetValue(registryType.FullName!, out config)) {
            return true;
        }

        // type-name without namespace
        if (_registryConfig.TryGetValue(registryType.Name, out config)) {
            return true;
        }

        // Wildcard matching
        var wildcard = '*';
        var wildcardMatch = _registryConfig.Where(entry => entry.Key.Contains(wildcard) && entry.Key.Length > 1)
            .OrderByDescending(entry => entry.Key.Replace(wildcard.ToString(), string.Empty).Length) // Get the most specific wildcard match
                .ThenBy(entry => entry.Key.Count(c => c == wildcard)) // Favor fewer wildcards when the lengths (without wildcards) match
            .FirstOrDefault(entry => registryType.FullName!.MatchWildcard(entry.Key, wildcard, comparison: StringComparison.OrdinalIgnoreCase))
            .Value;

        if (wildcardMatch is not null) {
            config = wildcardMatch;
            return true;
        }

        return false;
    }

}
