using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ServiceModules.Internal;
// TODO: Is this a good way to do this? My thinking was that this would make writing the tests easier,
//       but does it over-complicate things?
internal static class InternalServiceProvider {
    private static readonly ServiceDescriptor[] _internalServices = new[] {
        ServiceDescriptor.Transient<IModuleRunner, ModuleRunner>(),
        ServiceDescriptor.Transient<IModuleActivator, ModuleActivator>(),
        ServiceDescriptor.Transient<IModuleConfigApplicator, ModuleConfigApplicator>(),
        ServiceDescriptor.Transient<IModuleConfigLoader, ModuleConfigLoader>()
    };

    public static void EnsureInternalServices(IServiceCollection services) {
        foreach (var descriptor in _internalServices) {
            services.TryAdd(descriptor);
        }
    }

    public static void CleanupInternalServices(IServiceCollection services) {
        foreach (var descriptor in _internalServices) {
            services.RemoveAll(descriptor.ServiceType);
        }
    }

    // TODO: Can I provide a static property here to use for Test overrides?
    public static IModuleRunner ResolveModuleRunner(IServiceCollection intermediateServices)
        => intermediateServices.BuildServiceProvider().GetRequiredService<IModuleRunner>();
}
