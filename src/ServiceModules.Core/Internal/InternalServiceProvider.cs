using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ServiceModules.Internal;
internal static class InternalServiceProvider {
    internal static IModuleRunner? ModuleRunnerTestOverride = null;
    private static readonly IServiceProvider _moduleRunnerServices = new ServiceCollection() {
        ServiceDescriptor.Transient<IModuleRunner, ModuleRunner>(),
        ServiceDescriptor.Transient<IModuleActivator, ModuleActivator>(),
        ServiceDescriptor.Transient<IModuleConfigApplicator, ModuleConfigApplicator>(),
        ServiceDescriptor.Transient<IModuleConfigLoader, ModuleConfigLoader>()
    }.BuildServiceProvider();

    public static IModuleRunner GetModuleRunner(IServiceCollection intermediateServices)
        => ModuleRunnerTestOverride ?? _moduleRunnerServices.GetRequiredService<IModuleRunner>();
}
