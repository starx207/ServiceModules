using System;

namespace ServiceRegistryModules.Internal;
internal class RegistryConfigLoader : IRegistryConfigLoader {
    public RegistryConfiguration? LoadFrom(RegistryOptions options) {
        if (options.Configuration is null) {
            return null;
        }

        if (string.IsNullOrWhiteSpace(options.RegistryConfigSectionKey)) {
            throw new ArgumentException($"'{nameof(options.RegistryConfigSectionKey)}' cannot be null or whitespace.", nameof(options));
        }

        var registryConfig = new RegistryConfiguration();

        var configSection = options.Configuration.GetSection(options.RegistryConfigSectionKey);
        foreach (var registrySection in configSection.GetChildren()) {
            foreach (var propertySection in registrySection.GetChildren()) {
                if (propertySection.Value is { }) {
                    registryConfig.AddPropertyTo(registrySection.Key, propertySection.Key, propertySection.Value);
                } else {
                    var value = propertySection.GetSection(nameof(RegistryPropertyConfig.Value)).Value;
                    bool.TryParse(propertySection.GetSection(nameof(RegistryPropertyConfig.SuppressErrors)).Value, out var suppressErr);
                    registryConfig.AddPropertyTo(registrySection.Key, propertySection.Key, new RegistryPropertyConfig() {
                        Value = value,
                        SuppressErrors = suppressErr
                    });
                }
            }
        }

        return registryConfig;
    }
}
