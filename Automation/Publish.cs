using Cake.Common.Diagnostics;
using Cake.Common.IO;
using Cake.Common.Tools.DotNet;
using Cake.Frosting;
using Microsoft.Build.Construction;
using NuGet.Versioning;
using Git = LibGit2Sharp;

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

        string projectFilePath = Path.Combine(Context.ProjectRoot, "OpenInDevContainer/OpenInDevContainer.csproj");
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
