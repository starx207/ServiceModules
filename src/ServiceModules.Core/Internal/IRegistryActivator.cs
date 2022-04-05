using System.Collections.Generic;

namespace ServiceRegistryModules.Internal;

internal interface IRegistryActivator {
    IEnumerable<IRegistryModule> InstantiateRegistries(RegistryOptions options);
}
