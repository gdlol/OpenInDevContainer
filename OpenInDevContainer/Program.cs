using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

static async Task<string> Run(
    string fileName,
    IEnumerable<string> arguments,
    bool shell = false,
    bool captureOutput = false
)
{
    var startInfo = new ProcessStartInfo
    {
        FileName = fileName,
        RedirectStandardOutput = captureOutput,
        UseShellExecute = shell,
        CreateNoWindow = true,
    };
    foreach (var arg in arguments)
    {
        startInfo.ArgumentList.Add(arg);
    }
    using var process = new Process { StartInfo = startInfo };
    process.Start();
    string output = string.Empty;
    if (captureOutput)
    {
        output = await process.StandardOutput.ReadToEndAsync();
    }
    await process.WaitForExitAsync();
    return output;
}

static async Task<string> TryConvertWslPath(string path)
{
    string wslpathPath = "/usr/bin/wslpath";
    if (!File.Exists(wslpathPath))
    {
        return path;
    }

    string wslPath = await Run(wslpathPath, ["-w", path], captureOutput: true);
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
    await Run("code", ["--folder-uri", folderUri]);
}

string currentPath = Directory.GetCurrentDirectory();
string workspaceName = new DirectoryInfo(currentPath).Name;
if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
{
    currentPath = await TryConvertWslPath(currentPath);
}
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
