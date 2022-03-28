using Microsoft.Extensions.DependencyInjection;
using ServiceModules;

namespace TestSamples1;
public class TestRegistry2 : AbstractRegistryModule {
    public override void ConfigureServices(IServiceCollection services)
        => services.AddSingleton<Service>();

    public class Service { }
}
