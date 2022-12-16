using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;

namespace ServiceRegistryModules;

/// <summary>
/// A module containing a set of services to register with the <see cref="IServiceCollection"/>
/// </summary>
public interface IRegistryModule {
    /// <summary>
    /// The environments to which the registry should be applied.
    /// If none set, will be applied to all environments
    /// </summary>
    IReadOnlyCollection<string> TargetEnvironments { get; }

    /// <summary>
    /// Configure the services provided by this registry
    /// </summary>
    void ConfigureServices(IServiceCollection services);

    /// <summary>
    /// The order in which registry modules should be applied (highest priority first)
    /// </summary>
#if NETSTANDARD2_1_OR_GREATER
    int Priority => 0;
#else
    int Priority { get; }
#endif
}
