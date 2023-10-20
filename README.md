# ServiceRegistryModules
---
ServiceRegistryModules is a package that allows you use modules to register services using Microsoft's native
`IServiceCollection`. Some of the benefits you get from this are:
1. Reducing the size of bloated `Startup` classes
1. Ability to move service registration closer to servies being registered
1. Add custom providers to inject into the registry modules
1. Using the built-in `IConfiguration` to dynamically change runtime services
1. Integration with the .NET 6 `WebApplicationBuilder`

## Install the packages
There are 3 available packages to choose from to suit your needs:
1. `ServiceRegistryModules` - Provides the core funtionality for creating registry modules for your services.
1. `ServiceRegistryModules.Abstractions` - Contains just the abstract classes and interfaces needed to implement a registry. Use this
in projects where you want to define a registry, but don't need the ability to apply the registry (Included in `ServiceRegistryModules`).
1. `ServiceRegistryModules.AspNetCore` - Extends `ServiceRegistryModules` by adding integration with the `WebApplicationBuilder` introduced in .NET 6.

## Defining a registry
To define a registry, create a class that implements `IRegistryModule`. This interface contains the following three implementation details:
1. `ConfigureServices(IServiceCollection)` - The method that gets run to register your custom services.
1. `TargetEnvironments` - A collection of strings indicating which environments the registry should apply to. If it should be applied
regardless of environment, set this to an empty collection.
1. `Priority` - An integer used to control the order registries are applied in (highest priority is applied first). This is useful, for instance, if you are running
an integration test and you need to override services registered by the app - you can define a registry with a negative priority to ensure
it is applied last.

There is also an abstract helper class called `AbstractRegistryModule` which provides defaults for `Priority` and `TargetEnvironments` so all you
have to do is provide the implementation of `ConfigureServices`.

### Examples
``` c#
public class HighPriorityRegistry : IRegistryModule {
    public IReadOnlyCollection<string> TargetEnvironments => Array.Empty<string>();
    // Any registry with a lower priority than 1000 will be registered AFTER this one
    public int Priority => 1000;

    public void ConfigureServices(IServiceCollection services) {
        // Custom service registration here
    }
}

public class DevelopmentRegistry : AbstractRegistryModule {
    // This registry will only be applied if the host environment name is "Development" (case-insensitive)
    public override IReadOnlyCollection<string> TargetEnvironments => new[] { "Development" };

    public override void ConfigureServices(IServiceCollection services) {
        // Custom service registration here
    }
}

public class SimpleRegistry : AbstractRegistryModule {
    /*
        Simplest way to create a registry module.
        The TargetEnvironments default to an empty array
        and the Priority defaults to zero.
    */
    public override void ConfigureServices(IServiceCollection services) {
        // Custom service registration here
    }
}
```

> IMPORTANT! TargetEnviornments will only be enforced so long as the code that is applying the registries
has provided an environment to use. More on that below.

## Applying the registries
Registries are applied by calling the `IServiceCollection` extension method `ApplyRegistries()`. When calling this method
you can provide a set of assemblies that will be scanned for `IRegistryModule` implementations, or you can use the 
configuration builder to have more control of how the registries are discovered and applied.

A few of the most important configuration builder options are
- Ability to define providers that can be injected into the registries.
- Defining the `IHostEnvironment` to provide to the registries (and use for enforcing the `TargetEnvironments`).*
- Defining the `IConfiguration` to provide to the registries.*
- Controlling whether to apply internal/private registries or only public registries.

> *If using ServiceRegistryModules.AspNetCore, the same extensions can be used directly on the WebApplicationBuilder.
This also provides the benefit of automatically using the Environment and Configuration provided by WebApplicationBuilder

### Examples
``` c#
IServiceCollection services;
IHostEnvironment environment;
IConfiguration configuration;

// Scans the given assemblies for IRegistryModule implementations and applies them
services.ApplyRegistries(typeof(MyRegistry).Assembly, typeof(MyOtherRegistry).Assembly);

// Scans the assembly in which "MyRegistry" is defined for IRegistryModule implementations
// and applies any public registries found there, injecting the given environment and configuration
// into the registries if required.
services.ApplyRegistries(config =>
    config.PublicOnly()
          .FromAssemblyOf<MyRegistry>()
          .UsingEnvironment(environment)
          .UsingConfiguration(configuration)
);

// Applies the given registry types (regardless of access modifiers). If either registry
// has a constructor that requires a string or boolean parameter, they will be given
// "a string to inject" and true (respectively).
services.ApplyRegistries(config =>
    config.OfTypes(typeof(MyRegistry), typeof(MyOtherRegistry))
          .UsingProviders("a string to inject", true)
);

// Applies only the given registry instance
services.ApplyRegistries(config => config.From(new MyRegistry()));
```

