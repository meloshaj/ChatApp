using System.Security.Cryptography;
using System.Text;
using ChatCore.Interfaces;
using ChatCore.Models;

namespace ChatCore.Services
{
    public class AuthenticationService
    {
        private readonly IUserRepository userRepository;

        public AuthenticationService(IUserRepository userRepository)
        {
            this.userRepository = userRepository;
        }

        public async Task<(bool Success, string? UserId, string? Error)> RegisterAsync(
            string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                return (false, null, "Username and password are required.");
            }

            if (username.Contains(' '))
            {
                return (false, null, "Username cannot contain spaces.");
            }

            if (password.Length < 6)
            {
                return (false, null, "Password must be at least 6 characters.");
            }

            var existingUser = await userRepository.GetByUsernameAsync(username);
            if (existingUser != null)
            {
                return (false, null, "Username already exists.");
            }

            var user = new User
            {
                Id = Guid.NewGuid().ToString(),
                Username = username,
                PasswordHash = HashPassword(password),
                Status = UserStatus.Offline
            };

            await userRepository.AddAsync(user);

            return (true, user.Id, null);
        }

        public async Task<(bool Success, string? UserId, string? Error)> LoginAsync(
            string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                return (false, null, "Username and password are required.");
            }

            var user = await userRepository.GetByUsernameAsync(username);
            if (user == null)
            {
                return (false, null, "Invalid username or password.");
            }

            if (!VerifyPassword(password, user.PasswordHash))
            {
                return (false, null, "Invalid username or password.");
            }

            return (true, user.Id, null);
        }

        private static string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var saltedPassword = password + "ChatAppSalt2024"; // In production, use unique salt per user
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(saltedPassword));
            return Convert.ToBase64String(bytes);
        }

        private static bool VerifyPassword(string password, string hash)
        {
            var computedHash = HashPassword(password);
            return computedHash == hash;
        }

    }
}
