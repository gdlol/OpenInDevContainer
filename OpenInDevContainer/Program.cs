using System.CommandLine;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

static async Task<string> TryConvertWslPath(string path)
{
    string wslpathPath = "/usr/bin/wslpath";
    if (!File.Exists(wslpathPath))
    {
        return path;
    }

    var wslPath = await Subprocess.Run(wslpathPath, ["-w", path], captureOutput: true);
    return wslPath.Trim();
}

static string ToKebabCase(string name)
{
    name = KebabCaseBoundary.Spaces().Replace(name, "$1-$3");
    name = KebabCaseBoundary.Underscore().Replace(name, "$1-$3");
    name = KebabCaseBoundary.Dot().Replace(name, "$1-$3");
    name = KebabCaseBoundary.LowerUpper().Replace(name, "$1-$2");
    name = KebabCaseBoundary.Acronym().Replace(name, "$1-$2");
    return name.ToLowerInvariant();
}

static async Task GetCommand(string path, string workspaceName, bool debug)
{
    string hexPath = Convert.ToHexStringLower(Encoding.UTF8.GetBytes(path));
    string folderUri = $"vscode-remote://dev-container+{hexPath}/workspaces/{workspaceName}";
    if (debug)
    {
        Console.Error.WriteLine($"Path: {path}");
        Console.Error.WriteLine($"Folder URI: {folderUri}");
    }
    await Subprocess.Run("code", ["--folder-uri", folderUri]);
}

var rootCommand = new RootCommand("Open in Dev Container")
{
    Arguments = { Arguments.Workspace },
    Options = { Options.Debug },
    CommandAction = async (parseResult, token) =>
    {
        var workspace = parseResult.GetRequiredValue(Arguments.Workspace);
        var debug = parseResult.GetValue(Options.Debug);

        string workspacePath = workspace.FullName;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            workspacePath = await TryConvertWslPath(workspacePath);
        }

        string workspaceName = workspace.Name;
        workspaceName = ToKebabCase(workspaceName);
        await Subprocess.Run("mkdir", ["/root/test"], cancellationToken: token);

        await GetCommand(workspacePath, workspaceName, debug);
    },
};

await rootCommand.Parse(args).InvokeAsync();

internal partial class KebabCaseBoundary
{
    [GeneratedRegex(@"(\S)(\s+)(\S)")]
    public static partial Regex Spaces();

    [GeneratedRegex(@"(\S)(_)(\S)")]
    public static partial Regex Underscore();

    [GeneratedRegex(@"(\S)(\.)(\S)")]
    public static partial Regex Dot();

    [GeneratedRegex(@"(\p{Ll})(\p{Lu})")]
    public static partial Regex LowerUpper();

    [GeneratedRegex(@"(\p{Lu}|\p{N})(\p{Lu}\p{Ll})")]
    public static partial Regex Acronym();
}
