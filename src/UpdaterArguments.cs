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
}