using System;
using System.Linq;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Moq;
using ServiceRegistryModules.Exceptions;
using ServiceRegistryModules.Internal;
using Xunit;

namespace ServiceRegistryModules.Tests;
public class ServiceCollectionExtensions_Should {
    #region Tests
    [Fact]
    public void UseCorrectDefaultOptions_WhenNoConfigurationsProvided() {
        // Arrange
        var mock = new Dependencies();
        var services = CreateServices(mock);
        var expectedOptions = CreateOptions();
        expectedOptions.RegistryConfigSectionKey = ServiceRegistryModulesDefaults.CONFIGURATION_KEY;
        expectedOptions.PublicOnly = false;
        expectedOptions.RegistryTypes.Add(typeof(TestRegistry1));

        // Act
        services.ApplyRegistries();

        // Assert
        mock.OptionsApplied.Should().BeEquivalentTo(expectedOptions);
    }

    [Fact]
    public void AllowForSettingTheRegistryConfigSectionKey_InTheRegistryOptions() {
        // Arrange
        var expectedKey = "some_other_config_key";
        var mock = new Dependencies();
        var services = CreateServices(mock);

        // Act
        services.ApplyRegistries(config => config.WithConfigurationsFromSection(expectedKey));

        // Assert
        mock.OptionsApplied?.RegistryConfigSectionKey.Should().Be(expectedKey);
    }

    [Fact]
    public void AllowForRegisteringOnlyPublicRegistries() {
        // Arrange
        var mock = new Dependencies();
        var services = CreateServices(mock);

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
            typeof(TestSamples2.TestRegistry1)
        };
        var mock = new Dependencies();
        var services = CreateServices(mock);

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
        var services = CreateServices(mock);

        // Act
        services.ApplyRegistries(config => config.UsingProviders(providers));

