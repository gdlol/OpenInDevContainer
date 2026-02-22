using Cake.Common.Diagnostics;
using Cake.Common.IO;
using Cake.Common.Tools.Command;
using Cake.Common.Tools.DotNet;
using Cake.Core;
using Cake.Core.IO;
using Cake.Frosting;
using Microsoft.Build.Construction;
using NuGet.Versioning;
using Git = LibGit2Sharp;
using Path = System.IO.Path;

namespace Automation;

public class Publish : FrostingTask<Context>
{
    private const string NUGET_API_KEY = nameof(NUGET_API_KEY);
    private const string NUGET_SOURCE = nameof(NUGET_SOURCE);

    public static string NugetSource =>
        Environment.GetEnvironmentVariable(NUGET_SOURCE) ?? "https://api.nuget.org/v3/index.json";

    public override void Run(Context context)
    {
        string? apiKey = Environment.GetEnvironmentVariable(NUGET_API_KEY);
        string source;
        bool skipDuplicate;
        if (string.IsNullOrEmpty(apiKey))
        {
            source = Context.PackagePushPath;
            skipDuplicate = false;
            context.CreateDirectory(source);
            context.Information($"{NUGET_API_KEY} environment variable is not set. Pushing to {source}.");
        }
        else
        {
            source = NugetSource;
            skipDuplicate = true;
            context.Information($"Pushing to {source} with API key.");
        }
        foreach (string package in Directory.GetFiles(Context.PackageOutputPath))
        {
            context.DotNetNuGetPush(
                package,
                new()
                {
                    ApiKey = apiKey,
                    Source = source,
                    SkipDuplicate = skipDuplicate,
                }
            );
        }
    }
}

public class Unlist : FrostingTask<Context>
{
    const string NUGET_API_KEY = nameof(NUGET_API_KEY);

    public override void Run(Context context)
    {
        using var repo = new Git.Repository(Context.ProjectRoot);
        string[] tags = [.. repo.Tags.Select(t => t.FriendlyName)];
        string tag = tags.OrderByDescending(SemanticVersion.Parse).First();

        string projectFilePath = Context.OpenInDevContainerProjectFilePath;
        var project = ProjectRootElement.Open(projectFilePath);
        var runtimeIdentifiers = project
            .PropertyGroups.SelectMany(g => g.Properties)
            .Where(p => p.Name == "RuntimeIdentifiers")
            .SelectMany(p => p.Value.Split(';', StringSplitOptions.RemoveEmptyEntries))
            .ToArray();
        context.Information(
            $"Deleting version {tag} with runtime identifiers: {string.Join(", ", runtimeIdentifiers)}"
        );
        string apiKey =
            Environment.GetEnvironmentVariable(NUGET_API_KEY)
            ?? throw new InvalidOperationException($"{NUGET_API_KEY} environment variable is not set.");
        context.DotNetNuGetDelete(
            "oid",
            tag,
            new()
            {
                ApiKey = apiKey,
                Source = Publish.NugetSource,
                NonInteractive = true,
            }
        );
        foreach (string runtimeIdentifier in runtimeIdentifiers)
        {
            context.DotNetNuGetDelete(
                $"oid-rid.{runtimeIdentifier}",
                tag,
                new()
                {
                    ApiKey = apiKey,
                    Source = Publish.NugetSource,
                    NonInteractive = true,
                }
            );
        }
    }
}

public class PublishDocker : FrostingTask<Context>
{
    public const string CR_REGISTRY = nameof(CR_REGISTRY);
    public const string CR_IMAGE_NAME = nameof(CR_IMAGE_NAME);
    public const string CR_VERSION = nameof(CR_VERSION);
    public const string CR_PAT = nameof(CR_PAT);

    private static void EnableBuildx(Context context)
    {
        string builderName = "oid";
        bool create = false;
        try
        {
            context.Command(["docker"], arguments: ProcessArgumentBuilder.FromStrings(["buildx", "use", builderName]));
        }
        catch (Exception)
        {
            create = true;
        }
        if (create)
        {
            context.Command(
                ["docker"],
                arguments: ProcessArgumentBuilder.FromStrings([
                    "buildx",
                    "create",
                    "--name",
                    builderName,
                    "--use",
                    "--bootstrap",
                    "--driver-opt",
                    "network=host",
                ])
            );
        }
    }

    public override void Run(Context context)
    {
        string registry = Environment.GetEnvironmentVariable(CR_REGISTRY) ?? "ghcr.io";
        string imageName =
            Environment.GetEnvironmentVariable(CR_IMAGE_NAME)
            ?? throw new InvalidOperationException($"{CR_IMAGE_NAME} is not set.");
        string version =
            Environment.GetEnvironmentVariable(CR_VERSION)
            ?? throw new InvalidOperationException($"{CR_VERSION} is not set.");
        string token =
            Environment.GetEnvironmentVariable(CR_PAT) ?? throw new InvalidOperationException($"{CR_PAT} is not set.");

        context.Information($"Logging in to {registry}...");
        context.Command(
            ["docker"],
            new ProcessArgumentBuilder()
                .Append("login")
                .Append(registry)
                .AppendSwitch("--username", "USERNAME")
                .AppendSwitchQuotedSecret("--password", token)
        );

        string[] platforms =
        [
            "darwin/amd64",
            "darwin/arm64",
            "linux/amd64",
            "linux/arm64",
            "windows/amd64",
            "windows/arm64",
        ];

        string[] tags = [$@"{registry}/{imageName}:{version}", $@"{registry}/{imageName}:latest"];

        context.Information("Building and pushing multi-arch image...");
        EnableBuildx(context);
        context.Command(
            ["docker"],
            ProcessArgumentBuilder.FromStrings([
                "buildx",
                "build",
                "--file",
                Path.Combine(Context.ProjectRoot, "Docker/Dockerfile"),
                "--platform",
                string.Join(',', platforms),
                "--tag",
                tags[0],
                "--tag",
                tags[1],
                "--push",
                Context.ProjectRoot,
            ])
        );
    }
}
