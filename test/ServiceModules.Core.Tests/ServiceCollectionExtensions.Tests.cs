using System;
using System.Linq;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Moq;
using ServiceModules.Internal;
using Xunit;

namespace ServiceModules.Tests;
public class ServiceCollectionExtensions_Should {
    #region Tests
    [Fact]
    public void UseCorrectDefaultOptions_WhenNoConfigurationsProvided() {
        // Arrange
        var mock = new Dependencies();
        var services = CreateServices(mock);
        var expectedOptions = CreateOptions();
        expectedOptions.ModuleConfigSectionKey = "registry_modules";
        expectedOptions.PublicOnly = false;

        // Act
        services.ApplyModules();

        // Assert
        mock.OptionsApplied.Should().BeEquivalentTo(expectedOptions);
    }

    [Fact]
    public void AllowForSettingTheModuleConfigSectionKey_InTheModuleOptions() {
        // Arrange
        var expectedKey = "some_other_config_key";
        var mock = new Dependencies();
        var services = CreateServices(mock);

        // Act
        services.ApplyModules(config => config.UsingModuleConfigurationSection(expectedKey));

        // Assert
        mock.OptionsApplied?.ModuleConfigSectionKey.Should().Be(expectedKey);
    }

    [Fact]
    public void AllowForRegisteringOnlyPublicModules() {
        // Arrange
        var mock = new Dependencies();
        var services = CreateServices(mock);

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
        var services = CreateServices(mock);

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
        var services = CreateServices(mock);

        // Act
        services.ApplyModules(config => config.WithProviders(providers));

        // Assert
        using (new AssertionScope()) {
            mock.OptionsApplied?.Providers
                .Should().Contain(providers);
            mock.OptionsApplied?.AllowedModuleArgTypes
                .Should().BeEquivalentTo(providerTypes);
        }
    }

    [Fact]
    public void NotAddMoreThanOneProvider_OfTheSameType() {
        // Arrange
        var mock = new Dependencies();
        var services = CreateServices(mock);

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
        var services = CreateServices(mock);

        // Act
        services.ApplyModules(config => config.UsingModules(typeof(TestModule1)));

        // Assert
        mock.OptionsApplied?.ModuleTypes.Should().Equal(typeof(TestModule1));
    }

    [Fact]
    public void ThrowAnException_IfAddingAnExplicitModuleType_ThatDoesNotImplementIRegistryModule() {
        // Arrange
        var services = CreateServices();

        // Act
        var action = () => services.ApplyModules(config
            => config.UsingModules(typeof(TestService1), typeof(Dependencies)));

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Be("The following module types do not implement IRegistryModule: TestService1, Dependencies");
    }

    [Theory,
        InlineData(true),
        InlineData(false)]
    public void AllowForSettingTheEnvironment_InTheModuleOptions(bool setThroughProviderExtension) {
        // Arrange
        var mock = new Dependencies();
        var services = CreateServices(mock);
        var expectedEnv = new TestEnvironment();

        // Act
        services.ApplyModules(config => {
            if (setThroughProviderExtension) {
                config.WithProviders(expectedEnv);
            } else {
                config.WithEnvironment(expectedEnv);
            }
        });

        // Assert
        using (new AssertionScope()) {
            mock.OptionsApplied?.Environment.Should().BeSameAs(expectedEnv);
            mock.OptionsApplied?.Providers.Should().Contain(expectedEnv);
            mock.OptionsApplied?.AllowedModuleArgTypes.Should()
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
        services.ApplyModules(config
            => config.WithEnvironment(firstEnv)
                .WithEnvironment(expectedEnv));

        // Assert
        using (new AssertionScope()) {
            mock.OptionsApplied?.Environment.Should().BeSameAs(expectedEnv);
            mock.OptionsApplied?.Providers.Should().Contain(expectedEnv)
                .And.NotContain(firstEnv);
            mock.OptionsApplied?.AllowedModuleArgTypes.Should()
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
        var action = () => services.ApplyModules(config => config.WithEnvironment(new TestService1()));

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Be("Environment object must implement IHostEnvironment");
    }

    [Theory,
        InlineData(true),
        InlineData(false)]
    public void AllowForSettingTheConfiguration_InTheModuleOptions(bool setThroughProviderExtension) {
        // Arrange
        var mock = new Dependencies();
        var services = CreateServices(mock);
        var expectedCfg = new ConfigurationBuilder().Build();

        // Act
        services.ApplyModules(config => {
            if (setThroughProviderExtension) {
                config.WithProviders(expectedCfg);
            } else {
                config.WithConfiguration(expectedCfg);
            }
        });

        // Assert
        using (new AssertionScope()) {
            mock.OptionsApplied?.Configuration.Should().BeSameAs(expectedCfg);
            mock.OptionsApplied?.Providers.Should().Contain(expectedCfg);
            mock.OptionsApplied?.AllowedModuleArgTypes.Should().Contain(typeof(IConfiguration));
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
        services.ApplyModules(config
            => config.WithConfiguration(firstCfg)
                .WithConfiguration(expectedCfg));

        // Assert
        using (new AssertionScope()) {
            mock.OptionsApplied?.Configuration.Should().BeSameAs(expectedCfg);
            mock.OptionsApplied?.Providers.Should().Contain(expectedCfg)
                .And.NotContain(firstCfg);
        }
    }

    [Fact(Skip = "When testing, the entry assembly is 'testhost' which doesn't have any modules defined. Not sure how to test this (if it is even possible)")]
    public void GetModulesFromEntryAssembly_IfNoneOtherwiseConfigured() {
        // Arrange

        // Act

        // Assert
        //Assert.True(false, "Not Implemented");
    }

    [Fact]
    public void NotCreateOptionsWithModuleTypes_ThatAlsoHaveAnInstanceProvided() {
        // Arrange
        var module = new TestSamples1.TestRegistry1();
        var mock = new Dependencies();
        var services = CreateServices(mock);

        // Act
        services.ApplyModules(config
            => config.FromAssemblies(module.GetType().Assembly)
                .UsingModules(module));

        // Assert
        mock.OptionsApplied?.ModuleTypes.Should().NotContain(module.GetType());
    }
    #endregion

    #region Test Helpers
    private IServiceCollection CreateServices(Dependencies? deps = null) {
        var services = new ServiceCollection();
        InternalServiceProvider.ModuleRunnerTestOverride = deps?.Runner.Object;
        return services;
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
