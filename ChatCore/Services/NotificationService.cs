using ChatCore.Observers;

namespace ChatCore.Services
{
    public class NotificationService : 
        ISubject<PresenceNotification>,
        ISubject<TypingNotification>
    {
        private readonly List<IChatObserver<PresenceNotification>> presenceObservers = new();
        private readonly List<IChatObserver<TypingNotification>> typingObservers = new();
        private readonly object presenceLock = new();
        private readonly object typingLock = new();

        public void Attach(IChatObserver<PresenceNotification> observer)
        {
            lock (presenceLock)
            {
                if (!presenceObservers.Contains(observer))
                {
                    presenceObservers.Add(observer);
                }
            }
        }

        public void Detach(IChatObserver<PresenceNotification> observer)
        {
            lock (presenceLock)
            {
                presenceObservers.Remove(observer);
            }
        }

        public async Task NotifyAsync(PresenceNotification data)
        {
            List<IChatObserver<PresenceNotification>> observers;
            lock (presenceLock)
            {
                observers = new List<IChatObserver<PresenceNotification>>(presenceObservers);
            }

            foreach (var observer in observers)
            {
                await observer.UpdateAsync(data);
            }
        }

        public void Attach(IChatObserver<TypingNotification> observer)
        {
            lock (typingLock)
            {
                if (!typingObservers.Contains(observer))
                {
                    typingObservers.Add(observer);
                }
            }
        }

        public void Detach(IChatObserver<TypingNotification> observer)
        {
            lock (typingLock)
            {
                typingObservers.Remove(observer);
            }
        }

        public async Task NotifyAsync(TypingNotification data)
        {
            List<IChatObserver<TypingNotification>> observers;
            lock (typingLock)
            {
                observers = new List<IChatObserver<TypingNotification>>(typingObservers);
            }

            foreach (var observer in observers)
            {
                await observer.UpdateAsync(data);
            }
        }
    }
}
