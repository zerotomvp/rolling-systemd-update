namespace ZeroToMvp.Github.Actions.RollingSystemdUpdate;

class FileEntry
{
    public string Path { get; set; } = null!;
    public string Src { get; set; } = null!;
}

static class Utils
{
    public static IEnumerable<FileEntry> EnumerateFilesRecursively(string src)
    {
        return EnumerateFilesRecursively(src, null)
            .Select(relative => new FileEntry
            {
                Path = relative,
                Src = Path.Combine(src, relative)
            });
    }

    public static IEnumerable<string> EnumerateFilesRecursively(string top, string? dir)
    {
        string MakeRelative(string file) => Path.GetRelativePath(top, file);

        dir ??= top;

        foreach (var sub in Directory.GetDirectories(dir))
        {
            yield return MakeRelative(sub);

            foreach (var file in EnumerateFilesRecursively(top, sub))
            {
                yield return file;
            }
        }

        foreach (var file in Directory.GetFiles(dir))
        {
            yield return MakeRelative(file);
        }
    }

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