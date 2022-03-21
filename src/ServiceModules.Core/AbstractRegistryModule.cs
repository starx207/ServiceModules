using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;

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
