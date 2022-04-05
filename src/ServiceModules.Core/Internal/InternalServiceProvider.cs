using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ServiceRegistryModules.Internal;
internal static class InternalServiceProvider {
    internal static IRegistryRunner? RegistryRunnerTestOverride = null;
    private static readonly IServiceProvider _registryRunnerServices = new ServiceCollection() {
        ServiceDescriptor.Transient<IRegistryRunner, RegistryRunner>(),
        ServiceDescriptor.Transient<IRegistryActivator, RegistryActivator>(),
        ServiceDescriptor.Transient<IRegistryConfigApplicator, RegistryConfigApplicator>(),
        ServiceDescriptor.Transient<IRegistryConfigLoader, RegistryConfigLoader>()
    }.BuildServiceProvider();

    public static IRegistryRunner GetRegistryRunner()
        => RegistryRunnerTestOverride ?? _registryRunnerServices.GetRequiredService<IRegistryRunner>();
}
