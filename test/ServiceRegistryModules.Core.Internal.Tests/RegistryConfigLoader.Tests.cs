using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Extensions.Configuration;
using ServiceRegistryModules.Exceptions;
using Xunit;

namespace ServiceRegistryModules.Internal.Tests;
public class RegistryConfigLoader_Should {
    #region Tests
    [Fact]
    public void ReturnNull_WhenNoConfigurationDefined() {
        // Arrange
        var options = CreateOptions(nullConfig: true);
        var service = CreateService();

        // Act
        var config = service.LoadFrom(options);

        // Assert
        config.Should().BeNull();
    }

    [Theory,
        InlineData(null),
        InlineData(""),
        InlineData(" ")]
    public void ThrowAnException_WhenTheConfigurationKey_IsNotDefined(string? invalidKey) {
        // Arrange
        var options = CreateOptions(sectionKey: invalidKey);
        var service = CreateService();

        // Act
        var action = () => service.LoadFrom(options);

        // Assert
        using (new AssertionScope()) {
            var ex = action.Should().Throw<RegistryConfigurationException>();
            ex.Which.Message.Should().StartWith($"'{nameof(options.RegistryConfigSectionKey)}' cannot be null or whitespace.");
        }
    }

    [Theory, InlineData(true), InlineData(false)]
    public void CreateTheCorrectEntries_ForTheConfigurationSection(bool changeCaseBeforeFinalCheck) {
        // Arrange
        var key = "my_registry_config";
        var expectedConfig = new RegistryConfiguration();
        expectedConfig.AddPropertyTo("registry1", "prop1", "val1");
        expectedConfig.AddPropertyTo("registry1", "prop2", "val2");
        expectedConfig.AddPropertyTo("registry2", "prop1", "val1");
        expectedConfig.AddPropertyTo("registry3", "prop1", "val1");
        expectedConfig.AddPropertyTo("registry3", "prop2", "val2");
        expectedConfig.AddPropertyTo("registry3", "prop3", "val3");

        string KeyTransform(string input) => changeCaseBeforeFinalCheck ? input.ToUpper() : input;

        var configEntries = expectedConfig
            .SelectMany(registryEntry => registryEntry.Value
                .Select(propEntry => KeyValuePair.Create(
                    KeyTransform($"{key}:{ServiceRegistryModulesDefaults.CONFIGURATION_KEY}:{registryEntry.Key}:{propEntry.Key}"),
                    propEntry.Value.Value?.ToString()
                )))
            .ToArray();

        var options = CreateOptions(builder => builder.AddInMemoryCollection(configEntries), sectionKey: key);
        var service = CreateService();

        // Act
        var actualConfig = service.LoadFrom(options);
        // TODO: Is there a way to tell fluentvalidation to ignore case so I don't have to do this?
        var configLowered = new RegistryConfiguration();
        foreach (var config in actualConfig!) {
            foreach (var prop in config.Value) {
                configLowered.AddPropertyTo(config.Key.ToLower(), prop.Key.ToLower(), prop.Value.Value?.ToString()!);
            }
        }

        // Assert
        configLowered.Should().BeEquivalentTo(expectedConfig);
    }

    [Fact]
    public void CreateTheConfiguration_UsingAMixOfFull_AndSimpleConfig() {
        // Arrange
        var key = "my_registry_config";
        var expectedConfig = new RegistryConfiguration();
        expectedConfig.AddPropertyTo("registry1", "prop1", CreatePropCfg(value: "val1"));
        expectedConfig.AddPropertyTo("registry1", "prop2", "val2");
        expectedConfig.AddPropertyTo("registry1", "prop3", CreatePropCfg(value: "val2", suppressErrors: true));
        expectedConfig.AddPropertyTo("registry1", "prop4", CreatePropCfg(value: "val2"));

        var configEntries = expectedConfig
            .SelectMany(registryEntry => registryEntry.Value
                .Select(propEntry => KeyValuePair.Create(
                    $"{key}:{ServiceRegistryModulesDefaults.CONFIGURATION_KEY}:{registryEntry.Key}:{propEntry.Key}" + (propEntry.Key == "prop2" ? "" : $":{nameof(propEntry.Value.Value)}"),
                    propEntry.Value.Value?.ToString()
                )))
            .ToList();

        // Add the error suppression entries
        configEntries.AddRange(expectedConfig.SelectMany(registryEntry => registryEntry.Value
            .Where(propEntry => propEntry.Key == "prop3" || propEntry.Key == "prop4")
            .Select(propEntry => KeyValuePair.Create(
                $"{key}:{ServiceRegistryModulesDefaults.CONFIGURATION_KEY}:{registryEntry.Key}:{propEntry.Key}:{nameof(propEntry.Value.SuppressErrors)}",
                propEntry.Value.SuppressErrors.ToString())))!);

        var options = CreateOptions(builder => builder.AddInMemoryCollection(configEntries), sectionKey: key);
        var service = CreateService();

        // Act
        var actualConfig = service.LoadFrom(options);

        // Assert
        actualConfig.Should().BeEquivalentTo(expectedConfig);
    }

