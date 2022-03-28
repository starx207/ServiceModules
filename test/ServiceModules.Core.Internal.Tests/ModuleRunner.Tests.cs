using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Moq;
using Moq.Language.Flow;
using Xunit;

namespace ServiceModules.Internal.Tests;
public class ModuleRunner_Should {
    #region Tests
    [Fact]
    public void UseTheGivenOptions_ToInstantiateTheModules() {
        // Arrange
        var options = CreateOptions();
        var mock = new Dependencies();
        var service = CreateService(mock);

        ModuleOptions? actualOptions = null;
        mock.SetupInstantiateModules(callback: opt => actualOptions = opt);

        // Act
        service.ApplyRegistries(CreateServiceCollection(), options);

        // Assert
        actualOptions.Should().BeSameAs(options);
    }

    [Fact]
    public void PassTheGivenServices_ToEachModule() {
        // Arrange
        var expectedServices = CreateServiceCollection();

        var modules = new[] {
            CreateMockModule(),
            CreateMockModule(),
            CreateMockModule()
        };

        var mock = new Dependencies();
        var service = CreateService(mock);

        mock.SetupInstantiateModules(returnVal: modules.Select(m => m.Object));

        // Act
        service.ApplyRegistries(expectedServices, CreateOptions());

        // Assert
        foreach (var module in modules) {
            module.Verify(m => m.ConfigureServices(expectedServices), Times.Once());
        }
    }

    [Fact]
    public void ConfigureEachModule_BeforeApplyingItsServiceConfiguration() {
        // Arrange
        var module = CreateMockModule();
        var mock = new Dependencies();
        var service = CreateService(mock);

        module.Setup(m => m.ConfigureServices(It.IsAny<IServiceCollection>()))
            .Callback<IServiceCollection>(_ => {
                mock.Applicator.Verify(m => m.ApplyModuleConfiguration(module.Object),
                    Times.Once(), "Configuration not applied");
            }).Verifiable("Services not configured");

        mock.SetupInstantiateModules(returnVal: new[] { module.Object });

        // Act
        service.ApplyRegistries(CreateServiceCollection(), CreateOptions());

        // Assert
        module.Verify();
    }

    [Fact]
    public void UseTheGivenOptions_ToInitializeTheApplicatorOnce_BeforeApplyingToAnyModules() {
        // Arrange
        var modules = new[] {
            CreateMockModule(),
            CreateMockModule(),
            CreateMockModule()
        };
        var options = CreateOptions();
        var mock = new Dependencies();
        var service = CreateService(mock);

        mock.SetupInstantiateModules(returnVal: modules.Select(m => m.Object));

        ModuleOptions? actualOptions = null;
        mock.SetupInitializeFrom(opt => {
            actualOptions = opt;
            foreach (var module in modules) {
                module.Verify(m => m.ConfigureServices(It.IsAny<IServiceCollection>()),
                    Times.Never(), "Config not initialized before configuring services");
            }
        });

        // Act
        service.ApplyRegistries(CreateServiceCollection(), options);

        // Assert
        actualOptions.Should().BeSameAs(options);
        mock.Applicator.Verify(m => m.InitializeFrom(It.IsAny<ModuleOptions>()), Times.Once());
    }

