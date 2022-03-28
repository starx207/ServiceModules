using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ServiceModules.Internal.Tests;
public class ModuleActivator_Should {
    #region Tests
    [Fact]
    public void BeAbleToActivateAModule_WhenAllRequiredParametersSatisfied() {
        // Arrange
        var service = CreateService();
        var options = CreateOptions(
            moduleTypes: new[] {
                typeof(ModuleWithNoConstructor),
                typeof(ModuleWithTestService),
                typeof(ModuleWithDerivedTestService),
                typeof(InternalModule)
            },
            providers: new[] {
                new TestDerivedImplementation()
            }
        );

        // Act
        var moduleTypesCreated = service.InstantiateModules(options).Select(m => m.GetType());

        // Assert
        Assert.Equal(options.ModuleTypes, moduleTypesCreated);
    }

    [Fact]
    public void PassProviders_ToModuleWhenInstantiating() {
        // Arrange
        var stringProvider = "some-string";
        var boolProvider = true;
        var numProvider = 234;

        var service = CreateService();
        var options = CreateOptions(
            moduleTypes: new[] { typeof(ModuleWithMultipleParameters) },
            providers: new object[] { stringProvider, boolProvider, numProvider }
        );

        // Act
        var module = (ModuleWithMultipleParameters)service.InstantiateModules(options).Single();

        // Assert
        Assert.Equal(
            new object[] { stringProvider, numProvider, boolProvider },
            new object[] { module.Param1, module.Param2, module.Param3 }
        );
    }

    [Fact]
    public void NotCreateNonPublicModules_WhenOptionsSpecifyPublicOnly() {
        // Arrange
        var options = CreateOptions(
            publicOnly: true,
            moduleTypes: new[] {
                typeof(ModuleWithNoConstructor),
                typeof(InternalModule)
            }
        );
        var service = CreateService();

        // Act
        var createdModules = service.InstantiateModules(options).Select(m => m.GetType());

        // Assert
        Assert.Equal(
            new[] { typeof(ModuleWithNoConstructor) },
            createdModules
        );
    }

    [Theory,
        InlineData(true),
        InlineData(false)]
    public void ReturnAnEmptySetOfModules_IfNoModuleTypesGivenInTheOptions(bool publicOnly) {
        // Arrange
        var options = CreateOptions(
            publicOnly: publicOnly,
            moduleTypes: publicOnly ? new[] { typeof(InternalModule) } : Array.Empty<Type>());
        var service = CreateService();

        // Act
        var modules = service.InstantiateModules(options);

        // Assert
        Assert.False(modules.Any());
    }

    [Fact]
    public void ThrowInvalidOperationException_WhenNoSuitableConstructorFound_AndListAllowedParams() {
        // Arrange
        var options = CreateOptions(
            moduleTypes: new[] { typeof(ModuleWithMultipleParameters) },
            providers: new object[] { "some-string-provider", true }
        );
        var service = CreateService();

        var expectedMsg = $"Unable to activate {nameof(IRegistryModule)} of type '{nameof(ModuleWithMultipleParameters)}' -- no suitable constructor found. " +
            $"Allowable constructor parameters are: {typeof(string)}, {typeof(bool)}";

        // Act
        void ShouldThrow() => service.InstantiateModules(options);

        // Assert
        var ex = Assert.Throws<InvalidOperationException>(ShouldThrow);
        Assert.Equal(expectedMsg, ex.Message);
    }

    [Fact]
    public void ReturnModuleInstances_DefinedInTheOptions_AlongWithTheCreatedInstances() {
        // Arrange
        var options = CreateOptions(instances: new[] { new ModuleWithNoConstructor() }, moduleTypes: new[] { typeof(InternalModule) });
        var service = CreateService();

        // Act
        var modules = service.InstantiateModules(options);

        // Assert
        using (new AssertionScope()) {
            modules.Should().Contain(options.Modules.Single())
                .And.HaveCount(2);
            modules.Select(m => m.GetType()).Should().Contain(typeof(InternalModule));
        }
    }
    #endregion

    #region Test Helpers
    private static IModuleActivator CreateService() => new ModuleActivator();

    private static ModuleOptions CreateOptions(IEnumerable<IRegistryModule>? instances = null, IEnumerable<Type>? moduleTypes = null, IEnumerable<object>? providers = null, bool? publicOnly = null) {
        var options = new ModuleOptions();

        if (moduleTypes is not null) {
            options.ModuleTypes.AddRange(moduleTypes);
        }

        if (providers is not null) {
            options.Providers.AddRange(providers);
            options.AllowedModuleArgTypes.AddRange(options.Providers.Select(p => p.GetType()));
        }

        if (publicOnly is not null) {
            options.PublicOnly = publicOnly.Value;
        }

        if (instances is not null) {
            options.Modules = instances.ToList();
        }

        return options;
    }
    #endregion

    #region Test Classes
    public class ModuleWithNoConstructor : AbstractRegistryModule {
        public override void ConfigureServices(IServiceCollection services) => throw new System.NotImplementedException();
    }

    internal class InternalModule : AbstractRegistryModule {
        public override void ConfigureServices(IServiceCollection services) => throw new System.NotImplementedException();
    }

    public class ModuleWithTestService : AbstractRegistryModule {
        public ModuleWithTestService(ITestInterface _) {

        }
        public override void ConfigureServices(IServiceCollection services) => throw new System.NotImplementedException();
    }

    public class ModuleWithDerivedTestService : AbstractRegistryModule {
        public ModuleWithDerivedTestService(IDerivedTestInterface _) {

        }
        public override void ConfigureServices(IServiceCollection services) => throw new System.NotImplementedException();
    }

    public class ModuleWithMultipleParameters : AbstractRegistryModule {
        public ModuleWithMultipleParameters(string param1, int param2, bool param3) {
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
