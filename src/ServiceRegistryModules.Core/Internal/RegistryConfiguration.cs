using System;
using System.Collections.Generic;

namespace ServiceRegistryModules.Internal;
internal class RegistryConfiguration : Dictionary<string, IReadOnlyDictionary<string, RegistryPropertyConfig>> {
    public RegistryConfiguration() : base(StringComparer.OrdinalIgnoreCase) {
    }

    public void AddPropertyTo(string registry, string propertyName, string propertyValue)
        => AddPropertyTo(registry, propertyName, new RegistryPropertyConfig() { Value = propertyValue });

    public void AddPropertyTo(string registry, string propertyName, RegistryPropertyConfig propertyConfig) {
        if (!ContainsKey(registry)) {
            AddRegistry(registry);
        }

        ((Dictionary<string, RegistryPropertyConfig>)this[registry]).Add(propertyName, propertyConfig);
    }

    private void AddRegistry(string name)
        => this[name] = new Dictionary<string, RegistryPropertyConfig>(StringComparer.OrdinalIgnoreCase);

}
