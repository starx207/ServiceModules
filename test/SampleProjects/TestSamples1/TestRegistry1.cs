using Microsoft.Extensions.DependencyInjection;
using ServiceModules;

namespace TestSamples1;
public class TestRegistry1 : AbstractRegistryModule {
    public override void ConfigureServices(IServiceCollection services)
        => services.AddTransient<Service>();

    public class Service { }
}
