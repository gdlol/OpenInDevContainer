using Cake.Frosting;
using NuGet.Versioning;
using Git = LibGit2Sharp;

namespace Automation;

public class Push : FrostingTask<Context>
{
    private const string GIT_REMOTE_TOKEN = nameof(GIT_REMOTE_TOKEN);

    public static string GitRemoteToken
    {
        get =>
            Environment.GetEnvironmentVariable(GIT_REMOTE_TOKEN)
            ?? throw new InvalidOperationException($"{GIT_REMOTE_TOKEN} environment variable is not set");
    }

    public static Git.PushOptions Options =>
        new()
        {
            CredentialsProvider = (url, usernameFromUrl, types) =>
                new Git.UsernamePasswordCredentials { Username = "git", Password = GitRemoteToken },
        };

    public override void Run(Context context)
    {
        using var repo = new Git.Repository(Context.ProjectRoot);
        using var remote = repo.Network.Remotes.First();
        repo.Network.Push(remote, pushRefSpec: $"+refs/heads/{repo.Head.FriendlyName}", Options);
    }
}

public class PushTag : FrostingTask<Context>
{
    public override void Run(Context context)
    {
        using var repo = new Git.Repository(Context.ProjectRoot);
        string[] tags = [.. repo.Tags.Select(t => t.FriendlyName)];
        string tag = tags.OrderByDescending(SemanticVersion.Parse).First();
        using var remote = repo.Network.Remotes.First();
        repo.Network.Push(remote, pushRefSpec: $"+refs/tags/{tag}", Push.Options);
    }
}
