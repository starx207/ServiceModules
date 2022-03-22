using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;

namespace ServiceModules.Internal;
internal class ModuleOptions {
    public List<IRegistryModule> Modules { get; set; }
    public List<Type> ModuleTypes { get; set; }
    public List<object> Providers { get; set; }
    public List<Type> AllowedModuleArgTypes { get; set; }
    public bool PublicOnly { get; set; }
    public IConfiguration? Configuration { get; set; }
    public object? Environment { get; set; }
    public string ModuleConfigSectionKey { get; set; } = "registry_modules";

    public ModuleOptions() {
        Modules = new();
        ModuleTypes = new();
        Providers = new();
        AllowedModuleArgTypes = new();
    }
}
