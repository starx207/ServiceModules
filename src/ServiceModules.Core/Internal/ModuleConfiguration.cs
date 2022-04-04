using System;
using System.Collections.Generic;

namespace ServiceModules.Internal;
internal class ModuleConfiguration : Dictionary<string, IReadOnlyDictionary<string, ModulePropertyConfig>> {
    public ModuleConfiguration() : base(StringComparer.OrdinalIgnoreCase) {
    }

    public void AddModule(string name)
        => this[name] = new Dictionary<string, ModulePropertyConfig>(StringComparer.OrdinalIgnoreCase);

    public void AddPropertyTo(string module, string propertyName, string propertyValue)
        => AddPropertyTo(module, propertyName, new ModulePropertyConfig() { Value = propertyValue });

    public void AddPropertyTo(string module, string propertyName, ModulePropertyConfig propertyConfig) {
        if (!ContainsKey(module)) {
            AddModule(module);
        }

        ((Dictionary<string, ModulePropertyConfig>)this[module]).Add(propertyName, propertyConfig);
    }
}
