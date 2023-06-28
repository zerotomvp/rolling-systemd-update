namespace ZeroToMvp.Github.Actions.RollingSystemdUpdate;

class FileEntry
{
    public string Path { get; set; } = null!;
    public string Src { get; set; } = null!;
}

static class Utils
{
    public static Dictionary<string, string> ParseKeyValues(string s)
    {
        return s
            .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line =>
            {
                int indexOfEq = line.IndexOf('=');

                if (indexOfEq == -1)
                {
                    return (KeyValuePair<string, string>?)null;
                }

                string key = line.Substring(0, indexOfEq);

                if (key == "Environment")
                {
                    int nextIndexOfEq = line.IndexOf('=', indexOfEq + 1);

                    key += "_" + line.Substring(indexOfEq + 1, nextIndexOfEq - indexOfEq - 1);

                    indexOfEq = nextIndexOfEq;
                }

                string value = line.Substring(indexOfEq + 1);

                return new KeyValuePair<string, string>(key, value);
            })
            .Where(x => x != null)
            .Select(x => x!.Value)
            .ToDictionary(x => x.Key, x => x.Value);
    }

    public static IEnumerable<string> ParseBindings(string urls)
    {
        return urls.Split(
                new[] { ';' },
                StringSplitOptions.TrimEntries)
            .Select(s => s.Replace("*", "localhost"));
    }
}