        // Assert
        using (new AssertionScope()) {
            mock.OptionsApplied?.Providers
                .Should().Contain(providers);
            mock.OptionsApplied?.AllowedRegistryCtorArgTypes
                .Should().BeEquivalentTo(providerTypes);
        }
    }

    [Fact]
    public void NotAddMoreThanOneProvider_OfTheSameType() {
        // Arrange
        var mock = new Dependencies();
        var services = CreateServices(mock);

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
        var services = CreateServices(mock);

        // Act
        services.ApplyRegistries(config => config.OfTypes(typeof(TestRegistry1)));

        // Assert
        mock.OptionsApplied?.RegistryTypes.Should().Equal(typeof(TestRegistry1));
    }

    [Fact]
    public void ThrowAnException_IfAddingAnExplicitRegistryType_ThatDoesNotImplementIRegistryModule() {
        // Arrange
        var services = CreateServices();

        // Act
        var action = () => services.ApplyRegistries(config
            => config.OfTypes(typeof(TestService1), typeof(Dependencies)));

        // Assert
        action.Should().Throw<RegistryConfigurationException>()
            .Which.Message.Should().Be("The following registry types do not implement IRegistryModule: TestService1, Dependencies");
    }

    [Theory,
        InlineData(true),
        InlineData(false)]
    public void AllowForSettingTheEnvironment_InTheRegistryOptions(bool setThroughProviderExtension) {
        // Arrange
        var mock = new Dependencies();
        var services = CreateServices(mock);
        var expectedEnv = new TestEnvironment();

        // Act
        services.ApplyRegistries(config => {
            if (setThroughProviderExtension) {
                config.UsingProviders(expectedEnv);
            } else {
                config.UsingEnvironment(expectedEnv);
            }
        });

        // Assert
        using (new AssertionScope()) {
            mock.OptionsApplied?.Environment.Should().BeSameAs(expectedEnv);
            mock.OptionsApplied?.Providers.Should().Contain(expectedEnv);
            mock.OptionsApplied?.AllowedRegistryCtorArgTypes.Should()
                .Contain(typeof(IHostEnvironment))
                .And.Contain(expectedEnv.GetType());
        }
    }

    [Fact]
    public void ReplaceTheExistingEnvironment_IfConfiguredMoreThanOnce() {
        // Arrange
        var mock = new Dependencies();
        var services = CreateServices(mock);
        var firstEnv = new TestEnvironment();
        var expectedEnv = new TestOtherEnvironment();

        // Act
        services.ApplyRegistries(config
            => config.UsingEnvironment(firstEnv)
                .UsingEnvironment(expectedEnv));

        // Assert
        using (new AssertionScope()) {
            mock.OptionsApplied?.Environment.Should().BeSameAs(expectedEnv);
            mock.OptionsApplied?.Providers.Should().Contain(expectedEnv)
                .And.NotContain(firstEnv);
            mock.OptionsApplied?.AllowedRegistryCtorArgTypes.Should()
                .Contain(typeof(IHostEnvironment))
                .And.Contain(expectedEnv.GetType())
                .And.NotContain(firstEnv.GetType());
        }
    }

    [Fact]
    public void ThrowAnException_WhenSettingTheEnvironment_WithSomethingThatDoesNotImplement_IHostEnvironment() {
        // Arrange
        var mock = new Dependencies();
        var services = CreateServices(mock);

        // Act
        var action = () => services.ApplyRegistries(config => config.UsingEnvironment(new TestService1()));

        // Assert
        action.Should().Throw<RegistryConfigurationException>()
            .Which.Message.Should().Be("Environment object must implement IHostEnvironment");
    }

    [Theory,
        InlineData(true),
        InlineData(false)]
    public void AllowForSettingTheConfiguration_InTheRegistryOptions(bool setThroughProviderExtension) {
        // Arrange
        var mock = new Dependencies();
        var services = CreateServices(mock);
        var expectedCfg = new ConfigurationBuilder().Build();

        // Act
        services.ApplyRegistries(config => {
            if (setThroughProviderExtension) {
                config.UsingProviders(expectedCfg);
            } else {
                config.UsingConfiguration(expectedCfg);
            }
        });

        // Assert
        using (new AssertionScope()) {
            mock.OptionsApplied?.Configuration.Should().BeSameAs(expectedCfg);
            mock.OptionsApplied?.Providers.Should().Contain(expectedCfg);
            mock.OptionsApplied?.AllowedRegistryCtorArgTypes.Should().Contain(typeof(IConfiguration));
        }
    }

    [Fact]
    public void ReplaceTheExistingConfiguration_IfConfiguredMoreThanOnce() {
        // Arrange
        var mock = new Dependencies();
        var services = CreateServices(mock);
        var firstCfg = new ConfigurationBuilder().Build();
        var expectedCfg = new ConfigurationBuilder().Build();

        // Act
        services.ApplyRegistries(config
            => config.UsingConfiguration(firstCfg)
                .UsingConfiguration(expectedCfg));

        // Assert
        using (new AssertionScope()) {
            mock.OptionsApplied?.Configuration.Should().BeSameAs(expectedCfg);
            mock.OptionsApplied?.Providers.Should().Contain(expectedCfg)
                .And.NotContain(firstCfg);
        }
    }

    [Fact]
    public void NotCreateOptionsWithRegistryTypes_ThatAlsoHaveAnInstanceProvided() {
        // Arrange
        var registry = new TestSamples1.TestRegistry1();
        var mock = new Dependencies();
        var services = CreateServices(mock);

        // Act
        services.ApplyRegistries(config
            => config.FromAssemblies(registry.GetType().Assembly)
                .From(registry));

        // Assert
        mock.OptionsApplied?.RegistryTypes.Should().NotContain(registry.GetType());
    }
    #endregion

    #region Test Helpers
    private IServiceCollection CreateServices(Dependencies? deps = null) {
        var services = new ServiceCollection();
        InternalServiceProvider.RegistryRunnerTestOverride = deps?.Runner.Object;
        return services;
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

    private class TestEnvironment : IHostEnvironment {
        public string EnvironmentName { get; set; } = null!;
        public string ApplicationName { get; set; } = null!;
        public string ContentRootPath { get; set; } = null!;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private class TestOtherEnvironment : IHostEnvironment {
        public string EnvironmentName { get; set; } = null!;
        public string ApplicationName { get; set; } = null!;
        public string ContentRootPath { get; set; } = null!;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

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
