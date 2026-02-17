using Cake.Common.Diagnostics;
using Cake.Common.IO;
using Cake.Common.Tools.DotNet;
using Cake.Frosting;

namespace Automation;

public class Publish : FrostingTask<Context>
{
    private const string NUGET_API_KEY = nameof(NUGET_API_KEY);
    private const string NUGET_SOURCE = nameof(NUGET_SOURCE);

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
            source = Environment.GetEnvironmentVariable(NUGET_SOURCE) ?? "https://api.nuget.org/v3/index.json";
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
