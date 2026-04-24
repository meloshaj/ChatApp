namespace ChatCore.Observers
{
    public interface IChatObserver<T>
    {
        Task UpdateAsync(T data);
    }
}
