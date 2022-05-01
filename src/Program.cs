string serviceName = RequireEnvironmentVariable("INPUT_SERVICENAME");
string hosts = RequireEnvironmentVariable("INPUT_HOSTS");
string username = RequireEnvironmentVariable("INPUT_USERNAME");
int port = int.Parse(RequireEnvironmentVariable("INPUT_PORT"));
string key = RequireEnvironmentVariable("INPUT_KEY");
string source = RequireEnvironmentVariable("INPUT_SOURCE");

Console.WriteLine(serviceName);
Console.WriteLine(hosts);
Console.WriteLine(username);
Console.WriteLine(port);
Console.WriteLine(key);
Console.WriteLine(source);

string RequireEnvironmentVariable(string key)
{
    string? value = Environment.GetEnvironmentVariable(key);
    
    if (string.IsNullOrEmpty(value))
    {
        throw new ArgumentException($"Environment variable {key} is not set");
    }
    
    return value;
}