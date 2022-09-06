using Microsoft.Extensions.DependencyInjection;
using ServiceRegistryModules;

namespace UnreferencedTestSamples;
public class TestRegistry1 : AbstractRegistryModule {
    public override void ConfigureServices(IServiceCollection services) {

    }

    public class Service { }

    public static void OnConfigureServicesHandler(object sender, IServiceCollection services)
        => services.AddSingleton<Service>();

}
