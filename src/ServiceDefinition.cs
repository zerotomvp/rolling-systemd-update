namespace ZeroToMvp.Github.Actions.RollingSystemdUpdate;

public class ServiceDefinition
{
    public string WorkingDirectory { get; set; } = null!;

    public string[] Bindings { get; set; } = null!;
}