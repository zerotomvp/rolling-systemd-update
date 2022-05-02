using System.IO.Compression;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
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

        // set permissions + ownership

        SetPermissions(serviceDef.WorkingDirectory, "774");
        RecursivelySetOwnership(serviceDef.WorkingDirectory, serviceDef.User);

        // start service

        StartService();

        // wait for it to come up

        var firstBinding = serviceDef.Bindings.First();

        for (int i = 0; i < 60; ++i)
        {
            if (CheckHttpStatus(firstBinding))
            {
                Console.WriteLine("Update completed.");
                return;
            }

            Thread.Sleep(1000);
        }

        throw new InvalidOperationException("Service didn't start after update");
    }

    public void Rollback()
    {
        var serviceDef = GetServiceDefinition();

        string lastPath = $"{serviceDef.WorkingDirectory}.last";

        if (Args.Debug)
        {
            Console.WriteLine($"WorkingDirectory={serviceDef.WorkingDirectory}");
            Console.WriteLine($"User={serviceDef.User}");
            Console.WriteLine($"Bindings={string.Join(",", serviceDef.Bindings)}");
        }

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

        if (Args.Debug)
        {
            RunAndLogCommand($"ls -l {serviceDef.WorkingDirectory}");
        }
        
        // start

        StartService();

        // wait for it to come up

        var firstBinding = serviceDef.Bindings.First();

        for (int i = 0; i < 60; ++i)
        {
            if (CheckHttpStatus(firstBinding))
            {
                Console.WriteLine("Rollback completed.");
                return;
            }

            Thread.Sleep(1000);
        }

        throw new InvalidOperationException("Service didn't start after rollback");
    }

    ServiceDefinition GetServiceDefinition()
    {
        var cmd = ssh.RunCommand($"cat /etc/systemd/system/{Args.ServiceName}.service");

        var kvs = Utils.ParseKeyValues(cmd.Result);

        try
        {
            return new()
            {
                WorkingDirectory = kvs["WorkingDirectory"],
                User = kvs["User"],
                Bindings = Utils.ParseBindings(kvs["Environment_ASPNETCORE_URLS"]).ToArray()
            };
        }
        catch (KeyNotFoundException)
        {
            throw new InvalidOperationException(
                $"The service definition must define the WorkingDirectory and the ASPNETCORE_URLS env variable; found: {cmd.Result}");
        }
    }

    private void ForceRemovePath(string path)
    {
        RunAndLogCommand($"rm -rf {path}");
    }

    private void MovePath(string src, string dest)
    {
        RunAndLogCommand($"mv {src} {dest}");
    }

    private void SetPermissions(string dest, string permissions)
    {
        RunAndLogCommand($"chmod {permissions} {dest}");
    }

    private void RecursivelySetOwnership(string dest, string owner)
    {
        RunAndLogCommand($"chown -R {owner}:{owner} {dest}");
    }

    private bool IsServiceRunning()
    {
        var cmd = RunAndLogCommand($"systemctl is-active --quiet {Args.ServiceName}");

        return cmd.ExitStatus == 0;
    }

    private void StopService()
    {
        RunAndLogCommand($"systemctl stop {Args.ServiceName}");
    }

    private void StartService()
    {
        RunAndLogCommand($"systemctl start {Args.ServiceName}");
    }

    private bool CheckHttpStatus(string binding, string path = "/api/health", int expectedStatus = 200)
    {
        var cmd = RunAndLogCommand($"curl --write-out \"%{{http_code}}\" --output /dev/null --silent {binding}{path}");

        return cmd.Result == expectedStatus.ToString();
    }

    private void TransferFilesRecursively(string dest)
    {
        string src = Args.SourceDirectory;
        string tgzLocal = $"{Path.GetFileName(dest)}.tgz";
        string tgzDest = $"{dest}.tgz";

        using (var outStream = File.Create(tgzLocal))
        using (var gzoStream = new GZipOutputStream(outStream))
        using (var tarArchive = TarArchive.CreateOutputTarArchive(gzoStream))
        {
            foreach (var file in Utils.EnumerateFilesRecursively(src))
            {
                var tarEntry = TarEntry.CreateEntryFromFile(file.Src);

                tarEntry.Name = file.Relative;

                tarArchive.WriteEntry(tarEntry, true);
            }
        } 
        
        RunAndLogCommand($"mkdir -p {dest}");

        UploadFile(tgzLocal, tgzDest);

        RunAndLogCommand($"tar xf {tgzDest} -C {dest}");

        if (!Args.Debug)
        {
            ForceRemovePath(tgzDest);
        }
        else
        {
            RunAndLogCommand($"ls -l {tgzDest}");
            RunAndLogCommand($"ls -l {dest}");
        }
    }

    private void UploadFile(string src, string dest)
    {
        Console.WriteLine($"Transfering file {src} => {dest}");

        using var fileStream = File.OpenRead(src);

        sftp.UploadFile(fileStream, dest);
    }
    
    private SshCommand RunAndLogCommand(string commandText)
    {
        Console.WriteLine($"$ {commandText}");

        var command = ssh.RunCommand(commandText);

        if (!string.IsNullOrEmpty(command.Result))
        {
            Console.WriteLine("\t{0}", command.Result);
        }

        return command;
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