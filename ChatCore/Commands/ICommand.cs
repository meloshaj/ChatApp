namespace ChatCore.Commands
{
    public interface ICommand
    {
        Task ExecuteAsync();
        Task UndoAsync();
        string GetDescription();
    }
}
