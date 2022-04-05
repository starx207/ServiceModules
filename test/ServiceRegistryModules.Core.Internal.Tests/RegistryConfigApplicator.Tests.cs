using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Moq.Language.Flow;
using Xunit;

namespace ServiceRegistryModules.Internal.Tests {
    public class RegistryConfigApplicator_Should {
        #region Tests
        [Fact]
        public void DoNothingToTheRegistry_WhenNoConfigurationProvided() {
            // Arrange
            var mock = new Dependencies();
            var service = CreateService(mock);
            var registry = CreateTestRegistry1();
            var expectedRegistry = CreateTestRegistry1();

            mock.SetupLoadFrom(returnVal: null);

            // Act
            service.InitializeFrom(CreateOptions());
            service.ApplyRegistryConfiguration(registry);

            // Assert
            registry.Should().BeEquivalentTo(expectedRegistry);
        }

        [Fact]
        public void DoNothingToRegistry_WhenConfigHasNoEntryThatMatchesTheRegistry() {
            // Arrange
            var mock = new Dependencies();
            var service = CreateService(mock);
            var registry = CreateTestRegistry1();
            var expectedRegistry = CreateTestRegistry1();

            var config = CreateConfig();
            config.AddPropertyTo("TestRegistry3", nameof(Namespace1.TestRegistry3.PublicString), "a-new-value");
            config.AddPropertyTo("*.Namespace2.TestRegistry", nameof(Namespace2.TestRegistry.PublicString), "a-new-value");

            mock.SetupLoadFrom(returnVal: config);

            // Act
            service.InitializeFrom(CreateOptions());
            service.ApplyRegistryConfiguration(registry);

            // Assert
            registry.Should().BeEquivalentTo(expectedRegistry);
        }

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

