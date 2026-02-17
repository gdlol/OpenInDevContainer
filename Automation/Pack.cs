using Cake.Common.IO;
using Cake.Common.Tools.Command;
using Cake.Common.Tools.DotNet;
using Cake.Common.Tools.DotNet.MSBuild;
using Cake.Core.IO;
using Cake.Frosting;
using Path = System.IO.Path;

namespace Automation;

public static class Pack
{
    public static async Task RunAsync(Context context, string? runtimeIdentifier = null)
    {
        string? authors = Environment.GetEnvironmentVariable("AUTHORS");
        if (string.IsNullOrEmpty(authors))
        {
            context.Command(
                ["git"],
                out authors,
                arguments: ProcessArgumentBuilder.FromStrings(["config", "user.name"])
            );
            authors = authors.Trim();
        }
        var msbuildSettings = new DotNetMSBuildSettings
        {
            Properties =
            {
                ["PublishAot"] = [runtimeIdentifier == "any" ? "false" : "true"],
                ["ReadMePath"] = [Path.Combine(Context.ProjectRoot, "ReadMe.md")],
                ["PackageOutputPath"] = [Context.PackageOutputPath],
                ["Authors"] = [authors],
                ["PackageReadmeFile"] = ["ReadMe.md"],
                ["PackageDescription"] = ["Open folder in dev container."],
                ["PackageLicenseExpression"] = ["LGPL-3.0-only"],
                ["PackageRequireLicenseAcceptance"] = ["true"],
                ["PackageTags"] = ["open folder devcontainer vscode"],
            },
        };
        if (!string.IsNullOrEmpty(runtimeIdentifier))
        {
            msbuildSettings.Properties["RuntimeIdentifier"] = [runtimeIdentifier];
        }
        context.DotNetPack(
            Path.Combine(Context.ProjectRoot, "OpenInDevContainer"),
            new() { MSBuildSettings = msbuildSettings }
        );
    }
}

public class PackLinux : AsyncFrostingTask<Context>
{
    public override async Task RunAsync(Context context)
    {
        context.CleanDirectory(Context.PackageOutputPath);

        await Pack.RunAsync(context, "linux-x64");
        await Pack.RunAsync(context, "any");
        await Pack.RunAsync(context);
    }
}
