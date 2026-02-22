using System.CommandLine;

static class CommandExtensions
{
    extension(Command command)
    {
        public Func<ParseResult, CancellationToken, Task<int>> CommandAction
        {
            set => command.SetAction(value);
        }
    }
}
