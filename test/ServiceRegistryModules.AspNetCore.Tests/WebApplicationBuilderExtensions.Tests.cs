using System;
using System.Linq;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using ServiceRegistryModules.Exceptions;
using ServiceRegistryModules.Internal;
using Xunit;

namespace ServiceRegistryModules.AspNetCore.Tests;
public class WebApplicationBuilderExtensions_Should {
    #region Tests
    [Fact]
    public void UseCorrectDefaultOptions_WhenNoConfigurationsProvided() {
        // Arrange
        var mock = new Dependencies();
        var services = CreateBuilder(mock);
        var expectedOptions = CreateOptions();
        expectedOptions.RegistryConfigSectionKey = ServiceRegistryModulesDefaults.REGISTRIES_KEY;
        expectedOptions.PublicOnly = false;
        expectedOptions.RegistryTypes.Add(typeof(TestRegistry1));
        expectedOptions.Providers.AddRange(new object[] {
            services.Environment,
            services.Configuration
        });
        expectedOptions.AllowedRegistryCtorArgTypes.AddRange(new[] {
            typeof(IHostEnvironment),
            services.Environment.GetType(),
            typeof(IConfiguration),
            services.Configuration.GetType()
        });
        expectedOptions.Configuration = services.Configuration;
        expectedOptions.Environment = services.Environment;

        // Act
        services.ApplyRegistries();

        // Assert
        mock.OptionsApplied.Should().BeEquivalentTo(expectedOptions);
    }

    [Fact]
    public void PassTheWebAppBuildersServices_ToTheActivatedRegistries() {
        // Arrange
        var mock = new Dependencies();
        var builder = CreateBuilder(mock);

        IServiceCollection? services = null;
        mock.SetupApplyRegistries((svc, opt) => services = svc);

        // Act
        builder.ApplyRegistries();

        // Assert
        services.Should().NotBeNull()
            .And.BeSameAs(builder.Services);
    }

    [Fact]
    public void AllowForSettingTheRegistryConfigSectionKey_InTheRegistryOptions() {
        // Arrange
        var expectedKey = "some_other_config_key";
        var mock = new Dependencies();
        var services = CreateBuilder(mock);

        // Act
        services.ApplyRegistries(config => config.WithConfigurationsFromSection(expectedKey));

        // Assert
        mock.OptionsApplied?.RegistryConfigSectionKey.Should().Be(expectedKey);
    }

    [Fact]
    public void AllowForRegisteringOnlyPublicRegistries() {
        // Arrange
        var mock = new Dependencies();
        var services = CreateBuilder(mock);

        // Act
        services.ApplyRegistries(config => config.PublicOnly());

        // Assert
        mock.OptionsApplied?.PublicOnly.Should().BeTrue();
    }

    [Fact]
    public void AddAllRegistryTypes_InTheGivenAssemblies_ToTheRegistryOptions() {
        // Arrange
        var expectedRegistries = new[] {
            typeof(TestSamples1.TestRegistry1),
            typeof(TestSamples1.TestRegistry2),
            typeof(TestSamples2.TestRegistry1),
            typeof(TestSamples2.TestRegistry2)
        };
        var mock = new Dependencies();
        var services = CreateBuilder(mock);

        // Act
        services.ApplyRegistries(config => config.FromAssembliesOf(
            typeof(TestSamples1.TestRegistry1),
            typeof(TestSamples2.TestRegistry1)
        ));

        // Assert
        mock.OptionsApplied?.RegistryTypes.Should().BeEquivalentTo(expectedRegistries);
    }

    [Fact]
    public void AddTheGivenProviders_AndTheirTypes_ToTheRegistryOptions() {
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
        services.ApplyRegistries(config => config.UsingProviders(providers));

        // Assert
        using (new AssertionScope()) {
            mock.OptionsApplied?.Providers
                .Should().Contain(providers);
            mock.OptionsApplied?.AllowedRegistryCtorArgTypes
                .Should().Contain(providerTypes);
        }
    }

    [Fact]
    public void NotAddMoreThanOneProvider_OfTheSameType() {
        // Arrange
        var mock = new Dependencies();
        var services = CreateBuilder(mock);

        // Act
        services.ApplyRegistries(config => config.UsingProviders("Hello", "World"));

        // Assert
        mock.OptionsApplied?.Providers.Should()
            .Contain("Hello", "because it was added first")
            .And.NotContain("World", "because there was already a string added");
    }

    [Fact]
    public void AddAnyExplicitlyDefinedRegistryTypes_ToTheRegistryOptions() {
        // Arrange
        var mock = new Dependencies();
        var services = CreateBuilder(mock);

        // Act
        services.ApplyRegistries(config => config.OfTypes(typeof(TestRegistry1)));

        // Assert
        mock.OptionsApplied?.RegistryTypes.Should().Equal(typeof(TestRegistry1));
    }

    [Fact]
    public void ThrowAnException_IfAddingAnExplicitRegistryType_ThatDoesNotImplementIRegistryModule() {
        // Arrange
        var services = CreateBuilder();

        // Act
        var action = () => services.ApplyRegistries(config
            => config.OfTypes(typeof(TestService1), typeof(Dependencies)));

        // Assert
        action.Should().Throw<RegistryConfigurationException>()
            .Which.Message.Should().Be("The following registry types do not implement IRegistryModule: TestService1, Dependencies");
    }

    [Fact]
    public void NotCreateOptionsWithRegistryTypes_ThatAlsoHaveAnInstanceProvided() {
        // Arrange
        var registry = new TestSamples1.TestRegistry1();
        var mock = new Dependencies();
        var services = CreateBuilder(mock);

        // Act
        services.ApplyRegistries(config
            => config.FromAssemblies(registry.GetType().Assembly)
                .From(registry));

        // Assert
        mock.OptionsApplied?.RegistryTypes.Should().NotContain(registry.GetType());
    }
    #endregion

    #region Test Helpers
    private WebApplicationBuilder CreateBuilder(Dependencies? deps = null) {
        var builder = WebApplication.CreateBuilder();
        InternalServiceProvider.RegistryRunnerTestOverride = deps?.Runner.Object;
        return builder;
    }

    private static RegistryOptions CreateOptions() => new();
    #endregion

    #region Test Classes
    private class TestRegistry1 : AbstractRegistryModule {
        public IServiceProvider? ServiceProvider { get; private set; }
        public override void ConfigureServices(IServiceCollection services)
            => ServiceProvider = services.BuildServiceProvider();
    }

    private class TestService1 { }

    private class Dependencies {
        public Mock<IRegistryRunner> Runner { get; }
        public RegistryOptions? OptionsApplied { get; private set; }

        public Dependencies() {
            Runner = new();

            SetupApplyRegistries(null);
        }

        public void SetupApplyRegistries(Action<IServiceCollection, RegistryOptions>? callback)
            => Runner.Setup(m => m.ApplyRegistries(It.IsAny<IServiceCollection>(), It.IsAny<RegistryOptions>()))
                .Callback<IServiceCollection, RegistryOptions>((svc, options) => {
                    OptionsApplied = options;
                    callback?.Invoke(svc, options);
                });
    }
    #endregion
}
