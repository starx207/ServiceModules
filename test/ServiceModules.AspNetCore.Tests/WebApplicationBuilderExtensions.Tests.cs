using System;
using System.Linq;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using ServiceModules.Internal;
using Xunit;

namespace ServiceModules.AspNetCore.Tests;
public class WebApplicationBuilderExtensions_Should {
    #region Tests
    [Fact]
    public void UseCorrectDefaultOptions_WhenNoConfigurationsProvided() {
        // Arrange
        var mock = new Dependencies();
        var services = CreateBuilder(mock);
        var expectedOptions = CreateOptions();
        expectedOptions.ModuleConfigSectionKey = "registry_modules";
        expectedOptions.PublicOnly = false;
        expectedOptions.ModuleTypes.Add(typeof(TestModule1));
        expectedOptions.Providers.AddRange(new object[] {
            services.Environment,
            services.Configuration
        });
        expectedOptions.AllowedModuleArgTypes.AddRange(new[] {
            typeof(IHostEnvironment),
            services.Environment.GetType(),
            typeof(IConfiguration),
            services.Configuration.GetType()
        });
        expectedOptions.Configuration = services.Configuration;
        expectedOptions.Environment = services.Environment;

        // Act
        services.ApplyModules();

        // Assert
        mock.OptionsApplied.Should().BeEquivalentTo(expectedOptions);
    }

    [Fact]
    public void PassTheWebAppBuildersServices_ToTheActivatedModules() {
        // Arrange
        var mock = new Dependencies();
        var builder = CreateBuilder(mock);

        IServiceCollection? services = null;
        mock.SetupApplyRegistries((svc, opt) => services = svc);

        // Act
        builder.ApplyModules();

        // Assert
        services.Should().NotBeNull()
            .And.BeSameAs(builder.Services);
    }

    [Fact]
    public void AllowForSettingTheModuleConfigSectionKey_InTheModuleOptions() {
        // Arrange
        var expectedKey = "some_other_config_key";
        var mock = new Dependencies();
        var services = CreateBuilder(mock);

        // Act
        services.ApplyModules(config => config.UsingModuleConfigurationSection(expectedKey));

        // Assert
        mock.OptionsApplied?.ModuleConfigSectionKey.Should().Be(expectedKey);
    }

    [Fact]
    public void AllowForRegisteringOnlyPublicModules() {
        // Arrange
        var mock = new Dependencies();
        var services = CreateBuilder(mock);

        // Act
        services.ApplyModules(config => config.PublicOnly());

        // Assert
        mock.OptionsApplied?.PublicOnly.Should().BeTrue();
    }

    [Fact]
    public void AddAllModulesTypes_InTheGivenAssemblies_ToTheModuleOptions() {
        // Arrange
        var expectedModules = new[] {
            typeof(TestSamples1.TestRegistry1),
            typeof(TestSamples1.TestRegistry2),
            typeof(TestSamples2.TestRegistry1)
        };
        var mock = new Dependencies();
        var services = CreateBuilder(mock);

        // Act
        services.ApplyModules(config => config.FromAssemblies(
            typeof(TestSamples1.TestRegistry1),
            typeof(TestSamples2.TestRegistry1)
        ));

        // Assert
        mock.OptionsApplied?.ModuleTypes.Should().BeEquivalentTo(expectedModules);
    }

    [Fact]
    public void AddTheGivenProviders_AndTheirTypes_ToTheModuleOptions() {
        // Arrange
        var providers = new object[] {
            "Hello, World!",
            new TestService1(),
            new TestSamples2.TestRegistry1.Service()
        };
        var providerTypes = providers.Select(x => x.GetType()).ToArray();

        var mock = new Dependencies();
        var services = CreateBuilder(mock);

        // Act
        services.ApplyModules(config => config.WithProviders(providers));

        // Assert
        using (new AssertionScope()) {
            mock.OptionsApplied?.Providers
                .Should().Contain(providers);
            mock.OptionsApplied?.AllowedModuleArgTypes
                .Should().Contain(providerTypes);
        }
    }

    [Fact]
    public void NotAddMoreThanOneProvider_OfTheSameType() {
        // Arrange
        var mock = new Dependencies();
        var services = CreateBuilder(mock);

        // Act
        services.ApplyModules(config => config.WithProviders("Hello", "World"));

        // Assert
        mock.OptionsApplied?.Providers.Should()
            .Contain("Hello", "because it was added first")
            .And.NotContain("World", "because there was already a string added");
    }

    [Fact]
    public void AddAnyExplicitlyDefinedModuleTypes_ToTheModuleOptions() {
        // Arrange
        var mock = new Dependencies();
        var services = CreateBuilder(mock);

        // Act
        services.ApplyModules(config => config.UsingModules(typeof(TestModule1)));

        // Assert
        mock.OptionsApplied?.ModuleTypes.Should().Equal(typeof(TestModule1));
    }

    [Fact]
    public void ThrowAnException_IfAddingAnExplicitModuleType_ThatDoesNotImplementIRegistryModule() {
        // Arrange
        var services = CreateBuilder();

        // Act
        var action = () => services.ApplyModules(config
            => config.UsingModules(typeof(TestService1), typeof(Dependencies)));

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Be("The following module types do not implement IRegistryModule: TestService1, Dependencies");
    }

    [Fact]
    public void NotCreateOptionsWithModuleTypes_ThatAlsoHaveAnInstanceProvided() {
        // Arrange
        var module = new TestSamples1.TestRegistry1();
        var mock = new Dependencies();
        var services = CreateBuilder(mock);

        // Act
        services.ApplyModules(config
            => config.FromAssemblies(module.GetType().Assembly)
                .UsingModules(module));

        // Assert
        mock.OptionsApplied?.ModuleTypes.Should().NotContain(module.GetType());
    }
    #endregion

    #region Test Helpers
    private WebApplicationBuilder CreateBuilder(Dependencies? deps = null) {
        var builder = WebApplication.CreateBuilder();
        InternalServiceProvider.ModuleRunnerTestOverride = deps?.Runner.Object;
        return builder;
    }

    private static ModuleOptions CreateOptions() => new();
    #endregion

    #region Test Classes
    private class TestModule1 : AbstractRegistryModule {
        public IServiceProvider? ServiceProvider { get; private set; }
        public override void ConfigureServices(IServiceCollection services)
            => ServiceProvider = services.BuildServiceProvider();
    }

    private class TestService1 { }

    private class Dependencies {
        public Mock<IModuleRunner> Runner { get; }
        public ModuleOptions? OptionsApplied { get; private set; }

        public Dependencies() {
            Runner = new();

            SetupApplyRegistries(null);
        }

        public void SetupApplyRegistries(Action<IServiceCollection, ModuleOptions>? callback)
            => Runner.Setup(m => m.ApplyRegistries(It.IsAny<IServiceCollection>(), It.IsAny<ModuleOptions>()))
                .Callback<IServiceCollection, ModuleOptions>((svc, options) => {
                    OptionsApplied = options;
                    callback?.Invoke(svc, options);
                });
    }
    #endregion
}