    [Fact]
    public void CreateTheConfiguration_ForAnEventDelegate() {
        // Arrange
        var key = "my_registry_config";
        var expectedConfig = new RegistryConfiguration();
        expectedConfig.AddPropertyTo("registry1", "prop1", CreatePropCfg(value: "val1", type: ConfigurationType.Event));

        var configEntries = expectedConfig
            .SelectMany(registryEntry => registryEntry.Value
                .SelectMany(propEntry => new[] {
                    KeyValuePair.Create(
                        $"{key}:{ServiceRegistryModulesDefaults.CONFIGURATION_KEY}:{registryEntry.Key}:{propEntry.Key}:{nameof(propEntry.Value.Value)}",
                        propEntry.Value.Value?.ToString()
                    ),
                    KeyValuePair.Create(
                        $"{key}:{ServiceRegistryModulesDefaults.CONFIGURATION_KEY}:{registryEntry.Key}:{propEntry.Key}:{nameof(propEntry.Value.Type)}",
                        propEntry.Value.Type.ToString()?.ToLower()
                    )
                }))
            .ToList();

        var options = CreateOptions(builder => builder.AddInMemoryCollection(configEntries), sectionKey: key);
        var service = CreateService();

        // Act
        var actualConfig = service.LoadFrom(options);

        // Assert
        actualConfig.Should().BeEquivalentTo(expectedConfig);
    }

    [Fact]
    public void CreateTheConfiguration_UsingAnotherConfigKey_ToSetTheValue() {
        // Arrange
        var key = "my_registry_config";
        var expectedValue = "some-value-in-another-key";
        var otherKey = "some:other:key";

        var configEntries = new Dictionary<string, string> {
            { $"{key}:{ServiceRegistryModulesDefaults.CONFIGURATION_KEY}:registry1:prop1:value", otherKey },
            { $"{key}:{ServiceRegistryModulesDefaults.CONFIGURATION_KEY}:registry1:prop1:type", ConfigurationType.Config.ToString().ToLower() },
            { otherKey, expectedValue }
        };

        var expectedConfig = new RegistryConfiguration();
        expectedConfig.AddPropertyTo("registry1", "prop1", CreatePropCfg(value: expectedValue, type: ConfigurationType.Auto));

        var options = CreateOptions(builder => builder.AddInMemoryCollection(configEntries), sectionKey: key);
        var service = CreateService();

        // Act
        var actualConfig = service.LoadFrom(options);

        // Assert
        actualConfig.Should().BeEquivalentTo(expectedConfig);
    }

    [Fact]
    public void ThrowAnException_WhenAConfigurationWithTypeConfig_CannotBeResolved() {
        // Arrange
        var key = "my_registry_config";
        var otherKey = "some:missing:key";

        var configEntries = new Dictionary<string, string> {
            { $"{key}:{ServiceRegistryModulesDefaults.CONFIGURATION_KEY}:registry1:prop1:value", otherKey },
            { $"{key}:{ServiceRegistryModulesDefaults.CONFIGURATION_KEY}:registry1:prop1:type", ConfigurationType.Config.ToString().ToLower() },
            { otherKey.Replace(":missing:", ":not_missing:"), "some-value" }
        };

        var options = CreateOptions(builder => builder.AddInMemoryCollection(configEntries), sectionKey: key);
        var service = CreateService();

        // Act
        var action = () => service.LoadFrom(options);

        // Assert
        action.Should().Throw<RegistryConfigurationException>().Which
            .Message.Should().Be($"Unable to resolve configuration key for '{otherKey}'");
    }

