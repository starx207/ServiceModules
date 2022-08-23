using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ServiceRegistryModules.Exceptions;

namespace ServiceRegistryModules.Internal;
internal class RegistryRunner : IRegistryRunner {
    private readonly IRegistryActivator _activator;
    private readonly IRegistryConfigApplicator _configMgr;

    public RegistryRunner(IRegistryActivator activator, IRegistryConfigApplicator configMgr) {
        _activator = activator;
        _configMgr = configMgr;
    }

    public void ApplyRegistries(IServiceCollection services, RegistryOptions options) {
        var registries = GetRegistriesToRun(options);
        _configMgr.InitializeFrom(options);

        foreach (var registry in registries) {
            _configMgr.ApplyRegistryConfiguration(registry);
            registry.ConfigureServices(services);
        }
    }

    private IEnumerable<IRegistryModule> GetRegistriesToRun(RegistryOptions options) {
        var registries = _activator.InstantiateRegistries(options);

        if (options.Environment is { } environment) {
            if (!typeof(IHostEnvironment).IsAssignableFrom(environment.GetType())) {
                throw new RegistryConfigurationException($"{nameof(options.Environment)} must implement {nameof(IHostEnvironment)}");
            }
            var envName = ((IHostEnvironment)environment).EnvironmentName;

            registries = registries.Where(registry => registry.TargetEnvironments.Count == 0
                || registry.TargetEnvironments.Contains(envName, StringComparer.OrdinalIgnoreCase));
        }

        return registries;
    }
}
