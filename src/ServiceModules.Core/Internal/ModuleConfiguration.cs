using System;
using System.Collections.Generic;

namespace ServiceModules.Internal;
internal class ModuleConfiguration : Dictionary<string, IReadOnlyDictionary<string, string>> {
    public ModuleConfiguration() : base(StringComparer.OrdinalIgnoreCase) {
    }

    public void AddModule(string name)
        => this[name] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public void AddPropertyTo(string module, string propertyName, string propertyValue) {
        if (!ContainsKey(module)) {
            AddModule(module);
        }

        ((Dictionary<string, string>)this[module]).Add(propertyName, propertyValue);
    }
}