    [Fact]
    public void NotThrowAnExceptionOrAddTheConfiguration_WhenAConfigurationWithTypeConfig_CannotBeResolved_ButErrorSuppressionIsOn() {
        // Arrange
        var key = "my_registry_config";
        var otherKey = "some:missing:key";

        var configEntries = new Dictionary<string, string> {
            { $"{key}:{ServiceRegistryModulesDefaults.CONFIGURATION_KEY}:registry1:prop1:value", otherKey },
            { $"{key}:{ServiceRegistryModulesDefaults.CONFIGURATION_KEY}:registry1:prop1:type", ConfigurationType.Config.ToString().ToLower() },
            { $"{key}:{ServiceRegistryModulesDefaults.CONFIGURATION_KEY}:registry1:prop1:suppresserrors", "true" },
            { otherKey.Replace(":missing:", ":not_missing:"), "some-value" }
        };

        var options = CreateOptions(builder => builder.AddInMemoryCollection(configEntries), sectionKey: key);
        var service = CreateService();

        // Act
        RegistryConfiguration? actualConfig = null;
        var action = () => actualConfig = service.LoadFrom(options);

        // Assert
        action.Should().NotThrow();
        actualConfig.Should().NotBeNull().And.HaveCount(0);
    }

    [Fact]
    public void NotReturnKeys_FromTheWrongConfigSection() {
        // Arrange
        var key = "my_registry_config";
        var unexpectedConfig = new RegistryConfiguration();
        unexpectedConfig.AddPropertyTo("registry1", "prop1", "val1");
        unexpectedConfig.AddPropertyTo("registry1", "prop2", "val2");
        unexpectedConfig.AddPropertyTo("registry2", "prop1", "val1");
        unexpectedConfig.AddPropertyTo("registry3", "prop1", "val1");
        unexpectedConfig.AddPropertyTo("registry3", "prop2", "val2");
        unexpectedConfig.AddPropertyTo("registry3", "prop3", "val3");

        var configEntries = unexpectedConfig
            .SelectMany(registryEntry => registryEntry.Value
                .Select(propEntry => KeyValuePair.Create(
                    $"{key}:{ServiceRegistryModulesDefaults.CONFIGURATION_KEY}:{registryEntry.Key}:{propEntry.Key}",
                    propEntry.Value.Value?.ToString()
                )))
            .ToArray();

        var options = CreateOptions(builder => builder.AddInMemoryCollection(configEntries), sectionKey: $"not_{key}");
        var service = CreateService();

        // Act
        var actualConfig = service.LoadFrom(options);

        // Assert
        actualConfig.Should().BeEmpty();
    }

    [Fact]
    public void NotReturnKeys_ThatHaveNoPropertiesDefined() {
        // Arrange
        var key = "registry_config";

        var options = CreateOptions(builder => builder.AddInMemoryCollection(new[] {
            KeyValuePair.Create($"{key}:{ServiceRegistryModulesDefaults.CONFIGURATION_KEY}:SomeRegistry", "no-properties")
        }), sectionKey: key);
        var service = CreateService();

        // Act
        var actualConfig = service.LoadFrom(options);

        // Assert
        actualConfig.Should().BeEmpty();
    }
    #endregion

    #region Test Helpers
    private static IRegistryConfigLoader CreateService() => new RegistryConfigLoader();
    private static RegistryOptions CreateOptions(Action<ConfigurationBuilder>? config = null, string? sectionKey = ServiceRegistryModulesDefaults.REGISTRIES_KEY, bool nullConfig = false) {
        var options = new RegistryOptions() {
            RegistryConfigSectionKey = sectionKey!
        };
        if (!nullConfig) {
            var builder = new ConfigurationBuilder();
            config?.Invoke(builder);
            options.Configuration = builder.Build();
        }
        return options;
    }
    private static RegistryPropertyConfig CreatePropCfg(object? value = null, bool suppressErrors = false, ConfigurationType type = ConfigurationType.Auto)
        => new() { Value = value, SuppressErrors = suppressErrors, Type = type };
    #endregion
}
