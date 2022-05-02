using System.Text;
using Renci.SshNet;

namespace ZeroToMvp.Github.Actions.RollingSystemdUpdate;

public class Program
{
    public static int Main()
    {
        try
        {
            Execute();

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FATAL: {0}", ex);

            return 1;
        }
    }

    private static void Execute()
    {
        string serviceName = RequireEnvironmentVariable("INPUT_SERVICENAME");
        string[] hosts = RequireEnvironmentVariableAsStrings("INPUT_HOSTS");
        string[]? fingerprints = OptionalEnvironmentVariableAsStrings("INPUT_FINGERPRINTS");
        string username = RequireEnvironmentVariable("INPUT_USERNAME");
        int port = RequireEnvironmentVariableAsInt32("INPUT_PORT");
        string key = RequireEnvironmentVariable("INPUT_KEY");
        string source = RequireEnvironmentVariable("INPUT_SOURCE");

        bool.TryParse(Environment.GetEnvironmentVariable("INPUT_DEBUG"), out bool debug);

        if (fingerprints != null && fingerprints.Length != hosts.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(fingerprints),
                string.Join(",", fingerprints),
                "The number of fingerprints must match the number of hosts.");
        }

        var auth = new PrivateKeyAuthenticationMethod(username,
            new PrivateKeyFile(new MemoryStream(Encoding.UTF8.GetBytes(key))));

        byte[][]? expectedFingerPrints = fingerprints?.Select(Convert.FromHexString).ToArray();

        var updaters = hosts
            .Select((host, index) =>
            {
                var args = new UpdaterArguments
                {
                    ServiceName = serviceName,
                    Host = host,
                    Port = port,
                    Username = username,
                    Auth = auth,
                    ExpectedFingerprint = expectedFingerPrints?[index],
                    SourceDirectory = source,
                    Debug = debug
                };

                return new Updater(args);
            })
            .ToArray();

        var successful = new List<Updater>();
        Updater? inFlight = null;

        try
        {
            foreach (var updater in updaters)
            {
                Console.WriteLine("Starting UPDATE of {0}", updater.Args.Host);

                inFlight = updater;

                updater.Execute();

                successful.Add(updater);

                Console.WriteLine("UPDATE of {0} successful", updater.Args.Host);
            }

            inFlight = null;

            Console.WriteLine("All successful.");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("UPDATE failed: {0}", ex);

            var targets = successful
                .Concat(new[] { inFlight })
                .Where(x => x != null)
                .Distinct()
                .ToArray();

            foreach (var updater in targets)
            {
                Console.WriteLine("Starting ROLLBACK of {0}", updater!.Args.Host);

                updater.Rollback();

                Console.WriteLine("ROLLBACK of {0} successful", updater.Args.Host);
            }
            
            throw;
        }
        finally
        {
            foreach (var updater in updaters)
            {
                updater.Dispose();
            }
        }
    }

    private static string RequireEnvironmentVariable(string key)
    {
        string? value = Environment.GetEnvironmentVariable(key);

        if (string.IsNullOrEmpty(value))
        {
            throw new ArgumentException($"Environment variable {key} is not set");
        }

        return value;
    }

    private static int RequireEnvironmentVariableAsInt32(string key)
    {
        string value = RequireEnvironmentVariable(key);

        if (!int.TryParse(value, out int result))
        {
            throw new ArgumentOutOfRangeException(
                key,
                value,
                "Variable must be an integer.");
        }

        return result;
    }

    private static string[] RequireEnvironmentVariableAsStrings(string key, char sep = ',')
    {
        string value = RequireEnvironmentVariable(key);

        return value.Split(
            new[] { sep },
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }

    private static string[]? OptionalEnvironmentVariableAsStrings(string key, char sep = ',')
    {
        string? value = Environment.GetEnvironmentVariable(key);

        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        return value.Split(
            new[] { sep },
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }
}