using System.CommandLine;

static class Options
{
    public static Option<bool> Debug { get; } = new Option<bool>(name: "--debug") { Description = "Enable debug logs" };
}
