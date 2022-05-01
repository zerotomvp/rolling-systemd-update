if (args.Length != 6) 
{
    throw new ArgumentOutOfRangeException(
        nameof(args), 
        string.Join(", ", args),
        "Expected 6 arguments");
}

string serviceName = args[0];
string hosts = args[1];
string username = args[2];
int port = int.Parse(args[3]);
string key = args[4];
string source = args[5];

Console.WriteLine(serviceName);
Console.WriteLine(hosts);
Console.WriteLine(username);
Console.WriteLine(port);
Console.WriteLine(key);
Console.WriteLine(source);
