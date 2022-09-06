using System;
using ServiceRegistryModules.Exceptions;

namespace ServiceRegistryModules.Internal;
internal class RegistryConfigLoader : IRegistryConfigLoader {
    public RegistryConfiguration? LoadFrom(RegistryOptions options) {
        if (options.Configuration is null) {
            return null;
        }

        if (string.IsNullOrWhiteSpace(options.RegistryConfigSectionKey)) {
            throw new RegistryConfigurationException($"'{nameof(options.RegistryConfigSectionKey)}' cannot be null or whitespace.");
        }

        var configKey = $"{options.RegistryConfigSectionKey}:{ServiceRegistryModulesDefaults.CONFIGURATION_KEY}";
        var registryConfig = new RegistryConfiguration();

        var configSection = options.Configuration.GetSection(configKey);
        foreach (var registrySection in configSection.GetChildren()) {
            foreach (var propertySection in registrySection.GetChildren()) {
                if (propertySection.Value is { }) {
                    registryConfig.AddPropertyTo(registrySection.Key, propertySection.Key, propertySection.Value);
                } else {
                    var value = propertySection.GetSection(nameof(RegistryPropertyConfig.Value)).Value;
                    var hintPath = propertySection.GetSection(nameof(RegistryPropertyConfig.HintPath)).Value;
                    bool.TryParse(propertySection.GetSection(nameof(RegistryPropertyConfig.SuppressErrors)).Value, out var suppressErr);
                    Enum.TryParse<ConfigurationType>(propertySection.GetSection(nameof(RegistryPropertyConfig.Type)).Value, true, out var type);

                    if (type == ConfigurationType.Config) {
                        var newValue = options.Configuration[value];
                        if (newValue is null) {
                            if (suppressErr) {
                                continue;
                            }
                            throw new RegistryConfigurationException($"Unable to resolve configuration key for '{value}'");
                        }
                        value = newValue;
                        type = ConfigurationType.Auto;
                    }

                    var config = new RegistryPropertyConfig() {
                        Value = value,
                        SuppressErrors = suppressErr,
                        Type = type,
                        HintPath = hintPath
                    };

                    registryConfig.AddPropertyTo(registrySection.Key, propertySection.Key, config);
                }
            }
        }

        return registryConfig;
    }
}
