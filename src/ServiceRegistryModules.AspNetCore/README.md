# ServiceRegistryModules.AspNetCore
---
This packages extends the base `ServiceRegistryModules` package by adding extensions for the .NET 6 `WebApplicationBuilder`.
Usage is the same as the core package. The main difference being that The `IHostEnvironment` and `IConfiguration`
providers cannot be explicitly set when calling `ApplyRegistries()` since they are provided by the `WebApplicationBuilder`.

__TODO: provide link to documentation for "ServiceRegistryModules" package which is where I'll put the full implementation details.__