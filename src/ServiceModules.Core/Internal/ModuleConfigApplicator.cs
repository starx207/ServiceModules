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
        GuardUnsettableProperties(moduleType, propertiesToSet);

        foreach (var prop in propertiesToSet) {
            var converter = TypeDescriptor.GetConverter(prop.PropertyType);
            // TODO: This will throw a NotSupportedException if the conversion can't be completed.
            //       Do I want that? Should I handle the exception? Should I throw my own exception?
            prop.SetValue(module, converter.ConvertFrom(config[prop.Name]));
        }
    }

    private void GuardUndefinedProperties(Type moduleType, IEnumerable<PropertyInfo> propertiesToSet, IReadOnlyDictionary<string, string> config) {
        var undefinedConfigs = config.Keys.Except(propertiesToSet.Select(prop => prop.Name), StringComparer.OrdinalIgnoreCase);
        if (undefinedConfigs.Any()) {
            var msg = "Configuration failed for the following non-existant";
            if (_publicOnly) {
                msg += " or non-public";
            }
            msg += $" {moduleType.Name} properties: {string.Join(", ", undefinedConfigs)}";
            throw new InvalidOperationException(msg);
        }
    }

    private void GuardUnsettableProperties(Type moduleType, IEnumerable<PropertyInfo> propertiesToSet) {
        var unsettableProps = propertiesToSet.Where(HasInvalidSetter).Select(prop => prop.Name);
        if (unsettableProps.Any()) {
            var msg = $"Failed to configure {moduleType.Name} because no";
            if (_publicOnly) {
                msg += " public";
            }
            msg += $" setter found for the following properties: {string.Join(", ", unsettableProps)}";
            throw new InvalidOperationException(msg);
        }
    }

    private bool HasInvalidSetter(PropertyInfo property) => (_publicOnly ? property.GetSetMethod() : property.SetMethod) == null;

    private IEnumerable<PropertyInfo> GetPropertiesToConfigure(Type moduleType, IReadOnlyDictionary<string, string> config) {
        var bindingFlags = BindingFlags.Instance | BindingFlags.Public;
        if (!_publicOnly) {
            bindingFlags |= BindingFlags.NonPublic;
        }

        return moduleType.GetProperties(bindingFlags).Where(prop => config.ContainsKey(prop.Name));
    }

    private bool TryExtractConfigForModule(Type moduleType, out IReadOnlyDictionary<string, string> config) {
        config = new Dictionary<string, string>();

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
