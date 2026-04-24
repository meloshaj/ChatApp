namespace ChatCore.Commands
{
    public class CommandHistory
    {
        private readonly Stack<ICommand> executedCommands = new();
        private readonly int maxHistorySize;

        public CommandHistory(int maxHistorySize = 50)
        {
            this.maxHistorySize = maxHistorySize;
        }

        public async Task ExecuteAsync(ICommand command)
        {
            await command.ExecuteAsync();
            executedCommands.Push(command);

            if (executedCommands.Count > maxHistorySize)
            {
                var commands = executedCommands.ToList();
                commands.RemoveAt(commands.Count - 1);
                executedCommands.Clear();
                foreach (var cmd in commands.AsEnumerable().Reverse())
                {
                    executedCommands.Push(cmd);
                }
            }
        }

        public async Task<bool> UndoAsync()
        {
            if (executedCommands.Count == 0)
                return false;

            var command = executedCommands.Pop();
            await command.UndoAsync();
            return true;
        }

        public bool CanUndo() => executedCommands.Count > 0;

        public string? GetLastCommandDescription()
        {
            return executedCommands.Count > 0 ? executedCommands.Peek().GetDescription() : null;
        }
    }
}
