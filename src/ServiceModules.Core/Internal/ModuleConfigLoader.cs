using System;

namespace ServiceModules.Internal;
internal class ModuleConfigLoader : IModuleConfigLoader {
    public ModuleConfiguration? LoadFrom(ModuleOptions options) {
        if (options.Configuration is null) {
            return null;
        }

        if (string.IsNullOrWhiteSpace(options.ModuleConfigSectionKey)) {
            throw new ArgumentException($"'{nameof(options.ModuleConfigSectionKey)}' cannot be null or whitespace.", nameof(options));
        }

        var moduleConfig = new ModuleConfiguration();

        var configSection = options.Configuration.GetSection(options.ModuleConfigSectionKey);
        foreach (var moduleSection in configSection.GetChildren()) {
            foreach (var propertySection in moduleSection.GetChildren()) {
                if (propertySection.Value is { }) {
                    moduleConfig.AddPropertyTo(moduleSection.Key, propertySection.Key, propertySection.Value);
                } else {
                    var value = propertySection.GetSection(nameof(ModulePropertyConfig.Value)).Value;
                    bool.TryParse(propertySection.GetSection(nameof(ModulePropertyConfig.SuppressErrors)).Value, out var suppressErr);
                    moduleConfig.AddPropertyTo(moduleSection.Key, propertySection.Key, new ModulePropertyConfig() {
                        Value = value,
                        SuppressErrors = suppressErr
                    });
                }
            }
        }

        return moduleConfig;
    }
}
