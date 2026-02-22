using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using Cake.Common.IO;
using Cake.Common.Tools.Command;
using Cake.Common.Tools.DotNet;
using Cake.Common.Tools.DotNet.MSBuild;
using Cake.Core.IO;
using Cake.Frosting;
using NuGet.Packaging;
using Path = System.IO.Path;

namespace Automation;

public class Pack : AsyncFrostingTask<Context>
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
                ["PackageLicenseExpression"] = ["LGPL-2.1-only"],
                ["PackageRequireLicenseAcceptance"] = ["true"],
                ["PackageTags"] = ["open folder devcontainer vscode"],
            },
        };
        if (!string.IsNullOrEmpty(runtimeIdentifier))
        {
            msbuildSettings.Properties["PackageId"] = ["oid-rid"];
            msbuildSettings.Properties["RuntimeIdentifier"] = [runtimeIdentifier];
        }
        context.DotNetPack(
            Path.Combine(Context.ProjectRoot, "OpenInDevContainer"),
            new() { MSBuildSettings = msbuildSettings }
        );
    }

    private static void UpdatePackageNames()
    {
        var mainPackage = Directory.EnumerateFiles(Context.PackageOutputPath, "oid.*.nupkg").Single();

        // Locate DotnetToolSettings.xml
        using var reader = new PackageArchiveReader(mainPackage);
        var dotnetToolSettingsFile = reader
            .GetFiles()
            .Single(f => f.EndsWith("DotnetToolSettings.xml", StringComparison.OrdinalIgnoreCase));
        using var settingsStream = reader.GetStream(dotnetToolSettingsFile);
        var doc = XDocument.Load(settingsStream);
        reader.Dispose();

        var ns = doc.Root!.Name.Namespace;
        var packageNodes =
            doc.Root.Element(ns + "RuntimeIdentifierPackages")?.Elements(ns + "RuntimeIdentifierPackage")
            ?? throw new InvalidOperationException();
        foreach (var package in packageNodes)
        {
            string rid = package.Attribute("RuntimeIdentifier")?.Value ?? throw new InvalidOperationException();
            package.SetAttributeValue("Id", $"oid-rid.{rid}");
        }

        // Update the DotnetToolSettings.xml in the nupkg
        using var archive = ZipFile.Open(mainPackage, ZipArchiveMode.Update);
        var entry = archive.GetEntry(dotnetToolSettingsFile) ?? throw new InvalidOperationException();
        using var writer = entry.Open();
        writer.SetLength(0);
        doc.Save(writer);
    }

    public override async Task RunAsync(Context context)
    {
        context.CleanDirectory(Context.PackageOutputPath);

        await RunAsync(context, "any");
        await RunAsync(context);
        UpdatePackageNames();
    }
}

public class PackRid : AsyncFrostingTask<Context>
{
    public override async Task RunAsync(Context context)
    {
        context.CleanDirectory(Context.PackageOutputPath);

        var osPlatform =
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? nameof(OSPlatform.Linux)
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? nameof(OSPlatform.OSX)
            : RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? nameof(OSPlatform.Windows)
            : throw new PlatformNotSupportedException(RuntimeInformation.OSDescription);

        switch (osPlatform, RuntimeInformation.ProcessArchitecture)
        {
            case (nameof(OSPlatform.Linux), Architecture.Arm64):
                await Pack.RunAsync(context, "linux-arm64");
                break;
            case (nameof(OSPlatform.Linux), Architecture.X64):
                await Pack.RunAsync(context, "linux-x64");
                break;
            case (nameof(OSPlatform.OSX), Architecture.Arm64):
                await Pack.RunAsync(context, "osx-arm64");
                break;
            case (nameof(OSPlatform.OSX), Architecture.X64):
                await Pack.RunAsync(context, "osx-x64");
                break;
            case (nameof(OSPlatform.Windows), Architecture.Arm64):
                await Pack.RunAsync(context, "win-arm64");
                break;
            case (nameof(OSPlatform.Windows), Architecture.X64):
                await Pack.RunAsync(context, "win-x64");
                break;
            default:
                throw new PlatformNotSupportedException($"{osPlatform}-{RuntimeInformation.ProcessArchitecture}");
        }
    }
}
