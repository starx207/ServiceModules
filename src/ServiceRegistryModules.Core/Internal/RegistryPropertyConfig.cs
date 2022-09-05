namespace ServiceRegistryModules.Internal;
public class RegistryPropertyConfig {
    public object? Value { get; set; }
    public bool SuppressErrors { get; set; }
    public ConfigurationType Type { get; set; }
    public string? HintPath { get; set; }
}