    [Theory,
        MemberData(nameof(EnvironmentMatchInput), "development"),
        MemberData(nameof(EnvironmentMatchInput), "production"),
        MemberData(nameof(EnvironmentMatchInput), null),
        MemberData(nameof(EnvironmentMatchInput), "staging")]
    public void NotApplyConfigurations_OrConfigureServices_ForModulesThatDoNotMatchTheCurrentEnvironment(Mock<IRegistryModule>[] modules, string? environment, int[] expectedIndicies) {
        // Arrange
        var hostEnv = environment == null ? null : new TestEnvironment(environment);
        var options = CreateOptions(environment: hostEnv);
        var mock = new Dependencies();
        var service = CreateService(mock);

        mock.SetupInstantiateModules(returnVal: modules.Select(m => m.Object));

        // Act
        service.ApplyRegistries(CreateServiceCollection(), options);

        // Assert
        for (var i = 0; i < modules.Length; i++) {
            var times = expectedIndicies.Contains(i) ? Times.Once() : Times.Never();
            modules[i].Verify(m => m.ConfigureServices(It.IsAny<IServiceCollection>()),
                times, $"Module {i} ConfigureServices");
            mock.Applicator.Verify(m => m.ApplyModuleConfiguration(modules[i].Object),
                times, $"Module {i} ApplyConfiguration");
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
        action.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Be("Environment must implement IHostEnvironment");
    }
    #endregion

    #region Test Inputs
    private static IEnumerable<object[]> EnvironmentMatchInput(string? actualEnvironment) {
        var modules = new[] {
            CreateMockModule("Development"),
            CreateMockModule("production"),
            CreateMockModule("development", "Production"),
            CreateMockModule()
        };
        var expectedModuleIndicies = new List<int>();

        for (var i = 0; i < modules.Length; i++) {
            if (actualEnvironment is null) {
                expectedModuleIndicies.Add(i);
                continue;
            }
            if (modules[i].Object.TargetEnvironments.Contains(actualEnvironment, StringComparer.OrdinalIgnoreCase)) {
                expectedModuleIndicies.Add(i);
                continue;
            }
            if (modules[i].Object.TargetEnvironments.Count == 0) {
                expectedModuleIndicies.Add(i);
            }
        }

        return new object[][] {
            new object[] {
                modules,
                actualEnvironment!,
                expectedModuleIndicies.ToArray()
            }
        };
    }
    #endregion

    #region Test Helpers
    private static IModuleRunner CreateService(Dependencies? deps = null) {
        deps ??= new();
        var services = new ServiceCollection();
        services.AddSingleton(deps.Activator.Object);
        services.AddSingleton(deps.Applicator.Object);
        services.AddSingleton<IModuleRunner, ModuleRunner>();

        return services.BuildServiceProvider().GetRequiredService<IModuleRunner>();
    }

    private static Mock<IRegistryModule> CreateMockModule(params string[] targetEnvironments) {
        var mock = new Mock<IRegistryModule>();
        mock.SetupGet(m => m.TargetEnvironments).Returns(targetEnvironments);

        return mock;
    }

    private static ModuleOptions CreateOptions(object? environment = null)
        => new() { Environment = environment };

    private static IServiceCollection CreateServiceCollection() => new ServiceCollection();
    #endregion

    #region Test Classes
    private class Dependencies {
        public Mock<IModuleActivator> Activator { get; }
        public Mock<IModuleConfigApplicator> Applicator { get; }

        public Dependencies() {
            Activator = new();
            Applicator = new();

            SetupInstantiateModules();
            SetupInitializeFrom(null);
            SetupApplyConfiguration(null);
        }

        public void SetupInstantiateModules(Action<ModuleOptions>? callback = null, IEnumerable<IRegistryModule>? returnVal = null) {
            var setup = Activator.Setup(m => m.InstantiateModules(It.IsAny<ModuleOptions>()));
            IReturnsThrows<IModuleActivator, IEnumerable<IRegistryModule>> returnsThrows = setup;
            if (callback is not null) {
                returnsThrows = setup.Callback(callback);
            }
            returnsThrows.Returns(returnVal ?? Enumerable.Empty<IRegistryModule>());
        }

        public void SetupInitializeFrom(Action<ModuleOptions>? callback) {
            var setup = Applicator.Setup(m => m.InitializeFrom(It.IsAny<ModuleOptions>()));
            if (callback is not null) {
                setup.Callback(callback);
            }
        }

        public void SetupApplyConfiguration(Action<IRegistryModule>? callback) {
            var setup = Applicator.Setup(m => m.ApplyModuleConfiguration(It.IsAny<IRegistryModule>()));
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
