using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;

// NOTE: Not sure this is such a good idea anymore
// TODO: I want to add the ability to inject ILogger<T> into these modules.
//       Since logging typically isn't available at the time the services are being registered,
//       I will need to create some sort of deferred logger that will add messages to a queue
//       -- then that queue can be flushed to the actual logger once it is available
namespace ServiceModules;

/// <inheritdoc/>
public abstract class AbstractRegistryModule : IRegistryModule {
    /// <inheritdoc/>
    public virtual IReadOnlyCollection<string> TargetEnvironments => Array.Empty<string>();
    /// <inheritdoc/>
    public virtual int Priority => 0;
    /// <inheritdoc/>
    public abstract void ConfigureServices(IServiceCollection services);
}
