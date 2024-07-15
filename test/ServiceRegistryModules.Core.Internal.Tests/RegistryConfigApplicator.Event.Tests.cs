using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ServiceRegistryModules.Exceptions;
using Xunit;
using Xunit.Abstractions;

namespace ServiceRegistryModules.Internal.Tests {
    public partial class RegistryConfigApplicator_Should {
        #region Tests
        [Fact]
        public void CorrectlySetEventDelegate_InRegistry() {
            // Arrange
            var handlerName = $"{typeof(TestSamples2.TestRegistry1).FullName}.TestEventHandler";
            var registry = new TestSamples1.TestRegistry2();

            var config = CreateConfig();
            config.AddPropertyTo(registry.GetType().Name,
                nameof(registry.MyPublicEvent),
                CreatePropConfig(value: handlerName));

            var mock = new Dependencies();
            var service = CreateService(mock);

            mock.SetupLoadFrom(returnVal: config);

            // Act
            service.InitializeFrom(CreateOptions());
            service.ApplyRegistryConfiguration(registry);
            registry.ConfigureServices(new ServiceCollection());


            // Assert
            using (new AssertionScope()) {
                TestSamples2.HandledEvents.HandledEventFor.Should().BeSameAs(registry);
                TestSamples2.HandledEvents.HandledEventArgs.Should().BeSameAs(TestSamples1.TestRegistry2.EventArgs);
            }
        }

        [Fact]
        public void UseTheHintPath_ToLoadAnUnreferencedAssembly_ForEventHandling() {
            // Arrange
            var handlerName = $"UnreferencedTestSamples.TestRegistry1.OnConfigureServicesHandler";
            var registry = new TestSamples2.TestRegistry2();

            var config = CreateConfig();
            config.AddPropertyTo(registry.GetType().Name,
                nameof(registry.OnConfigure),
                CreatePropConfig(value: handlerName,
                    hintPath: "../../../../SampleProjects/UnreferencedTestSamples/bin/Debug/netstandard2.1/UnreferencedTestSamples.dll"));

            var mock = new Dependencies();
            var service = CreateService(mock);

            mock.SetupLoadFrom(returnVal: config);

            // Act
            service.InitializeFrom(CreateOptions());
            service.ApplyRegistryConfiguration(registry);
            var serviceCollection = new ServiceCollection();
            registry.ConfigureServices(serviceCollection);


            // Assert
            serviceCollection.FirstOrDefault(s => s.ServiceType.FullName == "UnreferencedTestSamples.TestRegistry1+Service")
                .Should().NotBeNull();
        }

        // TODO: This test is valuable to keep around, but it doesn't belong here as it tests more than just the config applicator
        [Fact]
        public void ThisTestsHavingTheHostAssm_CallbackToTheTestAssm_ForEventHandling() {
            // Arrange
            var handlerName = $"{typeof(Core.Internal.Tests.Utility).FullName}.{nameof(Core.Internal.Tests.Utility.OnMyPublicEvent)}";
            var configKey = $"{ServiceRegistryModulesDefaults.REGISTRIES_KEY}:{ServiceRegistryModulesDefaults.CONFIGURATION_KEY}";
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>() {
                    { $"{configKey}:{nameof(TestSamples1.TestRegistry2)}:{nameof(TestSamples1.TestRegistry2.MyPublicEvent)}", handlerName }
                });

            // Act
            TestSamples4.TestHost.ConfigureServices(config.Build());

            // Assert
            using (new AssertionScope()) {
                Core.Internal.Tests.Utility.HandledEventFor.Should().BeOfType<TestSamples1.TestRegistry2>();
                Core.Internal.Tests.Utility.HandledEventArgs.Should().BeSameAs(TestSamples1.TestRegistry2.EventArgs);
            }
        }

        [Theory, MemberData(nameof(ExceptionData), "{1} when {0}")]
        public void ThrowForEvent(EventExceptionTestCase scenario) {
            // Arrange
            var registry = new TestSamples1.TestRegistry2();
            var formattedErrorMsg = string.Format(scenario.ExpectedErrMsg, nameof(registry.MyPublicEvent));

            var config = CreateConfig();
            config.AddPropertyTo(registry.GetType().Name,
                nameof(registry.MyPublicEvent),
                CreatePropConfig(value: scenario.HandlerName));

            var mock = new Dependencies();
            var service = CreateService(mock);

            mock.SetupLoadFrom(returnVal: config);

            // Act
            service.InitializeFrom(CreateOptions());
            var action = () => service.ApplyRegistryConfiguration(registry);

            // Assert
            var ex = action.Should().Throw<RegistryConfigurationException>();
            using (new AssertionScope()) {
                ex.Which.Message.Should().Be(formattedErrorMsg);
                if (scenario.InnerExTypeName is not null) {
                    ex.Which.InnerException.Should().NotBeNull();
                    ex.Which.InnerException!.GetType().FullName.Should().Be(scenario.InnerExTypeName);
                } else {
                    ex.Which.InnerException.Should().BeNull();
                }
            }
        }

