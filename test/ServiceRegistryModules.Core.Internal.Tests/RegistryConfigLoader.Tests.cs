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
                    KeyTransform($"{key}:{registryEntry.Key}:{propEntry.Key}"),
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

        var configEntries = expectedConfig
            .SelectMany(registryEntry => registryEntry.Value
                .Select(propEntry => KeyValuePair.Create(
                    $"{key}:{registryEntry.Key}:{propEntry.Key}" + (propEntry.Key == "prop2" ? "" : $":{nameof(propEntry.Value.Value)}"),
                    propEntry.Value.Value?.ToString()
                )))
            .ToList();

        // Add the error suppression entries
        configEntries.AddRange(expectedConfig.SelectMany(registryEntry => registryEntry.Value
            .Where(propEntry => propEntry.Key != "prop2")
            .Select(propEntry => KeyValuePair.Create(
                $"{key}:{registryEntry.Key}:{propEntry.Key}:{nameof(propEntry.Value.SuppressErrors)}",
                propEntry.Value.SuppressErrors.ToString())))!);

        var options = CreateOptions(builder => builder.AddInMemoryCollection(configEntries), sectionKey: key);
        var service = CreateService();

        // Act
        var actualConfig = service.LoadFrom(options);

        // Assert
        actualConfig.Should().BeEquivalentTo(expectedConfig);
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
                    $"{key}:{registryEntry.Key}:{propEntry.Key}",
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
            KeyValuePair.Create($"{key}:SomeRegistry", "no-properties")
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
    private static RegistryOptions CreateOptions(Action<ConfigurationBuilder>? config = null, string? sectionKey = ServiceRegistryModulesDefaults.CONFIGURATION_KEY, bool nullConfig = false) {
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
    private static RegistryPropertyConfig CreatePropCfg(object? value = null, bool suppressErrors = false)
        => new() { Value = value, SuppressErrors = suppressErrors };
    #endregion
}
