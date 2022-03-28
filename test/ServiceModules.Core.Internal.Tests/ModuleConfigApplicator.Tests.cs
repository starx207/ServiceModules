using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Moq.Language.Flow;
using Xunit;

namespace ServiceModules.Internal.Tests {
    public class ModuleConfigApplicator_Should {
        #region Tests
        [Fact]
        public void DoNothingToTheModule_WhenNoConfigurationProvided() {
            // Arrange
            var mock = new Dependencies();
            var service = CreateService(mock);
            var module = CreateTestModule1();
            var expectedModule = CreateTestModule1();

            mock.SetupLoadFrom(returnVal: null);

            // Act
            service.InitializeFrom(CreateOptions());
            service.ApplyModuleConfiguration(module);

            // Assert
            module.Should().BeEquivalentTo(expectedModule);
        }

        [Fact]
        public void DoNothingToModule_WhenConfigHasNoEntryThatMatchesTheModule() {
            // Arrange
            var mock = new Dependencies();
            var service = CreateService(mock);
            var module = CreateTestModule1();
            var expectedModule = CreateTestModule1();

            var config = CreateConfig();
            AddConfigKey(config, "TestModule3",
                (nameof(Namespace1.TestModule3.PublicString), "a-new-value"));
            AddConfigKey(config, "*.Namespace2.TestModule",
                (nameof(Namespace2.TestModule.PublicString), "a-new-value"));

            mock.SetupLoadFrom(returnVal: config);

            // Act
            service.InitializeFrom(CreateOptions());
            service.ApplyModuleConfiguration(module);

            // Assert
            module.Should().BeEquivalentTo(expectedModule);
        }

        [Fact]
        public void CorrectlySetProperties_OfDifferentTypes() {
            // Arrange
            var newString = "'sup?";
            var newBool = true;
            var newInt = 432421;
            var module = CreateTestModule1();
            module.PublicBool = !newBool;
            module.PublicInt = newInt + 10000;
            module.PublicString = $"not-{newString}";

            var config = CreateConfig();
            AddConfigKey(config, nameof(Namespace1.TestModule),
                (nameof(module.PublicBool), newBool.ToString().ToLower()),
                (nameof(module.PublicInt), newInt.ToString()),
                (nameof(module.PublicString), newString));

            var mock = new Dependencies();
            var service = CreateService(mock);

            mock.SetupLoadFrom(returnVal: config);

            // Act
            service.InitializeFrom(CreateOptions());
            service.ApplyModuleConfiguration(module);

            // Assert
            using (new AssertionScope()) {
                module.PublicString.Should().Be(newString);
                module.PublicInt.Should().Be(newInt);
                module.PublicBool.Should().Be(newBool);
            }
        }

        [Theory, MemberData(nameof(KeyMatchingInputs))]
        public void ApplyTheCorrectConfiguration_WhenTheModuleMatchesMoreThanOne(int correctIndex, string[] keys) {
            // Arrange
            var correctVal = "correct";
            var wrongVal = "incorrect";
            var module = CreateTestModule1();
            module.PublicString = "starting-val";

            var config = CreateConfig();
            for (var i = 0; i < keys.Length; i++) {
                AddConfigKey(config, keys[i], (nameof(module.PublicString), i == correctIndex ? correctVal : wrongVal));
            }

            var mock = new Dependencies();
            var service = CreateService(mock);

            mock.SetupLoadFrom(returnVal: config);

            // Act
            service.InitializeFrom(CreateOptions());
            service.ApplyModuleConfiguration(module);

            // Assert
            module.PublicString.Should().Be(correctVal);
        }

        [Fact]
        public void ThrowAnException_WhenTryingToSetAPropertyThatDoesNotExist() {
            // Arrange
            var module = CreateTestModule3();
            var config = CreateConfig();
            AddConfigKey(config, module.GetType().Name, ($"NonExistant_{nameof(module.PublicString)}", "oops"));

            var mock = new Dependencies();
            var service = CreateService(mock);

            mock.SetupLoadFrom(returnVal: config);

            // Act
            service.InitializeFrom(CreateOptions());
            var action = () => service.ApplyModuleConfiguration(module);

            // Assert
            action.Should().Throw<InvalidOperationException>()
                .Which.Message.Should()
                .Be($"Configuration failed for the following non-existant {module.GetType().Name} properties: {config.Single().Value.Single().Key}");
        }

        [Fact]
        public void ThrowAnException_WhenTryingToSetANonPublicProperty_WhenOptionsUsePublicOnly() {
            // Arrange
            var module = CreateTestModule1();
            var config = CreateConfig();
            AddConfigKey(config, module.GetType().Name,
                ("InternalString", "internal-oops"),
                ("PrivateString", "private-oops"));

            var options = CreateOptions(publicOnly: true);
            var mock = new Dependencies();
            var service = CreateService(mock);

            mock.SetupLoadFrom(returnVal: config);

            // Act
            service.InitializeFrom(options);
            var action = () => service.ApplyModuleConfiguration(module);

            // Assert
            action.Should().Throw<InvalidOperationException>()
                .Which.Message.Should()
                .Be($"Configuration failed for the following non-existant or non-public {module.GetType().Name} properties: InternalString, PrivateString");
        }

