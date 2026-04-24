using System.Net.Sockets;
using ChatCore.Interfaces;
using ChatCore.Models;
using ChatCore.Persistence;
using ChatCore.States;
using Microsoft.Data.Sqlite;

namespace ChatCore.Repositories
{
    public sealed class SqliteUserRepository : IUserRepository
    {
        private readonly SqliteDatabase database;
        private readonly Dictionary<string, RuntimeUserState> runtimeUsers = new();
        private readonly object runtimeLock = new();

        public SqliteUserRepository(SqliteDatabase database)
        {
            this.database = database;
        }

        public async Task<User?> GetByIdAsync(string id)
        {
            await using var connection = database.CreateConnection();
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT id, username, password_hash, active_conversation_id, status, last_seen, is_typing, typing_in_conversation_id
                FROM users
                WHERE id = $id;
                """;
            command.Parameters.AddWithValue("$id", id);

            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return null;
            }

            User user = await ReadUserAsync(connection, reader);
            AttachRuntimeState(user);
            return user;
        }

        public async Task<IEnumerable<User>> GetAllAsync()
        {
            List<User> users = [];

            await using var connection = database.CreateConnection();
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT id, username, password_hash, active_conversation_id, status, last_seen, is_typing, typing_in_conversation_id
                FROM users
                ORDER BY username;
                """;

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                User user = await ReadUserAsync(connection, reader);
                AttachRuntimeState(user);
                users.Add(user);
            }

            return users;
        }

        public async Task AddAsync(User entity)
        {
            await using var connection = database.CreateConnection();
            await connection.OpenAsync();
            using SqliteTransaction transaction = connection.BeginTransaction();

            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
                    INSERT INTO users (id, username, password_hash, active_conversation_id, status, last_seen, is_typing, typing_in_conversation_id)
                    VALUES ($id, $username, $passwordHash, $activeConversationId, $status, $lastSeen, $isTyping, $typingInConversationId);
                    """;
                FillUserParameters(command, entity);
                await command.ExecuteNonQueryAsync();
            }

            await ReplaceFriendshipsAsync(connection, transaction, entity);
            await transaction.CommitAsync();
            UpdateRuntimeState(entity);
        }

        public async Task UpdateAsync(User entity)
        {
            await using var connection = database.CreateConnection();
            await connection.OpenAsync();
            using SqliteTransaction transaction = connection.BeginTransaction();

            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
                    UPDATE users
                    SET username = $username,
                        password_hash = $passwordHash,
                        active_conversation_id = $activeConversationId,
                        status = $status,
                        last_seen = $lastSeen,
                        is_typing = $isTyping,
                        typing_in_conversation_id = $typingInConversationId
                    WHERE id = $id;
                    """;
                FillUserParameters(command, entity);
                await command.ExecuteNonQueryAsync();
            }

