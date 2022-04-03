using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;

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
