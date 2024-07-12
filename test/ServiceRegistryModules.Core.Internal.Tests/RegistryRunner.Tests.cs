using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Moq;
using Moq.Language.Flow;
using ServiceRegistryModules.Exceptions;
using Xunit;

namespace ServiceRegistryModules.Internal.Tests;
public class RegistryRunner_Should {
    #region Tests
    [Fact]
    public void UseTheGivenOptions_ToInstantiateTheRegistries() {
        // Arrange
        var options = CreateOptions();
        var mock = new Dependencies();
        var service = CreateService(mock);

        RegistryOptions? actualOptions = null;
        mock.SetupInstantiateRegistries(callback: opt => actualOptions = opt);

        // Act
        service.ApplyRegistries(CreateServiceCollection(), options);

        // Assert
        actualOptions.Should().BeSameAs(options);
    }

    [Fact]
    public void PassTheGivenServices_ToEachRegistry() {
        // Arrange
        var expectedServices = CreateServiceCollection();

        var registries = new[] {
            CreateMockRegistry(),
            CreateMockRegistry(),
            CreateMockRegistry()
        };

        var mock = new Dependencies();
        var service = CreateService(mock);

        mock.SetupInstantiateRegistries(returnVal: registries.Select(m => m.Object));

        // Act
        service.ApplyRegistries(expectedServices, CreateOptions());

        // Assert
        foreach (var registry in registries) {
            registry.Verify(m => m.ConfigureServices(expectedServices), Times.Once());
        }
    }

    [Fact]
    public void ConfigureEachRegistry_BeforeApplyingItsServiceConfiguration() {
        // Arrange
        var registry = CreateMockRegistry();
        var mock = new Dependencies();
        var service = CreateService(mock);

        registry.Setup(m => m.ConfigureServices(It.IsAny<IServiceCollection>()))
            .Callback<IServiceCollection>(_ => {
                mock.Applicator.Verify(m => m.ApplyRegistryConfiguration(registry.Object),
                    Times.Once(), "Configuration not applied");
            }).Verifiable("Services not configured");

        mock.SetupInstantiateRegistries(returnVal: new[] { registry.Object });

        // Act
        service.ApplyRegistries(CreateServiceCollection(), CreateOptions());

        // Assert
        registry.Verify();
    }

    [Fact]
    public void UseTheGivenOptions_ToInitializeTheApplicatorOnce_BeforeApplyingToAnyRegistries() {
        // Arrange
        var registries = new[] {
            CreateMockRegistry(),
            CreateMockRegistry(),
            CreateMockRegistry()
        };
        var options = CreateOptions();
        var mock = new Dependencies();
        var service = CreateService(mock);

        mock.SetupInstantiateRegistries(returnVal: registries.Select(m => m.Object));

        RegistryOptions? actualOptions = null;
        mock.SetupInitializeFrom(opt => {
            actualOptions = opt;
            foreach (var registry in registries) {
                registry.Verify(m => m.ConfigureServices(It.IsAny<IServiceCollection>()),
                    Times.Never(), "Config not initialized before configuring services");
            }
        });

        // Act
        service.ApplyRegistries(CreateServiceCollection(), options);

        // Assert
        actualOptions.Should().BeSameAs(options);
        mock.Applicator.Verify(m => m.InitializeFrom(It.IsAny<RegistryOptions>()), Times.Once());
    }

    [Theory,
        MemberData(nameof(EnvironmentMatchInput), "development"),
        MemberData(nameof(EnvironmentMatchInput), "production"),
        MemberData(nameof(EnvironmentMatchInput), null),
        MemberData(nameof(EnvironmentMatchInput), "staging")]
    public void NotApplyConfigurations_OrConfigureServices_ForRegistriesThatDoNotMatchTheCurrentEnvironment(Mock<IRegistryModule>[] registries, string? environment, int[] expectedIndicies) {
        // Arrange
        var hostEnv = environment == null ? null : new TestEnvironment(environment);
        var options = CreateOptions(environment: hostEnv);
        var mock = new Dependencies();
        var service = CreateService(mock);

        mock.SetupInstantiateRegistries(returnVal: registries.Select(m => m.Object));

        // Act
        service.ApplyRegistries(CreateServiceCollection(), options);

        // Assert
        for (var i = 0; i < registries.Length; i++) {
            var times = expectedIndicies.Contains(i) ? Times.Once() : Times.Never();
            registries[i].Verify(m => m.ConfigureServices(It.IsAny<IServiceCollection>()),
                times, $"Registry {i} ConfigureServices");
            mock.Applicator.Verify(m => m.ApplyRegistryConfiguration(registries[i].Object),
                times, $"Registry {i} ApplyConfiguration");
        }
    }

