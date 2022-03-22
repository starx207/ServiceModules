using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;

// TODO: I want to add the ability to configure the modules further via IConfiguration.
//       Use reflection to set public properties of the modules.
//         -> Need to be able to specify the module name (and, optionally, namespace)
//       What about global configuration settings?
//         -> Maybe being able to supress/turn-on errors? When would this be useful?
//         -> If no modules are found to apply, should we throw an error?
namespace ServiceModules;

/// <summary>
/// A module containing a set of services to register with the <see cref="IServiceCollection"/>
/// </summary>
public interface IRegistryModule {
    /// <summary>
    /// The environments to which the module should be applied.
    /// If none set, will be applied to all environments
    /// </summary>
    IReadOnlyCollection<string> TargetEnvironments { get; }

    /// <summary>
    /// Configure the services provided by the module
    /// </summary>
    void ConfigureServices(IServiceCollection services);

    /// <summary>
    /// The order in which modules should be registered (highest priority first)
    /// </summary>
    int Priority => 0;
}
