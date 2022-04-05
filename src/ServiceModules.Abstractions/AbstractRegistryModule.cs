using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;

// IDEA: It would be cool if I could figure out how to add the ability to inject ILogger<T> into these registries.
//       Since logging typically isn't available at the time the services are being registered,
//       I would need to create some sort of deferred logger. Perhaps it could add messages to a queue
//       then that queue could be flushed to the actual logger once it is available
namespace ServiceRegistryModules;

/// <inheritdoc/>
public abstract class AbstractRegistryModule : IRegistryModule {
    /// <inheritdoc/>
    public virtual IReadOnlyCollection<string> TargetEnvironments => Array.Empty<string>();
    /// <inheritdoc/>
    public virtual int Priority => 0;
    /// <inheritdoc/>
    public abstract void ConfigureServices(IServiceCollection services);
}