    [Fact]
    public void ThrowAnException_WhenTheProvidedHostEnvironment_IsNotAnIHostEnvironment() {
        // Arrange
        var options = CreateOptions(environment: "not-a-valid-environment");
        var mock = new Dependencies();
        var service = CreateService(mock);

        // Act
        var action = () => service.ApplyRegistries(CreateServiceCollection(), options);

        // Assert
        action.Should().Throw<RegistryConfigurationException>()
            .Which.Message.Should().Be("Environment must implement IHostEnvironment");
    }
    #endregion

    #region Test Inputs
    public static IEnumerable<object[]> EnvironmentMatchInput(string? actualEnvironment) {
        var registries = new[] {
            CreateMockRegistry("Development"),
            CreateMockRegistry("production"),
            CreateMockRegistry("development", "Production"),
            CreateMockRegistry()
        };
        var expectedRegistryIndicies = new List<int>();

        for (var i = 0; i < registries.Length; i++) {
            if (actualEnvironment is null) {
                expectedRegistryIndicies.Add(i);
                continue;
            }
            if (registries[i].Object.TargetEnvironments.Contains(actualEnvironment, StringComparer.OrdinalIgnoreCase)) {
                expectedRegistryIndicies.Add(i);
                continue;
            }
            if (registries[i].Object.TargetEnvironments.Count == 0) {
                expectedRegistryIndicies.Add(i);
            }
        }

        return new object[][] {
            new object[] {
                registries,
                actualEnvironment!,
                expectedRegistryIndicies.ToArray()
            }
        };
    }
    #endregion

    #region Test Helpers
    private static IRegistryRunner CreateService(Dependencies? deps = null) {
        deps ??= new();
        var services = new ServiceCollection();
        services.AddSingleton(deps.Activator.Object);
        services.AddSingleton(deps.Applicator.Object);
        services.AddSingleton<IRegistryRunner, RegistryRunner>();

        return services.BuildServiceProvider().GetRequiredService<IRegistryRunner>();
    }

    private static Mock<IRegistryModule> CreateMockRegistry(params string[] targetEnvironments) {
        var mock = new Mock<IRegistryModule>();
        mock.SetupGet(m => m.TargetEnvironments).Returns(targetEnvironments);

        return mock;
    }

    private static RegistryOptions CreateOptions(object? environment = null)
        => new() { Environment = environment };

    private static IServiceCollection CreateServiceCollection() => new ServiceCollection();
    #endregion

    #region Test Classes
    private class Dependencies {
        public Mock<IRegistryActivator> Activator { get; }
        public Mock<IRegistryConfigApplicator> Applicator { get; }

        public Dependencies() {
            Activator = new();
            Applicator = new();

            SetupInstantiateRegistries();
            SetupInitializeFrom(null);
            SetupApplyConfiguration(null);
        }

        public void SetupInstantiateRegistries(Action<RegistryOptions>? callback = null, IEnumerable<IRegistryModule>? returnVal = null) {
            var setup = Activator.Setup(m => m.InstantiateRegistries(It.IsAny<RegistryOptions>()));
            IReturnsThrows<IRegistryActivator, IEnumerable<IRegistryModule>> returnsThrows = setup;
            if (callback is not null) {
                returnsThrows = setup.Callback(callback);
            }
            returnsThrows.Returns(returnVal ?? Enumerable.Empty<IRegistryModule>());
        }

        public void SetupInitializeFrom(Action<RegistryOptions>? callback) {
            var setup = Applicator.Setup(m => m.InitializeFrom(It.IsAny<RegistryOptions>()));
            if (callback is not null) {
                setup.Callback(callback);
            }
        }

        public void SetupApplyConfiguration(Action<IRegistryModule>? callback) {
            var setup = Applicator.Setup(m => m.ApplyRegistryConfiguration(It.IsAny<IRegistryModule>()));
            if (callback is not null) {
                setup.Callback(callback);
            }
        }
    }

    private class TestEnvironment : IHostEnvironment {
        public TestEnvironment(string name = "Production") => EnvironmentName = name;

        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = null!;
        public string ContentRootPath { get; set; } = null!;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
    #endregion
}