> If ApplyRegistries() is called without providing either an assembly to scan, a set of registry types,
or any registry instances, then the assembly from which the extension was called will be scanned for
IRegistryModule implementations.

## Registry configuration
You can provide runtime configuration for your registries by defining a section in any of the sources
used by your application's `IConfiguration`. Add a configuration key called "service_registries" to one
of your configuration sources (the key can be changed if you apply the registries using: `services.ApplyRegistries(config => config.WithConfigurationsFromSection("my_custom_key"))`).

This section is comprised of 3 subkeys:
1. `configuration` - Configuration for properties/events of registries before they are applied.
1. `add` - Additional registries to be loaded an applied.
1. `skip` - Registries that should NOT be applied even if loaded.

### Configuration section
Each entry under this key represents a registry you want to configure. And each registry configuration can
define properties or events of the registry to set before the registry is applied. The access modifiers of the members
and their setters don't matter unless your applying the registries using the `PublicOnly()` setting. Each member
can be configured with errorSuppression on or off (off by default). If error suppression is turned on for a member,
no errors will be thrown due to the member not existing, having incorrect access modifiers, or having an invalid value.

Registry keys are matched in the following order:
1. The full name of the registry is tried first (AssemblyName + TypeName).
1. The short name of the registry is tried next (TypeName only).
1. Wildcard matching is performed against the registry's full name. 
A wildcard character of "*" can be used to indicate 0 or more occurrances of any character.
   - Wildcard matches with the most specifity are given preferences. Meaning the longest set of non-wildcard characters
   wins. Ties are decided by the least number of wildcard characters.

When registries are being applied, if a configuration key is found that matches one of those 3 criteria,
the properties of the registry will be set according to the configured members listed.

#### Event configuration
Events on the registry can be configured by providing the fully qualified name of a static method to be used as
an event handler for the event. If the handler method is located in an assembly that is not referenced by the assembly
applying the registries, a `HintPath` may be specified to dicate where the assembly can be loaded from.

### Dynamically added registries
This key is an array of registries to add at runtime. The registry can either be defined just by specifying its fully
qualified name *or* by defining its `FullName`, `SuppressErrors` and `HintPath` properties (use `HintPath` to load a registry
from an unreferenced assembly).

### Dynamically skipped registries
This key is an array of registries to skip at runtime and consists of the fully qualified names of the registries to skip.
Any registry in this list that is not loaded, will be ignored.

### Examples
``` json
{
    "service_registries": {
        "add": [
            "AnAssemblyWith.RegistryToAdd",
            {
                "FullName": "UnreferencedAssembly.Registry",
                "SuppressErrors": true,
                "HintPath": "../path/to/assembly"
            }
        ],
        "skip": [
            "AnAssemblyWith.RegistryToSkip"
        ],
        "configuration": {
            "*Registry*": {
                "Property1": 30
            }, 

            "AnotherRegistry": {
                "APropertyToSet": "the new value"
            },

            "MyAssembly.With.A.RegistryModule": {
                "Property1": "runtime value",
                "AnotherProperty": false,
                "PropertyWithErrorSuppression": {
                    "Value": "the value to assign to the property",
                    "SuppressErrors": true
                }
            },

            "MyAssembly.*": {
                "PropertyToSetInAllRegistiresInMyAssembly": {
                    "Value": true,
                    "SuppressErrors": true
                },
                "EventToAddHandlerFor": {
                    "Value": "SomeAssembly.StaticMethod.ForEventHandler",
                    "HintPath": "../path/to/unreferenced/handler/assembly"
                }
            }
        }
    }
}
```
In the above example: 
- `SomeAssembly.AnotherRegistry` would match the 2nd and 1st configurations. The 2nd one would be applied.
- `MyAssembly.With.A.RegistryModule` would match the 1st, 3rd and 4th configurations. The 3rd one would be applied.
- `MyAssembly.MyRegistry.Module` would match the 1st and 4th configurations. The 4th one would be applied.
