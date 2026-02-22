using Cake.Frosting;
using Git = LibGit2Sharp;

namespace Automation;

public class Push : FrostingTask<Context>
{
    private const string GIT_REMOTE_TOKEN = nameof(GIT_REMOTE_TOKEN);

    public override void Run(Context context)
    {
        using var repo = new Git.Repository(Context.ProjectRoot);

        using var remote = repo.Network.Remotes.First();

        string token =
            Environment.GetEnvironmentVariable(GIT_REMOTE_TOKEN)
            ?? throw new InvalidOperationException($"{GIT_REMOTE_TOKEN} environment variable is not set");

        var options = new Git.PushOptions
        {
            CredentialsProvider = (url, usernameFromUrl, types) =>
                new Git.UsernamePasswordCredentials { Username = "git", Password = token },
        };
        repo.Network.Push(remote, pushRefSpec: $"+refs/heads/{repo.Head.FriendlyName}", options);
    }
}
