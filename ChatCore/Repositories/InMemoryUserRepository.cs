using ChatCore.Interfaces;
using ChatCore.Models;

namespace ChatCore.Repositories
{
    public class InMemoryUserRepository : IUserRepository
    {
        private readonly Dictionary<string, User> users = new();
        private readonly object lockObject = new();

        public Task<User?> GetByIdAsync(string id)
        {
            lock (lockObject)
            {
                users.TryGetValue(id, out var user);
                return Task.FromResult(user);
            }
        }

        public Task<IEnumerable<User>> GetAllAsync()
        {
            lock (lockObject)
            {
                return Task.FromResult<IEnumerable<User>>(users.Values.ToList());
            }
        }

        public Task AddAsync(User entity)
        {
            lock (lockObject)
            {
                users[entity.Id] = entity;
            }
            return Task.CompletedTask;
        }

        public Task UpdateAsync(User entity)
        {
            lock (lockObject)
            {
                users[entity.Id] = entity;
            }
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string id)
        {
            lock (lockObject)
            {
                users.Remove(id);
            }
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(string id)
        {
            lock (lockObject)
            {
                return Task.FromResult(users.ContainsKey(id));
            }
        }

        public Task<User?> GetByUsernameAsync(string username)
        {
            lock (lockObject)
            {
                var user = users.Values.FirstOrDefault(u => 
                    string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));
                return Task.FromResult(user);
            }
        }

        public Task<bool> IsUsernameAvailableAsync(string username, string? excludedUserId = null)
        {
            lock (lockObject)
            {
                var isAvailable = users.Values.All(u =>
                    u.Id == excludedUserId ||
                    !string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));
                return Task.FromResult(isAvailable);
            }
        }

        public Task<IEnumerable<User>> GetOnlineUsersAsync()
        {
            lock (lockObject)
            {
                var onlineUsers = users.Values
                    .Where(u => u.Status == UserStatus.Online)
                    .ToList();
                return Task.FromResult<IEnumerable<User>>(onlineUsers);
            }
        }
    }
}
