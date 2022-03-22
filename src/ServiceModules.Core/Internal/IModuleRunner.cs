using Microsoft.Extensions.DependencyInjection;

namespace ServiceModules.Internal;

internal interface IModuleRunner {
    void ApplyRegistries(IServiceCollection services, ModuleOptions options);
}
