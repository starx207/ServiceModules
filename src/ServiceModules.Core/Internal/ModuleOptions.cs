using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ServiceModules.Internal;
internal class ModuleOptions {
    public List<IRegistryModule> Modules { get; set; }
    public List<Type> ModuleTypes { get; set; }
    public List<object> Providers { get; set; }
    public List<Type> AllowedModuleArgTypes { get; set; }
    public bool OnlyPublicModules { get; set; }
    public IConfiguration? Configuration { get; set; }
    public object? Environment { get; set; }

    public ModuleOptions() {
        Modules = new();
        ModuleTypes = new();
        Providers = new();
        AllowedModuleArgTypes = new();
    }
}
