using Microsoft.Extensions.DependencyInjection;

namespace ServiceRegistryModules.Internal;

internal interface IRegistryRunner {
    void ApplyRegistries(IServiceCollection services, RegistryOptions options);
}
