using System;
using System.Linq;
using FluentAssertions;
using FluentAssertions.Execution;
using ServiceRegistryModules.Exceptions;
using Xunit;

namespace ServiceRegistryModules.Internal.Tests {
    public partial class RegistryConfigApplicator_Should {
        #region Tests
        [Fact]
        public void CorrectlySetProperties_OfDifferentTypes() {
            // Arrange
            var newString = "'sup?";
            var newBool = true;
            var newInt = 432421;
            var registry = CreateTestRegistry1();
            registry.PublicBool = !newBool;
            registry.PublicInt = newInt + 10000;
            registry.PublicString = $"not-{newString}";

            var config = CreateConfig();
            config.AddPropertyTo(nameof(Namespace1.TestRegistry), nameof(registry.PublicBool), newBool.ToString().ToLower());
            config.AddPropertyTo(nameof(Namespace1.TestRegistry), nameof(registry.PublicInt), newInt.ToString());
            config.AddPropertyTo(nameof(Namespace1.TestRegistry), nameof(registry.PublicString), newString);

            var mock = new Dependencies();
            var service = CreateService(mock);

            mock.SetupLoadFrom(returnVal: config);

            // Act
            service.InitializeFrom(CreateOptions());
            service.ApplyRegistryConfiguration(registry);

            // Assert
            using (new AssertionScope()) {
                registry.PublicString.Should().Be(newString);
                registry.PublicInt.Should().Be(newInt);
                registry.PublicBool.Should().Be(newBool);
            }
        }

        [Fact]
        public void ThrowAnException_WhenTryingToSetAPropertyThatDoesNotExist() {
            // Arrange
            var registry = CreateTestRegistry3();
            var config = CreateConfig();
            config.AddPropertyTo(registry.GetType().Name, $"NonExistant_{nameof(registry.PublicString)}", "oops");

            var mock = new Dependencies();
            var service = CreateService(mock);

            mock.SetupLoadFrom(returnVal: config);

            // Act
            service.InitializeFrom(CreateOptions());
            var action = () => service.ApplyRegistryConfiguration(registry);

            // Assert
            action.Should().Throw<RegistryConfigurationException>()
                .Which.Message.Should()
                .Be($"Configuration failed for the following non-existant {registry.GetType().Name} members: {config.Single().Value.Single().Key}");
        }

        [Fact]
        public void NotThrowAnException_WhenTryingToSetAPropertyThatDoesNotExist_WithErrorSuppressionOn() {
            // Arrange
            var registry = CreateTestRegistry3();
            var config = CreateConfig();
            config.AddPropertyTo(registry.GetType().Name, $"NonExistant_{nameof(registry.PublicString)}", CreatePropConfig(suppressErrs: true));

            var mock = new Dependencies();
            var service = CreateService(mock);

            mock.SetupLoadFrom(returnVal: config);

            // Act
            service.InitializeFrom(CreateOptions());
            var action = () => service.ApplyRegistryConfiguration(registry);

            // Assert
            action.Should().NotThrow();
        }

        [Fact]
        public void ThrowAnException_WhenTryingToSetANonPublicProperty_WhenOptionsUsePublicOnly() {
            // Arrange
            var registry = CreateTestRegistry1();
            var config = CreateConfig();
            config.AddPropertyTo(registry.GetType().Name, "InternalString", "internal-oops");
            config.AddPropertyTo(registry.GetType().Name, "PrivateString", "private-oops");

            var options = CreateOptions(publicOnly: true);
            var mock = new Dependencies();
            var service = CreateService(mock);

            mock.SetupLoadFrom(returnVal: config);

            // Act
            service.InitializeFrom(options);
            var action = () => service.ApplyRegistryConfiguration(registry);

            // Assert
            action.Should().Throw<RegistryConfigurationException>()
                .Which.Message.Should()
                .Be($"Configuration failed for the following non-existant or non-public {registry.GetType().Name} members: InternalString, PrivateString");
        }

        [Fact]
        public void NotThrowAnException_WhenTryingToSetANonPublicProperty_WhenOptionsUsePublicOnly_ButErrorSuppressionOn() {
            // Arrange
            var registry = CreateTestRegistry1();
            var config = CreateConfig();
            config.AddPropertyTo(registry.GetType().Name, "InternalString", CreatePropConfig(suppressErrs: true));
            config.AddPropertyTo(registry.GetType().Name, "PrivateString", CreatePropConfig(suppressErrs: true));

            var options = CreateOptions(publicOnly: true);
            var mock = new Dependencies();
            var service = CreateService(mock);

            mock.SetupLoadFrom(returnVal: config);

            // Act
            service.InitializeFrom(options);
            var action = () => service.ApplyRegistryConfiguration(registry);

            // Assert
            action.Should().NotThrow();
        }

