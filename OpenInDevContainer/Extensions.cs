using System.CommandLine;

static class CommandExtensions
{
    extension(Command command)
    {
        public Func<ParseResult, CancellationToken, Task> CommandAction
        {
            set => command.SetAction(value);
        }
    }
}
