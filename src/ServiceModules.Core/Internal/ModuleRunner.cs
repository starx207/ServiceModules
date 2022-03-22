using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// TODO: I need to make some decisions about verbiage to use and be consistent throughout the codebase
namespace ServiceModules.Internal;
internal class ModuleRunner : IModuleRunner {
    private readonly IModuleActivator _activator;
    private readonly IModuleConfigApplicator _configMgr;

    public ModuleRunner(IModuleActivator activator, IModuleConfigApplicator configMgr) {
        _activator = activator;
        _configMgr = configMgr;
    }

    public void ApplyRegistries(IServiceCollection services, ModuleOptions options) {
        var modules = GetModulesToRun(options);
        _configMgr.InitializeFrom(options);

        foreach (var module in modules) {
            _configMgr.ApplyModuleConfiguration(module);
            module.ConfigureServices(services);
        }
    }

    private IEnumerable<IRegistryModule> GetModulesToRun(ModuleOptions options) {
        var modules = _activator.InstantiateModules(options);

        if (options.Environment is { } environment) {
            if (!typeof(IHostEnvironment).IsAssignableFrom(environment.GetType())) {
                throw new InvalidOperationException($"{nameof(options.Environment)} must implement {nameof(IHostEnvironment)}");
            }
            var envName = ((IHostEnvironment)environment).EnvironmentName;

            modules = modules.Where(module => module.TargetEnvironments.Count == 0
                || module.TargetEnvironments.Contains(envName, StringComparer.OrdinalIgnoreCase));
        }

        return modules;
    }
}
