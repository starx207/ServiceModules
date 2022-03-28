using System;
using System.Collections.Generic;
using System.Linq;

namespace ServiceModules.Internal;
// TODO: I want to be able to supress configuration errors for specific properties. Errors will be on be default, but users can turn them off if needed.
//       So the value of a property can be either the value to set the property to, or an array(or object?) where the first element is the property value,
//       and the second is a boolean indicating whether error supression is on.
internal class ModuleConfigLoader : IModuleConfigLoader {
    // TODO: provide a nicer return type that can be more easily referenced elsewhere
    public Dictionary<string, IReadOnlyDictionary<string, string>>? LoadFrom(ModuleOptions options) {
        if (options.Configuration is null) {
            return null;
        }

        if (string.IsNullOrWhiteSpace(options.ModuleConfigSectionKey)) {
            throw new ArgumentException($"'{nameof(options.ModuleConfigSectionKey)}' cannot be null or whitespace.", nameof(options));
        }

        var moduleConfig = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        Dictionary<string, string> ModuleConfigAsDict(string key) => (Dictionary<string, string>)moduleConfig[key];

        var configSection = options.Configuration.GetSection(options.ModuleConfigSectionKey);
        foreach (var moduleSection in configSection.GetChildren()) {
            if (!moduleConfig.ContainsKey(moduleSection.Key)) {
                moduleConfig[moduleSection.Key] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            foreach (var propertySection in moduleSection.GetChildren()) {
                ModuleConfigAsDict(moduleSection.Key)[propertySection.Key] = propertySection.Value;
            }
        }

        var emptyKeys = moduleConfig.Where(entry => !entry.Value.Any()).Select(entry => entry.Key);
        foreach (var key in emptyKeys) {
            moduleConfig.Remove(key);
        }

        return moduleConfig;
    }
}
