using System.CommandLine;

static class Arguments
{
    public static Argument<DirectoryInfo> Workspace { get; } =
        new Argument<DirectoryInfo>(name: "workspace")
        {
            Description = "The workspace to open, defaults to the current directory",
            DefaultValueFactory = _ => new DirectoryInfo(Environment.CurrentDirectory),
        };
}
