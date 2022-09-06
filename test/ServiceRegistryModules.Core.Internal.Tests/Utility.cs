using System;

namespace ServiceRegistryModules.Core.Internal.Tests;
public class Utility {
    public static object? HandledEventFor = null;
    public static EventArgs? HandledEventArgs = null;

    public static void OnMyPublicEvent(object sender, EventArgs e) {
        HandledEventFor = sender;
        HandledEventArgs = e;
    }
}
