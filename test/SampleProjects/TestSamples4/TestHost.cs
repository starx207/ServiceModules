using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ServiceRegistryModules;
using TestSamples1;

namespace TestSamples4;
public static class TestHost {
    public static void ConfigureServices(IConfiguration config) {
        var services = new ServiceCollection();
        services.ApplyRegistries(opt =>
            opt.FromAssemblyOf<TestRegistry2>()
                .UsingConfiguration(config)
        );
    }
}
