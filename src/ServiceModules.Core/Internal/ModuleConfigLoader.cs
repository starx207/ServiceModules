using System;

namespace ServiceModules.Internal;
// TODO: I want to be able to supress configuration errors for specific properties. Errors will be on be default, but users can turn them off if needed.
//       So the value of a property can be either the value to set the property to, or an array(or object?) where the first element is the property value,
//       and the second is a boolean indicating whether error supression is on.
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
                moduleConfig.AddPropertyTo(moduleSection.Key, propertySection.Key, propertySection.Value);
            }
        }

        return moduleConfig;
    }
}