        [Theory, MemberData(nameof(ExceptionData), "{1} when {0} but error suppression is on")]
        public void NotThrowForEvent(EventExceptionTestCase scenario) {
            // Arrange
            var registry = new TestSamples1.TestRegistry2();

            var config = CreateConfig();
            config.AddPropertyTo(registry.GetType().Name,
                nameof(registry.MyPublicEvent),
                CreatePropConfig(value: scenario.HandlerName, suppressErrs: true));

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

        #region Test Data
        public static TheoryData<EventExceptionTestCase> ExceptionData(string displayFormat) => new() {
            new EventExceptionTestCase(displayFormat,
                condition: "event handler assembly cannot be found",
                handlerName: "Some.Bogus.Assembly.TestEventHandler",
                expectedErrMsg: "'{0}' event handler could not be loaded from assembly 'Some.Bogus'.",
                innerExType: typeof(FileNotFoundException)),

            new EventExceptionTestCase(displayFormat,
                condition: "event handler declaring type cannot be found",
                handlerName: $"{typeof(TestSamples2.TestRegistry1).FullName}_Oops.TestEventHandler",
                expectedErrMsg: $"'{{0}}' event handler could not be found in type '{nameof(TestSamples2.TestRegistry1)}_Oops'.",
                innerExType: typeof(TypeLoadException)),

            new EventExceptionTestCase(displayFormat,
                condition: "event handler method cannot be found",
                handlerName: $"{typeof(TestSamples2.TestRegistry1).FullName}.TestEventHandler_Oops",
                expectedErrMsg: $"'{{0}}' event handler could not be set from method 'TestEventHandler_Oops'. No such static method found."),

            new EventExceptionTestCase(displayFormat,
                condition: "event handler method incompatible with delegate",
                handlerName: $"{typeof(TestSamples2.TestRegistry1).FullName}.TestInvalidHandler",
                expectedErrMsg: $"'{nameof(TestSamples2.TestRegistry1)}.TestInvalidHandler' is not a compatible event handler for '{{0}}'.",
                innerExType: typeof(ArgumentException)),

            new EventExceptionTestCase(displayFormat,
                condition: "configured handler does not include the assembly name",
                handlerName: $"{nameof(TestSamples2.TestRegistry1)}.TestEventHandler",
                expectedErrMsg: $"Invalid handler name ({nameof(TestSamples2.TestRegistry1)}.TestEventHandler). Please use the fully qualified handler name."),

            new EventExceptionTestCase(displayFormat,
                condition: "configured handler does not include the type name",
                handlerName: "TestEventHandler",
                expectedErrMsg: "Invalid handler name (TestEventHandler). Please use the fully qualified handler name.")
        };
        #endregion

        #region Test Classes
        public class EventExceptionTestCase : IXunitSerializable {
            public string DisplayFormat { get; private set; }
            public string Condition { get; private set; }
            public string HandlerName { get; private set; }
            public string ExpectedErrMsg { get; private set; }
            public string? InnerExTypeName { get; private set; }
            public string? InnerExTypeShortName { get; private set; }

            public EventExceptionTestCase() {
                DisplayFormat = string.Empty;
                Condition = string.Empty;
                HandlerName = string.Empty;
                ExpectedErrMsg = string.Empty;
            }

            public EventExceptionTestCase(string displayFormat, string condition, string handlerName, string expectedErrMsg, Type? innerExType = null) {
                DisplayFormat = displayFormat;
                Condition = condition;
                HandlerName = handlerName;
                ExpectedErrMsg = expectedErrMsg;
                InnerExTypeName = innerExType?.FullName;
                InnerExTypeShortName = innerExType?.Name;
            }

            public override string ToString() => string.Format(DisplayFormat, Condition, InnerExTypeShortName ?? nameof(RegistryConfigurationException)).Trim();
            public void Deserialize(IXunitSerializationInfo info) {
                DisplayFormat = info.GetValue<string>(nameof(DisplayFormat));
                Condition = info.GetValue<string>(nameof(Condition));
                HandlerName = info.GetValue<string>(nameof(HandlerName));
                ExpectedErrMsg = info.GetValue<string>(nameof(ExpectedErrMsg));
                InnerExTypeName = info.GetValue<string?>(nameof(InnerExTypeName));
                InnerExTypeShortName = info.GetValue<string?>(nameof(InnerExTypeShortName));
            }
            public void Serialize(IXunitSerializationInfo info) {
                info.AddValue(nameof(DisplayFormat), DisplayFormat);
                info.AddValue(nameof(Condition), Condition);
                info.AddValue(nameof(HandlerName), HandlerName);
                info.AddValue(nameof(ExpectedErrMsg), ExpectedErrMsg);
                info.AddValue(nameof(InnerExTypeName), InnerExTypeName);
                info.AddValue(nameof(InnerExTypeShortName), InnerExTypeShortName);
            }
        }
        #endregion
    }
}
