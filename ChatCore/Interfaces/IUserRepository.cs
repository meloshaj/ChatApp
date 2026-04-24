using ChatCore.Models;

namespace ChatCore.Interfaces
{
    public interface IUserRepository : IRepository<User>
    {
        Task<User?> GetByUsernameAsync(string username);
        Task<bool> IsUsernameAvailableAsync(string username, string? excludedUserId = null);
        Task<IEnumerable<User>> GetOnlineUsersAsync();
    }
}