        [Fact]
        public void ThrowAnException_WhenTryingToSetAProperty_WithNoSetter() {
            // Arrange
            var module = CreateTestModule1();
            var config = CreateConfig();
            AddConfigKey(config, module.GetType().Name,
                (nameof(module.StringWithoutSetter), "oops"),
                (nameof(module.StringWithLambdaGetter), "oops"));

            var mock = new Dependencies();
            var service = CreateService(mock);

            mock.SetupLoadFrom(returnVal: config);

            // Act
            service.InitializeFrom(CreateOptions());
            var action = () => service.ApplyModuleConfiguration(module);

            // Assert
            action.Should().Throw<InvalidOperationException>()
                .Which.Message.Should()
                .Be($"Failed to configure {module.GetType().Name} because no setter found for the following properties: " +
                $"{nameof(module.StringWithoutSetter)}, {nameof(module.StringWithLambdaGetter)}");
        }

        [Fact]
        public void ThrowAnException_WhenTryingToSetAProperty_WithNonPublicSetter_WhenOptionsUsePublicOnly() {
            // Arrange
            var module = CreateTestModule1();
            var config = CreateConfig();
            AddConfigKey(config, module.GetType().Name,
                (nameof(module.StringWithInternalSetter), "oops"),
                (nameof(module.StringWithPrivateSetter), "oops"));

            var options = CreateOptions(publicOnly: true);
            var mock = new Dependencies();
            var service = CreateService(mock);

            mock.SetupLoadFrom(returnVal: config);

            // Act
            service.InitializeFrom(options);
            var action = () => service.ApplyModuleConfiguration(module);

            // Assert
            action.Should().Throw<InvalidOperationException>()
                .Which.Message.Should()
                .Be($"Failed to configure {module.GetType().Name} because no public setter found for the following properties: " +
                $"{nameof(module.StringWithInternalSetter)}, {nameof(module.StringWithPrivateSetter)}");
        }

        [Fact]
        public void PassGivenOptions_ToModuleConfigLoader_ForRetreivingConfigurations() {
            // Arrange
            var expectedOptions = CreateOptions();
            var mock = new Dependencies();
            var service = CreateService(mock);

            ModuleOptions? actualOptions = null;
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
                new object[] { 0, new[] { typeof(Namespace1.TestModule).FullName, typeof(Namespace1.TestModule).Name } },
                new object[] { 0, new[] { typeof(Namespace1.TestModule).FullName, typeof(Namespace2.TestModule).FullName } },
                new object[] { 1, new[] { typeof(Namespace2.TestModule).FullName, typeof(Namespace1.TestModule).Name } },
                new object[] { 1, new[] { "ServiceModules.Internal.Tests.*.TestModule", typeof(Namespace1.TestModule).Name } },
                new object[] { 0, new[] { "ServiceModules.Internal.Tests.*.TestModule", "ServiceModules.Internal.Tests.*" } },
                new object[] { 0, new[] { "ServiceModules.Internal.Test*", "ServiceModules.Internal*Test*" } },
                new object[] { 1, new[] { "*ServiceModules.Internal.Test*", "ServiceModules.Internal.Test*" } }
            };
        #endregion

        #region Test Helpers
        private static object?[] GetAllProperties<T>(T module) where T : IRegistryModule
            => module.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Select(prop => prop.GetValue(module, null))
            .ToArray();

        private static IModuleConfigApplicator CreateService(Dependencies? deps = null) {
            deps ??= new();

            var services = new ServiceCollection();
            services.AddSingleton(deps.Loader.Object);
            services.AddSingleton<IModuleConfigApplicator, ModuleConfigApplicator>();

            return services.BuildServiceProvider().GetRequiredService<IModuleConfigApplicator>();
        }
        private static ModuleOptions CreateOptions(bool publicOnly = false) => new() { PublicOnly = publicOnly };

        private static Namespace1.TestModule CreateTestModule1() => new();
        private static Namespace2.TestModule CreateTestModule2() => new();
        private static Namespace1.TestModule3 CreateTestModule3() => new();

        private static Dictionary<string, IReadOnlyDictionary<string, string>> CreateConfig() => new();

        private static void AddConfigKey(Dictionary<string, IReadOnlyDictionary<string, string>> source, string typeName, params (string propName, string propVal)[] props) {
            var entryDict = new Dictionary<string, string>();
            foreach (var (propName, propVal) in props) {
                entryDict[propName] = propVal;
            }
            source[typeName] = entryDict;
        }
        #endregion

        #region Test Classes
        private class Dependencies {
            public Mock<IModuleConfigLoader> Loader { get; }

            public Dependencies() {
                Loader = new();

                SetupLoadFrom();
            }

            public void SetupLoadFrom(Action<ModuleOptions>? callback = null, Dictionary<string, IReadOnlyDictionary<string, string>>? returnVal = null) {
                var setup = Loader.Setup(m => m.LoadFrom(It.IsAny<ModuleOptions>()));
                IReturnsThrows<IModuleConfigLoader, Dictionary<string, IReadOnlyDictionary<string, string>>?> returnsThrows = setup;
                if (callback is not null) {
                    returnsThrows = setup.Callback(callback);
                }
                returnsThrows.Returns(returnVal);
            }
        }
        #endregion
    }
}

namespace ServiceModules.Internal.Tests.Namespace1 {
    public class TestModule : AbstractRegistryModule {
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

    public class TestModule3 : AbstractRegistryModule {
        public string PublicString { get; set; } = string.Empty;
        public override void ConfigureServices(IServiceCollection services) => throw new NotImplementedException();
    }
}

namespace ServiceModules.Internal.Tests.Namespace2 {
    public class TestModule : AbstractRegistryModule {
        public string PublicString { get; set; } = string.Empty;
        public override void ConfigureServices(IServiceCollection services) => throw new NotImplementedException();
    }
}
