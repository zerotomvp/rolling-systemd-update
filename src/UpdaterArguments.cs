using Renci.SshNet;

namespace ZeroToMvp.Github.Actions.RollingSystemdUpdate;

class UpdaterArguments
{
    public string ServiceName { get; set; } = null!;
    public string Host { get; set; } = null!;
    public int Port { get; set; }
    public string Username { get; set; } = null!;
    public PrivateKeyAuthenticationMethod Auth { get; set; } = null!;
    public byte[]? ExpectedFingerprint { get; set; }
    public string SourceDirectory { get; set; } = null!;
    public bool Debug { get; set; }
    public bool HealthCheck { get; set; } = true;
    public string HealthCheckPath { get; set; } = null!;
}