using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace ServiceRegistryModules.Internal;
internal class RegistryOptions {
    public List<IRegistryModule> Registries { get; set; }
    public List<Type> RegistryTypes { get; set; }
    public List<object> Providers { get; set; }
    public List<Type> AllowedRegistryCtorArgTypes { get; set; }
    public bool PublicOnly { get; set; }
    public IConfiguration? Configuration { get; set; }
    public object? Environment { get; set; }
    public string RegistryConfigSectionKey { get; set; } = "service_registries";

    public RegistryOptions() {
        Registries = new();
        RegistryTypes = new();
        Providers = new();
        AllowedRegistryCtorArgTypes = new();
    }
}
