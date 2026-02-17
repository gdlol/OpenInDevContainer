using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

static async Task<string> GetWslPath(string path)
{
    using var process = new Process
    {
        StartInfo = new ProcessStartInfo
        {
            FileName = "wslpath",
            ArgumentList = { "-w", path },
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        },
    };
    process.Start();
    string wslPath = await process.StandardOutput.ReadToEndAsync();
    await process.WaitForExitAsync();
    return wslPath.Trim();
}

static string ToKebabCase(string name)
{
    name = KebabCaseBoundary.Spaces().Replace(name, "$1-$3");
    name = KebabCaseBoundary.Underscore().Replace(name, "$1-$3");
    name = KebabCaseBoundary.LowerUpper().Replace(name, "$1-$2");
    name = KebabCaseBoundary.Acronym().Replace(name, "$1-$2");
    return name.ToLowerInvariant();
}

static string GetHexPath(string path)
{
    return Convert.ToHexStringLower(Encoding.UTF8.GetBytes(path));
}

static async Task GetCommand(string path, string workspaceName)
{
    string hexPath = GetHexPath(path);
    string folderUri = $"vscode-remote://dev-container+{hexPath}/workspaces/{workspaceName}";
    Console.WriteLine($"Folder URI: {folderUri}");
    using var process = new Process
    {
        StartInfo = new ProcessStartInfo
        {
            FileName = "code",
            ArgumentList = { "--folder-uri", folderUri },
            UseShellExecute = false,
            CreateNoWindow = true,
        },
    };
    process.Start();
    await process.WaitForExitAsync();
}

string currentPath = Directory.GetCurrentDirectory();
string workspaceName = new DirectoryInfo(currentPath).Name;
currentPath = await GetWslPath(currentPath);
workspaceName = ToKebabCase(workspaceName);

await GetCommand(currentPath, workspaceName);

internal partial class KebabCaseBoundary
{
    [GeneratedRegex(@"(\S)(\s+)(\S)")]
    public static partial Regex Spaces();

    [GeneratedRegex(@"(\S)(_)(\S)")]
    public static partial Regex Underscore();

    [GeneratedRegex(@"(\p{Ll})(\p{Lu})")]
    public static partial Regex LowerUpper();

    [GeneratedRegex(@"(\p{Lu}|\p{N})(\p{Lu}\p{Ll})")]
    public static partial Regex Acronym();
}
