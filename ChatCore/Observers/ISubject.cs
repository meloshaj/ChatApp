namespace ChatCore.Observers
{
    public interface ISubject<T>
    {
        void Attach(IChatObserver<T> observer);
        void Detach(IChatObserver<T> observer);
        Task NotifyAsync(T data);
    }
}
