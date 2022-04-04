using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace ServiceModules.Internal;
internal class ModuleConfigApplicator : IModuleConfigApplicator {
    private ModuleConfiguration? _moduleConfig;
    private bool _publicOnly;
    private readonly IModuleConfigLoader _configLoader;

    public ModuleConfigApplicator(IModuleConfigLoader configLoader) => _configLoader = configLoader;

    public void InitializeFrom(ModuleOptions options) {
        _moduleConfig = _configLoader.LoadFrom(options);
        _publicOnly = options.PublicOnly;
    }

    public void ApplyModuleConfiguration(IRegistryModule module) {
        if (_moduleConfig is null) {
            return;
        }

        var moduleType = module.GetType();
        if (!TryExtractConfigForModule(moduleType, out var config)) {
            return;
        }

        var propertiesToSet = GetPropertiesToConfigure(moduleType, config);
        GuardUndefinedProperties(moduleType, propertiesToSet, config);
        propertiesToSet = FilterUnsettablePropertiesOrThrow(moduleType, propertiesToSet, config);

        foreach (var prop in propertiesToSet) {
            var converter = TypeDescriptor.GetConverter(prop.PropertyType);
            try {
                prop.SetValue(module, converter.ConvertFrom(config[prop.Name].Value));
            } catch {
                if (!config.TryGetValue(prop.Name, out var value) || !value.SuppressErrors) {
                    throw;
                }
            }
        }
    }

    private void GuardUndefinedProperties(Type moduleType, IEnumerable<PropertyInfo> propertiesToSet, IReadOnlyDictionary<string, ModulePropertyConfig> config) {
        var undefinedConfigs = config.Where(cfg => !cfg.Value.SuppressErrors)
            .Select(cfg => cfg.Key)
            .Except(propertiesToSet.Select(prop => prop.Name), StringComparer.OrdinalIgnoreCase);

        if (undefinedConfigs.Any()) {
            var msg = "Configuration failed for the following non-existant";
            if (_publicOnly) {
                msg += " or non-public";
            }
            msg += $" {moduleType.Name} properties: {string.Join(", ", undefinedConfigs)}";
            throw new InvalidOperationException(msg);
        }
    }

    private IEnumerable<PropertyInfo> FilterUnsettablePropertiesOrThrow(Type moduleType, IEnumerable<PropertyInfo> propertiesToSet, IReadOnlyDictionary<string, ModulePropertyConfig> config) {
        var ignoreProps = config.Where(cfg => cfg.Value.SuppressErrors).Select(cfg => cfg.Key).ToArray();
        var settableProps = propertiesToSet.Where(HasValidSetter).ToArray();

        var unsettableProps = propertiesToSet
            .Where(prop => !ignoreProps.Contains(prop.Name, StringComparer.OrdinalIgnoreCase))
            .Except(settableProps)
            .Select(prop => prop.Name);

        if (unsettableProps.Any()) {
            var msg = $"Failed to configure {moduleType.Name} because no";
            if (_publicOnly) {
                msg += " public";
            }
            msg += $" setter found for the following properties: {string.Join(", ", unsettableProps)}";
            throw new InvalidOperationException(msg);
        }

        return settableProps;
    }

    private bool HasValidSetter(PropertyInfo property) => (_publicOnly ? property.GetSetMethod() : property.SetMethod) != null;

    private IEnumerable<PropertyInfo> GetPropertiesToConfigure(Type moduleType, IReadOnlyDictionary<string, ModulePropertyConfig> config) {
        var bindingFlags = BindingFlags.Instance | BindingFlags.Public;
        if (!_publicOnly) {
            bindingFlags |= BindingFlags.NonPublic;
        }

        return moduleType.GetProperties(bindingFlags).Where(prop => config.ContainsKey(prop.Name));
    }

    private bool TryExtractConfigForModule(Type moduleType, out IReadOnlyDictionary<string, ModulePropertyConfig> config) {
        config = new Dictionary<string, ModulePropertyConfig>(StringComparer.OrdinalIgnoreCase);

        // Namespace + type-name first
        if (_moduleConfig!.TryGetValue(moduleType.FullName, out config)) {
            return true;
        }

        // type-name without namespace
        if (_moduleConfig.TryGetValue(moduleType.Name, out config)) {
            return true;
        }

        // Wildcard matching
        var wildcard = '*';
        var wildcardMatch = _moduleConfig.Where(entry => entry.Key.Contains(wildcard) && entry.Key.Length > 1)
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

}
