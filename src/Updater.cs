using Renci.SshNet;
using Renci.SshNet.Common;

namespace ZeroToMvp.Github.Actions.RollingSystemdUpdate;

class Updater : IDisposable
{
    private readonly SshClient ssh;
    private readonly SftpClient sftp;

    public Updater(UpdaterArguments args)
    {
        Args = args;

        var connectionInfo = new ConnectionInfo(args.Host, args.Port, args.Username, args.Auth);

        ssh = new SshClient(connectionInfo);
        sftp = new SftpClient(connectionInfo);

        if (Args.ExpectedFingerprint != null)
        {
            ssh.HostKeyReceived += OnHostKeyReceived!;
            sftp.HostKeyReceived += OnHostKeyReceived!;
        }
    }

    public UpdaterArguments Args { get; set; }

    public void Execute()
    {
        ssh.Connect();
        sftp.Connect();

        // get service definition first, it could fail

        var serviceDef = GetServiceDefinition();

        string lastPath = $"{serviceDef.WorkingDirectory}.last";
        
        // copy files

        string runNumber = Environment.GetEnvironmentVariable("GITHUB_RUN_NUMBER")!;
        string runAttempt = Environment.GetEnvironmentVariable("GITHUB_RUN_ATTEMPT")!;

        string tmp = $"/tmp/{Args.ServiceName}.{runNumber}.{runAttempt}";

        TransferFilesRecursively(tmp);

        // run update

        if (IsServiceRunning())
        {
            StopService();
        }

        // remove last

        ForceRemovePath(lastPath);

        // move current to last

        MovePath(serviceDef.WorkingDirectory, lastPath);

        // move temp to current

        MovePath(tmp, serviceDef.WorkingDirectory);

        // copy permissions + ownership

        CopyPermissions(lastPath, serviceDef.WorkingDirectory);
        CopyPermissions(lastPath, serviceDef.WorkingDirectory);

        // start service

        StartService();

        // wait for it to come up

        var firstBinding = serviceDef.Bindings.First();

        for (int i = 0; i < 60; ++i)
        {
            if (CheckHttpStatus(firstBinding))
            {
                break;
            }

            Thread.Sleep(1000);
        }

        throw new InvalidOperationException("Service didn't start after update");
    }

    public void Rollback()
    {
        var serviceDef = GetServiceDefinition();

        string lastPath = $"{serviceDef.WorkingDirectory}.last";

        // stop service

        if (IsServiceRunning())
        {
            StopService();
        }

        // delete current

        // TODO: check last exists first

        ForceRemovePath(serviceDef.WorkingDirectory);

        // move last to current

        MovePath(lastPath, serviceDef.WorkingDirectory);
        
        // start

        StartService();

        // wait for it to come up

        var firstBinding = serviceDef.Bindings.First();

        for (int i = 0; i < 60; ++i)
        {
            if (CheckHttpStatus(firstBinding))
            {
                break;
            }

            Thread.Sleep(1000);
        }

        throw new InvalidOperationException("Service didn't start after rollback");
    }

    ServiceDefinition GetServiceDefinition()
    {
        var cmd = ssh.RunCommand($"cat /etc/systemd/system/{Args.ServiceName}.service");

        var kvs = Utils.ParseKeyValues(cmd.Result);

        return new()
        {
            WorkingDirectory = kvs["WorkingDirectory"],
            User = kvs["User"],
            Environment = kvs["Environment_ASPNETCORE_ENVIRONMENT"],
            Bindings = Utils.ParseBindings(kvs["Environment_ASPNETCORE_URLS"]).ToArray()
        };
    }

    private void ForceRemovePath(string path)
    {
        ssh.RunCommand($"rm -rf {path}");
    }

    private void RecursiveCopy(string src, string dest)
    {
        ssh.RunCommand($"cp -R {src} {dest}");
    }

    private void MovePath(string src, string dest)
    {
        ssh.RunCommand($"mv {src} {dest}");
    }

    private void CopyPermissions(string src, string dest)
    {
        ssh.RunCommand($"chmod --reference={src} {dest}");
    }

    private void CopyOwnership(string src, string dest)
    {
        ssh.RunCommand($"chmod --reference={src} {dest}");
    }

    private bool IsServiceRunning()
    {
        var cmd = ssh.RunCommand($"systemctl is-active --quiet {Args.ServiceName}");

        return cmd.ExitStatus == 0;
    }

    private void StopService()
    {
        ssh.RunCommand($"systemctl stop {Args.ServiceName}");
    }

    private void StartService()
    {
        ssh.RunCommand($"systemctl start {Args.ServiceName}");
    }

    private bool CheckHttpStatus(string binding, string path = "/api/health", int expectedStatus = 200)
    {
        var cmd = ssh.RunCommand($"curl --write-out \"%{{http_code}}\" --output /dev/null --silent {binding}{path}");

        return cmd.Result == expectedStatus.ToString();
    }

    private void TransferFilesRecursively(string dest)
    {
        string src = Args.SourceDirectory;

        var files = Utils.EnumerateFilesRecursively(src)
            .Select(relative =>
            {
                string fullPathDest = Path.Combine(dest, relative);

                return new
                {
                    Relative = relative,
                    Src = Path.Combine(src, relative),
                    Dest = fullPathDest,
                    DestDirectory = Path.GetDirectoryName(fullPathDest)
                };
            })
            .ToArray();

        var destDirectories = files.Select(x => x.DestDirectory).ToArray();

        foreach (var destDirectory in destDirectories)
        {
            ssh.RunCommand($"mkdir -p {destDirectory}");
        }

        foreach (var file in files)
        {
            using var fileStream = File.OpenRead(file.Src);

            sftp.UploadFile(fileStream, file.Dest);
        }
    }

    private void OnHostKeyReceived(object sender, HostKeyEventArgs e)
    {
        e.CanTrust = e.FingerPrint.SequenceEqual(Args.ExpectedFingerprint!);
    }

    public void Dispose()
    {
        try
        {
            ssh.Dispose();
        } catch { }

        try
        {
            sftp.Dispose();
        } catch { }
    }
}