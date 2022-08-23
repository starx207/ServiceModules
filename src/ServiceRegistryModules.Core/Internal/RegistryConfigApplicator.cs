using System;
using System.Collections.Generic;
using System.ComponentModel;
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

        var propertiesToSet = GetPropertiesToConfigure(registryType, config);
        GuardUndefinedProperties(registryType, propertiesToSet, config);
        propertiesToSet = FilterUnsettablePropertiesOrThrow(registryType, propertiesToSet, config);

        foreach (var prop in propertiesToSet) {
            var converter = TypeDescriptor.GetConverter(prop.PropertyType);
            try {
                prop.SetValue(registry, converter.ConvertFrom(config[prop.Name].Value));
            } catch (Exception ex) {
                if (!config.TryGetValue(prop.Name, out var value) || !value.SuppressErrors) {
                    throw new RegistryConfigurationException($"Unable to set {prop.Name} value to configured value.", ex);
                }
            }
        }
    }

    private void GuardUndefinedProperties(Type registryType, IEnumerable<PropertyInfo> propertiesToSet, IReadOnlyDictionary<string, RegistryPropertyConfig> config) {
        var undefinedConfigs = config.Where(cfg => !cfg.Value.SuppressErrors)
            .Select(cfg => cfg.Key)
            .Except(propertiesToSet.Select(prop => prop.Name), StringComparer.OrdinalIgnoreCase);

        if (undefinedConfigs.Any()) {
            var msg = "Configuration failed for the following non-existant";
            if (_publicOnly) {
                msg += " or non-public";
            }
            msg += $" {registryType.Name} properties: {string.Join(", ", undefinedConfigs)}";
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

    private IEnumerable<PropertyInfo> GetPropertiesToConfigure(Type registryType, IReadOnlyDictionary<string, RegistryPropertyConfig> config) {
        var bindingFlags = BindingFlags.Instance | BindingFlags.Public;
        if (!_publicOnly) {
            bindingFlags |= BindingFlags.NonPublic;
        }

        return registryType.GetProperties(bindingFlags).Where(prop => config.ContainsKey(prop.Name));
    }

    private bool TryExtractConfigForRegistry(Type registryType, out IReadOnlyDictionary<string, RegistryPropertyConfig> config) {
        config = new Dictionary<string, RegistryPropertyConfig>(StringComparer.OrdinalIgnoreCase);

        // Namespace + type-name first
        if (_registryConfig!.TryGetValue(registryType.FullName, out config)) {
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
            .FirstOrDefault(entry => registryType.FullName.MatchWildcard(entry.Key, wildcard, comparison: StringComparison.OrdinalIgnoreCase))
            .Value;

        if (wildcardMatch is not null) {
            config = wildcardMatch;
            return true;
        }

        return false;
    }

}