            await ReplaceFriendshipsAsync(connection, transaction, entity);
            await transaction.CommitAsync();
            UpdateRuntimeState(entity);
        }

        public async Task DeleteAsync(string id)
        {
            await using var connection = database.CreateConnection();
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM users WHERE id = $id;";
            command.Parameters.AddWithValue("$id", id);
            await command.ExecuteNonQueryAsync();

            lock (runtimeLock)
            {
                runtimeUsers.Remove(id);
            }
        }

        public async Task<bool> ExistsAsync(string id)
        {
            await using var connection = database.CreateConnection();
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT EXISTS(SELECT 1 FROM users WHERE id = $id);";
            command.Parameters.AddWithValue("$id", id);
            return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
        }

        public async Task<User?> GetByUsernameAsync(string username)
        {
            await using var connection = database.CreateConnection();
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT id, username, password_hash, active_conversation_id, status, last_seen, is_typing, typing_in_conversation_id
                FROM users
                WHERE username = $username COLLATE NOCASE;
                """;
            command.Parameters.AddWithValue("$username", username);

            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return null;
            }

            User user = await ReadUserAsync(connection, reader);
            AttachRuntimeState(user);
            return user;
        }

        public async Task<bool> IsUsernameAvailableAsync(string username, string? excludedUserId = null)
        {
            await using var connection = database.CreateConnection();
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT COUNT(*)
                FROM users
                WHERE username = $username COLLATE NOCASE
                  AND ($excludedUserId IS NULL OR id <> $excludedUserId);
                """;
            command.Parameters.AddWithValue("$username", username);
            command.Parameters.AddWithValue("$excludedUserId", (object?)excludedUserId ?? DBNull.Value);
            long count = (long)(await command.ExecuteScalarAsync() ?? 0L);
            return count == 0;
        }

        public async Task<IEnumerable<User>> GetOnlineUsersAsync()
        {
            List<User> users = [];

            await using var connection = database.CreateConnection();
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT id, username, password_hash, active_conversation_id, status, last_seen, is_typing, typing_in_conversation_id
                FROM users
                WHERE status = $status;
                """;
            command.Parameters.AddWithValue("$status", (int)UserStatus.Online);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                User user = await ReadUserAsync(connection, reader);
                AttachRuntimeState(user);
                users.Add(user);
            }

            return users.Where(user => user.Client != null).ToList();
        }

        public async Task ResetPresenceAsync()
        {
            await using var connection = database.CreateConnection();
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE users
                SET status = $offlineStatus,
                    is_typing = 0,
                    typing_in_conversation_id = NULL,
                    last_seen = $lastSeen;
                """;
            command.Parameters.AddWithValue("$offlineStatus", (int)UserStatus.Offline);
            command.Parameters.AddWithValue("$lastSeen", DateTime.UtcNow.ToString("O"));
            await command.ExecuteNonQueryAsync();

            lock (runtimeLock)
            {
                runtimeUsers.Clear();
            }
        }

        private static void FillUserParameters(SqliteCommand command, User entity)
        {
            command.Parameters.AddWithValue("$id", entity.Id);
            command.Parameters.AddWithValue("$username", entity.Username);
            command.Parameters.AddWithValue("$passwordHash", entity.PasswordHash);
            command.Parameters.AddWithValue("$activeConversationId", (object?)entity.ActiveConversationId ?? DBNull.Value);
            command.Parameters.AddWithValue("$status", (int)entity.Status);
            command.Parameters.AddWithValue("$lastSeen", entity.LastSeen.ToString("O"));
            command.Parameters.AddWithValue("$isTyping", entity.IsTyping ? 1 : 0);
            command.Parameters.AddWithValue("$typingInConversationId", (object?)entity.TypingInConversationId ?? DBNull.Value);
        }

        private async Task ReplaceFriendshipsAsync(SqliteConnection connection, SqliteTransaction transaction, User entity)
        {
            await using (var deleteCommand = connection.CreateCommand())
            {
                deleteCommand.Transaction = transaction;
                deleteCommand.CommandText = "DELETE FROM friendships WHERE user_id = $userId;";
                deleteCommand.Parameters.AddWithValue("$userId", entity.Id);
                await deleteCommand.ExecuteNonQueryAsync();
            }

            foreach (string friendId in entity.FriendIds)
            {
                await using var insertCommand = connection.CreateCommand();
                insertCommand.Transaction = transaction;
                insertCommand.CommandText = """
                    INSERT OR IGNORE INTO friendships (user_id, friend_id)
                    VALUES ($userId, $friendId);
                    """;
                insertCommand.Parameters.AddWithValue("$userId", entity.Id);
                insertCommand.Parameters.AddWithValue("$friendId", friendId);
                await insertCommand.ExecuteNonQueryAsync();
            }
        }

        private async Task<User> ReadUserAsync(SqliteConnection connection, SqliteDataReader reader)
        {
            User user = new()
            {
                Id = reader.GetString(0),
                Username = reader.GetString(1),
                PasswordHash = reader.GetString(2),
                ActiveConversationId = reader.IsDBNull(3) ? null : reader.GetString(3),
                Status = (UserStatus)reader.GetInt32(4),
                LastSeen = DateTime.Parse(reader.GetString(5), null, System.Globalization.DateTimeStyles.RoundtripKind),
                IsTyping = reader.GetInt32(6) == 1,
                TypingInConversationId = reader.IsDBNull(7) ? null : reader.GetString(7)
            };

            user.SetState(CreateState(user.Status));

            await using var friendsCommand = connection.CreateCommand();
            friendsCommand.CommandText = "SELECT friend_id FROM friendships WHERE user_id = $userId;";
            friendsCommand.Parameters.AddWithValue("$userId", user.Id);
            await using var friendsReader = await friendsCommand.ExecuteReaderAsync();
            while (await friendsReader.ReadAsync())
            {
                user.FriendIds.Add(friendsReader.GetString(0));
            }

            return user;
        }

        private void AttachRuntimeState(User user)
        {
            lock (runtimeLock)
            {
                if (runtimeUsers.TryGetValue(user.Id, out RuntimeUserState? runtimeState))
                {
                    user.Client = runtimeState.Client;
                    user.SetState(runtimeState.State);
                    user.Status = runtimeState.State.Status;
                    return;
                }
            }

            user.SetState(CreateState(user.Status));
        }

        private void UpdateRuntimeState(User user)
        {
            lock (runtimeLock)
            {
                if (user.Client == null)
                {
                    runtimeUsers.Remove(user.Id);
                    return;
                }

                runtimeUsers[user.Id] = new RuntimeUserState(user.Client, user.GetState());
            }
        }

        private static ChatCore.States.IUserState CreateState(UserStatus status)
        {
            return status switch
            {
                UserStatus.Online => new OnlineState(),
                UserStatus.Away => new AwayState(),
                UserStatus.Busy => new BusyState(),
                _ => new OfflineState()
            };
        }

        private sealed record RuntimeUserState(TcpClient Client, ChatCore.States.IUserState State);
    }
}
