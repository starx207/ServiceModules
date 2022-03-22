using System.Collections.Generic;

namespace ServiceModules.Internal;

internal interface IModuleConfigLoader {
    Dictionary<string, IReadOnlyDictionary<string, string>>? LoadFrom(ModuleOptions options);
}
