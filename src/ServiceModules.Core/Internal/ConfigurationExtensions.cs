using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ServiceModules.Internal;
internal static class ConfigurationExtensions {
    public static Dictionary<string, IReadOnlyDictionary<string, string>> GetModuleConfig(this IConfiguration configuration, string sectionKey) {
        if (string.IsNullOrWhiteSpace(sectionKey)) {
            throw new ArgumentException($"'{nameof(sectionKey)}' cannot be null or whitespace.", nameof(sectionKey));
        }

        var moduleConfig = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        Dictionary<string, string> ModuleConfigAsDict(string key) => (Dictionary<string, string>)moduleConfig[key];

        var configSection = configuration.GetSection(sectionKey);
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
