using System.CommandLine;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

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

static async ValueTask<string?> TryGetDevContainerConfig(string workspacePath, CancellationToken cancellationToken)
{
    string[] candidatePaths =
    [
        // Path.Combine(workspacePath, ".config/devcontainer/.devcontainer.json"),
        Path.Combine(workspacePath, ".devcontainer/devcontainer.json"),
        Path.Combine(workspacePath, ".devcontainer.json"),
    ];
    foreach (string candidatePath in candidatePaths)
    {
        if (File.Exists(candidatePath))
        {
            return await File.ReadAllTextAsync(candidatePath, cancellationToken);
        }
    }
    return null;
}

static string? TryGetWorkspaceFolder(string devcontainerConfig)
{
    var config = JsonDocument
        .Parse(
            devcontainerConfig,
            new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }
        )
        .RootElement;
    if (config.TryGetProperty("workspaceFolder", out JsonElement workspaceFolder))
    {
        return workspaceFolder.GetString();
    }
    return null;
}

static async Task GetCommand(string localWorkspaceFolder, string containerWorkspaceFolder, bool debug)
{
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
    {
        localWorkspaceFolder = await TryConvertWslPath(localWorkspaceFolder);
    }
    string hexPath = Convert.ToHexStringLower(Encoding.UTF8.GetBytes(localWorkspaceFolder));
    string folderUri = $"vscode-remote://dev-container+{hexPath}${containerWorkspaceFolder}";
    if (debug)
    {
        Console.Error.WriteLine($"Path: {localWorkspaceFolder}");
        Console.Error.WriteLine($"Folder URI: {folderUri}");
    }
    await Subprocess.Run("code", ["--folder-uri", folderUri], shell: true);
}

var rootCommand = new RootCommand("Open in Dev Container")
{
    Arguments = { Arguments.Workspace },
    Options = { Options.Debug },
    CommandAction = async (parseResult, token) =>
    {
        var workspace = parseResult.GetRequiredValue(Arguments.Workspace);
        var debug = parseResult.GetValue(Options.Debug);

        string localWorkspaceFolder = workspace.FullName;

        string? devcontainerConfig = await TryGetDevContainerConfig(localWorkspaceFolder, token);
        if (devcontainerConfig is null)
        {
            Console.Error.WriteLine($"devcontainer config not found in {localWorkspaceFolder}");
            return 1;
        }
        string? containerWorkspaceFolder = TryGetWorkspaceFolder(devcontainerConfig);
        if (containerWorkspaceFolder is null)
        {
            Console.Error.WriteLine($"workspaceFolder not found in devcontainer config");
            return 1;
        }
        if (containerWorkspaceFolder.Contains("${"))
        {
            Console.Error.WriteLine(
                $"Variable substitution in workspaceFolder is not supported: {containerWorkspaceFolder}"
            );
            return 1;
        }

        await GetCommand(localWorkspaceFolder, containerWorkspaceFolder, debug);
        return 0;
    },
};

await rootCommand.Parse(args).InvokeAsync();
