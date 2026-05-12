using System.Net;

public sealed class AuthenticationRateLimiter
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(2);
    private readonly Dictionary<string, FailedAttemptState> attempts = new(StringComparer.OrdinalIgnoreCase);
    private readonly object syncRoot = new();

    public bool IsBlocked(IPEndPoint remoteEndpoint, string username, out TimeSpan retryAfter)
    {
        string key = GetKey(remoteEndpoint, username);
        lock (syncRoot)
        {
            if (!attempts.TryGetValue(key, out FailedAttemptState? state) || state.BlockedUntil <= DateTime.UtcNow)
            {
                retryAfter = TimeSpan.Zero;
                return false;
            }

            retryAfter = state.BlockedUntil - DateTime.UtcNow;
            return true;
        }
    }

    public void RecordFailure(IPEndPoint remoteEndpoint, string username)
    {
        string key = GetKey(remoteEndpoint, username);
        lock (syncRoot)
        {
            attempts.TryGetValue(key, out FailedAttemptState? state);
            state ??= new FailedAttemptState();
            state.FailedAttempts++;

            if (state.FailedAttempts >= MaxFailedAttempts)
            {
                state.BlockedUntil = DateTime.UtcNow.Add(LockoutDuration);
            }

            attempts[key] = state;
        }
    }

    public void RecordSuccess(IPEndPoint remoteEndpoint, string username)
    {
        string key = GetKey(remoteEndpoint, username);
        lock (syncRoot)
        {
            attempts.Remove(key);
        }
    }

    private static string GetKey(IPEndPoint remoteEndpoint, string username)
    {
        return $"{remoteEndpoint.Address}|{username.Trim().ToLowerInvariant()}";
    }

    private sealed class FailedAttemptState
    {
        public int FailedAttempts { get; set; }
        public DateTime BlockedUntil { get; set; }
    }
}