        [Fact]
        public void ThrowAnException_WhenTryingToSetAProperty_WithNoSetter() {
            // Arrange
            var registry = CreateTestRegistry1();
            var config = CreateConfig();
            config.AddPropertyTo(registry.GetType().Name, nameof(registry.StringWithoutSetter), "oops");
            config.AddPropertyTo(registry.GetType().Name, nameof(registry.StringWithLambdaGetter), "oops");

            var mock = new Dependencies();
            var service = CreateService(mock);

            mock.SetupLoadFrom(returnVal: config);

            // Act
            service.InitializeFrom(CreateOptions());
            var action = () => service.ApplyRegistryConfiguration(registry);

            // Assert
            action.Should().Throw<RegistryConfigurationException>()
                .Which.Message.Should()
                .Be($"Failed to configure {registry.GetType().Name} because no setter found for the following properties: " +
                $"{nameof(registry.StringWithoutSetter)}, {nameof(registry.StringWithLambdaGetter)}");
        }

        [Fact]
        public void NotThrowAnException_WhenTryingToSetAProperty_WithNoSetter_ButErrorSuppressionOn() {
            // Arrange
            var registry = CreateTestRegistry1();
            var config = CreateConfig();
            config.AddPropertyTo(registry.GetType().Name, nameof(registry.StringWithoutSetter), CreatePropConfig(suppressErrs: true));
            config.AddPropertyTo(registry.GetType().Name, nameof(registry.StringWithLambdaGetter), CreatePropConfig(suppressErrs: true));

            var mock = new Dependencies();
            var service = CreateService(mock);

            mock.SetupLoadFrom(returnVal: config);

            // Act
            service.InitializeFrom(CreateOptions());
            var action = () => service.ApplyRegistryConfiguration(registry);

            // Assert
            action.Should().NotThrow();
        }

        [Fact]
        public void ThrowAnException_WhenTryingToSetAProperty_WithNonPublicSetter_WhenOptionsUsePublicOnly() {
            // Arrange
            var registry = CreateTestRegistry1();
            var config = CreateConfig();
            config.AddPropertyTo(registry.GetType().Name, nameof(registry.StringWithInternalSetter), "oops");
            config.AddPropertyTo(registry.GetType().Name, nameof(registry.StringWithPrivateSetter), "oops");

            var options = CreateOptions(publicOnly: true);
            var mock = new Dependencies();
            var service = CreateService(mock);

            mock.SetupLoadFrom(returnVal: config);

            // Act
            service.InitializeFrom(options);
            var action = () => service.ApplyRegistryConfiguration(registry);

            // Assert
            action.Should().Throw<RegistryConfigurationException>()
                .Which.Message.Should()
                .Be($"Failed to configure {registry.GetType().Name} because no public setter found for the following properties: " +
                $"{nameof(registry.StringWithInternalSetter)}, {nameof(registry.StringWithPrivateSetter)}");
        }

        [Fact]
        public void NotThrowAnException_WhenTryingToSetAProperty_WithNonPublicSetter_WhenOptionsUsePublicOnly_ButErrorSuppressionOn() {
            // Arrange
            var registry = CreateTestRegistry1();
            var config = CreateConfig();
            config.AddPropertyTo(registry.GetType().Name, nameof(registry.StringWithInternalSetter), CreatePropConfig(suppressErrs: true));
            config.AddPropertyTo(registry.GetType().Name, nameof(registry.StringWithPrivateSetter), CreatePropConfig(suppressErrs: true));

            var options = CreateOptions(publicOnly: true);
            var mock = new Dependencies();
            var service = CreateService(mock);

            mock.SetupLoadFrom(returnVal: config);

            // Act
            service.InitializeFrom(options);
            var action = () => service.ApplyRegistryConfiguration(registry);

            // Assert
            action.Should().NotThrow();
        }

        [Fact]
        public void ThrowAnException_WhenTryingToSetAPropertyWithAnInvalidValue() {
            // Arrange
            var registry = CreateTestRegistry1();
            var config = CreateConfig();
            config.AddPropertyTo(registry.GetType().Name, nameof(registry.PublicInt), "I'm not an integer!");

            var mock = new Dependencies();
            var service = CreateService(mock);

            mock.SetupLoadFrom(returnVal: config);

            // Act
            service.InitializeFrom(CreateOptions());
            var action = () => service.ApplyRegistryConfiguration(registry);

            // Assert
            action.Should().Throw<RegistryConfigurationException>();
        }

        [Fact]
        public void NotThrowAnException_WhenTryingToSetAPropertyWithAnInvalidValue_WithErrorSuppressionOn() {
            // Arrange
            var registry = CreateTestRegistry1();
            var config = CreateConfig();
            config.AddPropertyTo(registry.GetType().Name, nameof(registry.PublicInt), CreatePropConfig(value: "I'm not an integer!", suppressErrs: true));

            var mock = new Dependencies();
            var service = CreateService(mock);

            mock.SetupLoadFrom(returnVal: config);

            // Act
            service.InitializeFrom(CreateOptions());
            var action = () => service.ApplyRegistryConfiguration(registry);

            // Assert
            action.Should().NotThrow();
        }
        #endregion
    }
}
