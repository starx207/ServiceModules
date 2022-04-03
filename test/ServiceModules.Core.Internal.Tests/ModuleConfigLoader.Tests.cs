using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ServiceModules.Internal.Tests;
public class ModuleConfigLoader_Should {
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
            var ex = action.Should().Throw<ArgumentException>();
            ex.Which.ParamName.Should().Be("options");
            ex.Which.Message.Should().StartWith($"'{nameof(options.ModuleConfigSectionKey)}' cannot be null or whitespace.");
        }
    }

    [Theory, InlineData(true)]//, InlineData(false)]
    public void CreateTheCorrectEntries_ForTheConfigurationSection(bool changeCaseBeforeFinalCheck) {
        // Arrange
        var key = "my_module_config";
        var expectedConfig = new ModuleConfiguration();
        expectedConfig.AddPropertyTo("module1", "prop1", "val1");
        expectedConfig.AddPropertyTo("module1", "prop2", "val2");
        expectedConfig.AddPropertyTo("module2", "prop1", "val1");
        expectedConfig.AddPropertyTo("module3", "prop1", "val1");
        expectedConfig.AddPropertyTo("module3", "prop2", "val2");
        expectedConfig.AddPropertyTo("module3", "prop3", "val3");

        string KeyTransform(string input) => changeCaseBeforeFinalCheck ? input.ToUpper() : input;

        var configEntries = expectedConfig
            .SelectMany(moduleEntry => moduleEntry.Value
                .Select(propEntry => KeyValuePair.Create(
                    KeyTransform($"{key}:{moduleEntry.Key}:{propEntry.Key}"),
                    propEntry.Value
                )))
            .ToArray();

        var options = CreateOptions(builder => builder.AddInMemoryCollection(configEntries), sectionKey: key);
        var service = CreateService();

        // Act
        var actualConfig = service.LoadFrom(options);
        // TODO: Is there a way to tell fluentvalidation to ignore case so I don't have to do this?
        var configLowered = new ModuleConfiguration();
        foreach (var config in actualConfig) {
            foreach (var prop in config.Value) {
                configLowered.AddPropertyTo(config.Key.ToLower(), prop.Key.ToLower(), prop.Value);
            }
        }

        // Assert
        configLowered.Should().BeEquivalentTo(expectedConfig);
    }

    [Fact]
    public void NotReturnKeys_FromTheWrongConfigSection() {
        // Arrange
        var key = "my_module_config";
        var unexpectedConfig = new ModuleConfiguration();
        unexpectedConfig.AddPropertyTo("module1", "prop1", "val1");
        unexpectedConfig.AddPropertyTo("module1", "prop2", "val2");
        unexpectedConfig.AddPropertyTo("module2", "prop1", "val1");
        unexpectedConfig.AddPropertyTo("module3", "prop1", "val1");
        unexpectedConfig.AddPropertyTo("module3", "prop2", "val2");
        unexpectedConfig.AddPropertyTo("module3", "prop3", "val3");

        var configEntries = unexpectedConfig
            .SelectMany(moduleEntry => moduleEntry.Value
                .Select(propEntry => KeyValuePair.Create(
                    $"{key}:{moduleEntry.Key}:{propEntry.Key}",
                    propEntry.Value
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
        var key = "module_config";

        var options = CreateOptions(builder => builder.AddInMemoryCollection(new[] {
            KeyValuePair.Create($"{key}:SomeModule", "no-properties")
        }), sectionKey: key);
        var service = CreateService();

        // Act
        var actualConfig = service.LoadFrom(options);

        // Assert
        actualConfig.Should().BeEmpty();
    }
    #endregion

    #region Test Helpers
    private static IModuleConfigLoader CreateService() => new ModuleConfigLoader();
    private static ModuleOptions CreateOptions(Action<ConfigurationBuilder>? config = null, string? sectionKey = "registry_modules", bool nullConfig = false) {
        var options = new ModuleOptions() {
            ModuleConfigSectionKey = sectionKey!
        };
        if (!nullConfig) {
            var builder = new ConfigurationBuilder();
            config?.Invoke(builder);
            options.Configuration = builder.Build();
        }
        return options;
    }
    #endregion
}
