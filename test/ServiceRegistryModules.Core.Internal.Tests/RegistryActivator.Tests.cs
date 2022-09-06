using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Extensions.DependencyInjection;
using ServiceRegistryModules.Exceptions;
using Xunit;

namespace ServiceRegistryModules.Internal.Tests;
public class RegistryActivator_Should {
    #region Tests
    [Fact]
    public void BeAbleToActivateARegistry_WhenAllRequiredParametersSatisfied() {
        // Arrange
        var service = CreateService();
        var options = CreateOptions(
            registryTypes: new[] {
                typeof(RegistryWithNoConstructor),
                typeof(RegistryWithTestService),
                typeof(RegistryWithDerivedTestService),
                typeof(InternalRegistry)
            },
            providers: new[] {
                new TestDerivedImplementation()
            }
        );

        // Act
        var registryTypesCreated = service.InstantiateRegistries(options).Select(m => m.GetType());

        // Assert
        Assert.Equal(options.RegistryTypes, registryTypesCreated);
    }

    [Fact]
    public void PassProviders_ToRegistryWhenInstantiating() {
        // Arrange
        var stringProvider = "some-string";
        var boolProvider = true;
        var numProvider = 234;

        var service = CreateService();
        var options = CreateOptions(
            registryTypes: new[] { typeof(RegistryWithMultipleParameters) },
            providers: new object[] { stringProvider, boolProvider, numProvider }
        );

        // Act
        var registry = (RegistryWithMultipleParameters)service.InstantiateRegistries(options).Single();

        // Assert
        Assert.Equal(
            new object[] { stringProvider, numProvider, boolProvider },
            new object[] { registry.Param1, registry.Param2, registry.Param3 }
        );
    }

    [Fact]
    public void NotCreateNonPublicRegistries_WhenOptionsSpecifyPublicOnly() {
        // Arrange
        var options = CreateOptions(
            publicOnly: true,
            registryTypes: new[] {
                typeof(RegistryWithNoConstructor),
                typeof(InternalRegistry)
            }
        );
        var service = CreateService();

        // Act
        var createdRegistries = service.InstantiateRegistries(options).Select(m => m.GetType());

        // Assert
        Assert.Equal(
            new[] { typeof(RegistryWithNoConstructor) },
            createdRegistries
        );
    }

    [Theory,
        InlineData(true),
        InlineData(false)]
    public void ReturnAnEmptySetOfRegistries_IfNoRegistryTypesGivenInTheOptions(bool publicOnly) {
        // Arrange
        var options = CreateOptions(
            publicOnly: publicOnly,
            registryTypes: publicOnly ? new[] { typeof(InternalRegistry) } : Array.Empty<Type>());
        var service = CreateService();

        // Act
        var registries = service.InstantiateRegistries(options);

        // Assert
        Assert.False(registries.Any());
    }

    [Fact]
    public void ThrowInvalidOperationException_WhenNoSuitableConstructorFound_AndListAllowedParams() {
        // Arrange
        var options = CreateOptions(
            registryTypes: new[] { typeof(RegistryWithMultipleParameters) },
            providers: new object[] { "some-string-provider", true }
        );
        var service = CreateService();

        var expectedMsg = $"Unable to activate {nameof(IRegistryModule)} of type '{nameof(RegistryWithMultipleParameters)}' -- no suitable constructor found. " +
            $"Allowable constructor parameters are: {typeof(string)}, {typeof(bool)}";

        // Act
        void ShouldThrow() => service.InstantiateRegistries(options);

        // Assert
        var ex = Assert.Throws<RegistryActivationException>(ShouldThrow);
        Assert.Equal(expectedMsg, ex.Message);
    }

    [Fact]
    public void ReturnRegistryInstances_DefinedInTheOptions_AlongWithTheCreatedInstances() {
        // Arrange
        var options = CreateOptions(instances: new[] { new RegistryWithNoConstructor() }, registryTypes: new[] { typeof(InternalRegistry) });
        var service = CreateService();

        // Act
        var registries = service.InstantiateRegistries(options);

        // Assert
        using (new AssertionScope()) {
            registries.Should().Contain(options.Registries.Single())
                .And.HaveCount(2);
            registries.Select(m => m.GetType()).Should().Contain(typeof(InternalRegistry));
        }
    }
    #endregion

    #region Test Helpers
    private static IRegistryActivator CreateService() => new RegistryActivator();

    private static RegistryOptions CreateOptions(IEnumerable<IRegistryModule>? instances = null, IEnumerable<Type>? registryTypes = null, IEnumerable<object>? providers = null, bool? publicOnly = null) {
        var options = new RegistryOptions();

        if (registryTypes is not null) {
            options.RegistryTypes.AddRange(registryTypes);
        }

        if (providers is not null) {
            options.Providers.AddRange(providers);
            options.AllowedRegistryCtorArgTypes.AddRange(options.Providers.Select(p => p.GetType()));
        }

        if (publicOnly is not null) {
            options.PublicOnly = publicOnly.Value;
        }

        if (instances is not null) {
            options.Registries = instances.ToList();
        }

        return options;
    }
    #endregion

    #region Test Classes
    public class RegistryWithNoConstructor : AbstractRegistryModule {
        public override void ConfigureServices(IServiceCollection services) => throw new System.NotImplementedException();
    }

    internal class InternalRegistry : AbstractRegistryModule {
        public override void ConfigureServices(IServiceCollection services) => throw new System.NotImplementedException();
    }

    public class RegistryWithTestService : AbstractRegistryModule {
        public RegistryWithTestService(ITestInterface _) {

        }
        public override void ConfigureServices(IServiceCollection services) => throw new System.NotImplementedException();
    }

    public class RegistryWithDerivedTestService : AbstractRegistryModule {
        public RegistryWithDerivedTestService(IDerivedTestInterface _) {

        }
        public override void ConfigureServices(IServiceCollection services) => throw new System.NotImplementedException();
    }

    public class RegistryWithMultipleParameters : AbstractRegistryModule {
        public RegistryWithMultipleParameters(string param1, int param2, bool param3) {
            Param1 = param1;
            Param2 = param2;
            Param3 = param3;
        }

        public string Param1 { get; }
        public int Param2 { get; }
        public bool Param3 { get; }

        public override void ConfigureServices(IServiceCollection services) => throw new System.NotImplementedException();
    }

    public interface ITestInterface { }
    public interface IDerivedTestInterface : ITestInterface { }

    public class TestImplementation : ITestInterface { }
    public class TestDerivedImplementation : IDerivedTestInterface { }

    #endregion
}
