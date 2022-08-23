using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Moq.Language.Flow;
using Xunit;

namespace ServiceRegistryModules.Internal.Tests {
    public partial class RegistryConfigApplicator_Should {
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
