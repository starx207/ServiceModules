using System;
using Microsoft.Extensions.DependencyInjection;
using ServiceRegistryModules;

namespace TestSamples2;
public class TestRegistry2 : AbstractRegistryModule {
    public event EventHandler<IServiceCollection>? OnConfigure;

    public override void ConfigureServices(IServiceCollection services)
        => OnConfigure?.Invoke(this, services);

    public class Service { }
}
