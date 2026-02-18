using System.IO.Compression;
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
            msbuildSettings.Properties["PackageId"] = ["oid-rid"];
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
    private static void UpdatePackageNames()
    {
        var packages = Directory.EnumerateFiles(Context.PackageOutputPath, "oid*.nupkg").ToList();
        var mainPackage = packages.Single(f => !Path.GetFileName(f).Contains("-rid."));

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

        await Pack.RunAsync(context, "linux-x64");
        await Pack.RunAsync(context, "any");
        await Pack.RunAsync(context);
        UpdatePackageNames();
    }
}
