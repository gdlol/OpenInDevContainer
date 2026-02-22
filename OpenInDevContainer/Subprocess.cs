using System.ComponentModel;
using System.Diagnostics;

[Serializable]
class SubprocessException : Win32Exception
{
    public SubprocessException(int error)
        : base(error) { }

    public SubprocessException(int error, string? message)
        : base(error, message) { }
}

static class Subprocess
{
    public static async Task<string> Run(
        string fileName,
        IEnumerable<string> arguments,
        bool shell = false,
        bool captureOutput = false,
        CancellationToken cancellationToken = default
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
            output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        }
        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0)
        {
            throw new SubprocessException(process.ExitCode);
        }
        return output;
    }
}
