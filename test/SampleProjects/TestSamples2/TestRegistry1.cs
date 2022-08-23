using System;
using Microsoft.Extensions.DependencyInjection;
using ServiceRegistryModules;

namespace TestSamples2;
public class TestRegistry1 : AbstractRegistryModule {
    public override void ConfigureServices(IServiceCollection services)
        => services.AddTransient<Service>();

    public class Service { }

    private static void TestEventHandler(object sender, EventArgs e) {
        HandledEvents.HandledEventFor = sender;
        HandledEvents.HandledEventArgs = e;
    }

    private static void TestInvalidHandler() {
        // No-op
    }
}

public static class HandledEvents {
    public static object? HandledEventFor;
    public static EventArgs? HandledEventArgs;
}
