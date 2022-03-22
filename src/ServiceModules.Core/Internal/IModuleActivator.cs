using System.Collections.Generic;

namespace ServiceModules.Internal;

internal interface IModuleActivator {
    IEnumerable<IRegistryModule> InstantiateModules(ModuleOptions options);
}