        [Theory, MemberData(nameof(KeyMatchingInputs))]
        public void ApplyTheCorrectConfiguration_WhenTheRegistryMatchesMoreThanOne(int correctIndex, string[] keys) {
            // Arrange
            var correctVal = "correct";
            var wrongVal = "incorrect";
            var registry = CreateTestRegistry1();
            registry.PublicString = "starting-val";

            var config = CreateConfig();
            for (var i = 0; i < keys.Length; i++) {
                config.AddPropertyTo(keys[i], nameof(registry.PublicString), i == correctIndex ? correctVal : wrongVal);
            }

            var mock = new Dependencies();
            var service = CreateService(mock);

            mock.SetupLoadFrom(returnVal: config);

            // Act
            service.InitializeFrom(CreateOptions());
            service.ApplyRegistryConfiguration(registry);

            // Assert
            registry.PublicString.Should().Be(correctVal);
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
            action.Should().Throw<InvalidOperationException>()
                .Which.Message.Should()
                .Be($"Configuration failed for the following non-existant {registry.GetType().Name} properties: {config.Single().Value.Single().Key}");
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
            action.Should().Throw<InvalidOperationException>()
                .Which.Message.Should()
                .Be($"Configuration failed for the following non-existant or non-public {registry.GetType().Name} properties: InternalString, PrivateString");
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
            action.Should().Throw<InvalidOperationException>()
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
            action.Should().Throw<InvalidOperationException>()
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
        public void PassGivenOptions_ToRegistryConfigLoader_ForRetreivingConfigurations() {
            // Arrange
            var expectedOptions = CreateOptions();
            var mock = new Dependencies();
            var service = CreateService(mock);

            RegistryOptions? actualOptions = null;
            mock.SetupLoadFrom(callback: opt => actualOptions = opt);

            // Act
            service.InitializeFrom(expectedOptions);

            // Assert
            actualOptions.Should().BeSameAs(expectedOptions);
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
            action.Should().Throw<Exception>();
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

        #region Test Inputs
        private static IEnumerable<object[]> KeyMatchingInputs()
            => new[] {
                new object[] { 0, new[] { typeof(Namespace1.TestRegistry).FullName, typeof(Namespace1.TestRegistry).Name } },
                new object[] { 0, new[] { typeof(Namespace1.TestRegistry).FullName, typeof(Namespace2.TestRegistry).FullName } },
                new object[] { 1, new[] { typeof(Namespace2.TestRegistry).FullName, typeof(Namespace1.TestRegistry).Name } },
                new object[] { 1, new[] { "ServiceRegistryModules.Internal.Tests.*.TestRegistry", typeof(Namespace1.TestRegistry).Name } },
                new object[] { 0, new[] { "ServiceRegistryModules.Internal.Tests.*.TestRegistry", "ServiceRegistryModules.Internal.Tests.*" } },
                new object[] { 0, new[] { "ServiceRegistryModules.Internal.Test*", "ServiceRegistryModules.Internal*Test*" } },
                new object[] { 1, new[] { "*ServiceRegistryModules.Internal.Test*", "ServiceRegistryModules.Internal.Test*" } }
            };
        #endregion

        #region Test Helpers
        private static IRegistryConfigApplicator CreateService(Dependencies? deps = null) {
            deps ??= new();

            var services = new ServiceCollection();
            services.AddSingleton(deps.Loader.Object);
            services.AddSingleton<IRegistryConfigApplicator, RegistryConfigApplicator>();

            return services.BuildServiceProvider().GetRequiredService<IRegistryConfigApplicator>();
        }
        private static RegistryOptions CreateOptions(bool publicOnly = false) => new() { PublicOnly = publicOnly };
        private static RegistryPropertyConfig CreatePropConfig(string value = "test", bool suppressErrs = false)
            => new() {
                Value = value,
                SuppressErrors = suppressErrs
            };

        private static Namespace1.TestRegistry CreateTestRegistry1() => new();
        private static Namespace1.TestRegistry3 CreateTestRegistry3() => new();

        private static RegistryConfiguration CreateConfig() => new();

        #endregion

        #region Test Classes
        private class Dependencies {
            public Mock<IRegistryConfigLoader> Loader { get; }

            public Dependencies() {
                Loader = new();

                SetupLoadFrom();
            }

            public void SetupLoadFrom(Action<RegistryOptions>? callback = null, RegistryConfiguration? returnVal = null) {
                var setup = Loader.Setup(m => m.LoadFrom(It.IsAny<RegistryOptions>()));
                IReturnsThrows<IRegistryConfigLoader, RegistryConfiguration?> returnsThrows = setup;
                if (callback is not null) {
                    returnsThrows = setup.Callback(callback);
                }
                returnsThrows.Returns(returnVal);
            }
        }
        #endregion
    }
}

namespace ServiceRegistryModules.Internal.Tests.Namespace1 {
    public class TestRegistry : AbstractRegistryModule {
        public string PublicString { get; set; } = string.Empty;
        public int PublicInt { get; set; }
        public bool PublicBool { get; set; }

        public string StringWithInternalSetter { get; internal set; } = string.Empty;
        public string StringWithPrivateSetter { get; private set; } = string.Empty;
        public string StringWithoutSetter { get; } = string.Empty;
        public string StringWithLambdaGetter => string.Empty;
        internal string InternalString { get; set; } = string.Empty;
        private string PrivateString { get; set; } = string.Empty;

        public override void ConfigureServices(IServiceCollection services) => throw new NotImplementedException();
    }

    public class TestRegistry3 : AbstractRegistryModule {
        public string PublicString { get; set; } = string.Empty;
        public override void ConfigureServices(IServiceCollection services) => throw new NotImplementedException();
    }
}

namespace ServiceRegistryModules.Internal.Tests.Namespace2 {
    public class TestRegistry : AbstractRegistryModule {
        public string PublicString { get; set; } = string.Empty;
        public override void ConfigureServices(IServiceCollection services) => throw new NotImplementedException();
    }
}
