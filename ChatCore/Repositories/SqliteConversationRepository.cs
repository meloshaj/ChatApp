using ChatCore.Interfaces;
using ChatCore.Models;
using ChatCore.Persistence;
using Microsoft.Data.Sqlite;

namespace ChatCore.Repositories
{
    public sealed class SqliteConversationRepository : IConversationRepository
    {
        private readonly SqliteDatabase database;

        public SqliteConversationRepository(SqliteDatabase database)
        {
            this.database = database;
        }

        public async Task<Conversation?> GetByIdAsync(string id)
        {
            await using var connection = database.CreateConnection();
            await connection.OpenAsync();
            return await GetConversationByConditionAsync(connection, "WHERE id = $value", id);
        }

        public async Task<IEnumerable<Conversation>> GetAllAsync()
        {
            List<Conversation> conversations = [];

            await using var connection = database.CreateConnection();
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT id, name, type, owner_id
                FROM conversations
                ORDER BY name;
                """;

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                conversations.Add(await ReadConversationAsync(connection, reader));
            }

            return conversations;
        }

        public async Task AddAsync(Conversation entity)
        {
            await UpsertAsync(entity, false);
        }

        public async Task UpdateAsync(Conversation entity)
        {
            await UpsertAsync(entity, true);
        }

        public async Task DeleteAsync(string id)
        {
            await using var connection = database.CreateConnection();
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM conversations WHERE id = $id;";
            command.Parameters.AddWithValue("$id", id);
            await command.ExecuteNonQueryAsync();
        }

        public async Task<bool> ExistsAsync(string id)
        {
            await using var connection = database.CreateConnection();
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT EXISTS(SELECT 1 FROM conversations WHERE id = $id);";
            command.Parameters.AddWithValue("$id", id);
            return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
        }

        public async Task<IEnumerable<Conversation>> GetUserConversationsAsync(string userId)
        {
            List<Conversation> conversations = [];

            await using var connection = database.CreateConnection();
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT c.id, c.name, c.type, c.owner_id
                FROM conversations c
                INNER JOIN conversation_participants cp ON cp.conversation_id = c.id
                WHERE cp.user_id = $userId
                ORDER BY c.name;
                """;
            command.Parameters.AddWithValue("$userId", userId);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                conversations.Add(await ReadConversationAsync(connection, reader));
            }

            return conversations;
        }

        public async Task<PrivateConversation?> GetPrivateConversationAsync(string userId1, string userId2)
        {
            await using var connection = database.CreateConnection();
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT c.id, c.name, c.type, c.owner_id
                FROM conversations c
                INNER JOIN conversation_participants cp1 ON cp1.conversation_id = c.id AND cp1.user_id = $userId1
                INNER JOIN conversation_participants cp2 ON cp2.conversation_id = c.id AND cp2.user_id = $userId2
                WHERE c.type = 'private';
                """;
            command.Parameters.AddWithValue("$userId1", userId1);
            command.Parameters.AddWithValue("$userId2", userId2);

            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return null;
            }

            return (PrivateConversation)await ReadConversationAsync(connection, reader);
        }

        public async Task<GroupConversation?> GetGroupByNameAsync(string groupName)
        {
            await using var connection = database.CreateConnection();
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT id, name, type, owner_id
                FROM conversations
                WHERE type = 'group' AND name = $groupName COLLATE NOCASE;
                """;
            command.Parameters.AddWithValue("$groupName", groupName);

            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return null;
            }

            return (GroupConversation)await ReadConversationAsync(connection, reader);
        }

        public async Task RecordUserJoinedAsync(string conversationId, string userId, DateTime timestamp)
        {
            await using var connection = database.CreateConnection();
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO membership_history (conversation_id, user_id, joined_at, left_at)
                VALUES ($conversationId, $userId, $joinedAt, NULL);
                """;
            command.Parameters.AddWithValue("$conversationId", conversationId);
            command.Parameters.AddWithValue("$userId", userId);
            command.Parameters.AddWithValue("$joinedAt", timestamp.ToString("o"));
            
            await command.ExecuteNonQueryAsync();
        }

        public async Task RecordUserLeftAsync(string conversationId, string userId, DateTime timestamp)
        {
            await using var connection = database.CreateConnection();
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE membership_history
                SET left_at = $leftAt
                WHERE conversation_id = $conversationId 
                  AND user_id = $userId 
                  AND left_at IS NULL;
                """;
            command.Parameters.AddWithValue("$conversationId", conversationId);
            command.Parameters.AddWithValue("$userId", userId);
            command.Parameters.AddWithValue("$leftAt", timestamp.ToString("o"));

            await command.ExecuteNonQueryAsync();
        }

        public async Task<IEnumerable<(DateTime JoinedAt, DateTime? LeftAt)>> GetMembershipHistoryAsync(string conversationId, string userId)
        {
            List<(DateTime JoinedAt, DateTime? LeftAt)> history = [];

            await using var connection = database.CreateConnection();
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT joined_at, left_at
                FROM membership_history
                WHERE conversation_id = $conversationId AND user_id = $userId
                ORDER BY joined_at ASC;
                """;
            command.Parameters.AddWithValue("$conversationId", conversationId);
            command.Parameters.AddWithValue("$userId", userId);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                DateTime joinedAt = DateTime.Parse(reader.GetString(0)).ToUniversalTime();
                DateTime? leftAt = reader.IsDBNull(1) ? null : DateTime.Parse(reader.GetString(1)).ToUniversalTime();
                history.Add((joinedAt, leftAt));
            }

            return history;
        }

        private async Task UpsertAsync(Conversation entity, bool updateExisting)
        {
            await using var connection = database.CreateConnection();
            await connection.OpenAsync();
            using SqliteTransaction transaction = connection.BeginTransaction();

            string sql = updateExisting
                ? """
                    UPDATE conversations
                    SET name = $name,
                        type = $type,
                        owner_id = $ownerId
                    WHERE id = $id;
                    """
                : """
                    INSERT INTO conversations (id, name, type, owner_id)
                    VALUES ($id, $name, $type, $ownerId);
                    """;

            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = sql;
                command.Parameters.AddWithValue("$id", entity.Id);
                command.Parameters.AddWithValue("$name", entity.Name);
                command.Parameters.AddWithValue("$type", entity is GroupConversation ? "group" : "private");
                command.Parameters.AddWithValue("$ownerId", entity is GroupConversation group ? group.OwnerId : DBNull.Value);
                await command.ExecuteNonQueryAsync();
            }

            await using (var deleteParticipants = connection.CreateCommand())
            {
                deleteParticipants.Transaction = transaction;
                deleteParticipants.CommandText = "DELETE FROM conversation_participants WHERE conversation_id = $conversationId;";
                deleteParticipants.Parameters.AddWithValue("$conversationId", entity.Id);
                await deleteParticipants.ExecuteNonQueryAsync();
            }

            foreach (string participantId in entity.ParticipantIds)
            {
                await using var insertParticipant = connection.CreateCommand();
                insertParticipant.Transaction = transaction;
                insertParticipant.CommandText = """
                    INSERT INTO conversation_participants (conversation_id, user_id)
                    VALUES ($conversationId, $userId);
                    """;
                insertParticipant.Parameters.AddWithValue("$conversationId", entity.Id);
                insertParticipant.Parameters.AddWithValue("$userId", participantId);
                await insertParticipant.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }

        private async Task<Conversation?> GetConversationByConditionAsync(SqliteConnection connection, string whereClause, string value)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"""
                SELECT id, name, type, owner_id
                FROM conversations
                {whereClause};
                """;
            command.Parameters.AddWithValue("$value", value);

            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return null;
            }

            return await ReadConversationAsync(connection, reader);
        }

        private async Task<Conversation> ReadConversationAsync(SqliteConnection connection, SqliteDataReader reader)
        {
            string id = reader.GetString(0);
            string name = reader.GetString(1);
            string type = reader.GetString(2);
            string? ownerId = reader.IsDBNull(3) ? null : reader.GetString(3);

            Conversation conversation = type switch
            {
                "group" => new GroupConversation { Id = id, Name = name, OwnerId = ownerId ?? string.Empty },
                _ => new PrivateConversation { Id = id, Name = name }
            };

            await using var participantsCommand = connection.CreateCommand();
            participantsCommand.CommandText = """
                SELECT user_id
                FROM conversation_participants
                WHERE conversation_id = $conversationId
                ORDER BY user_id;
                """;
            participantsCommand.Parameters.AddWithValue("$conversationId", id);

            await using var participantReader = await participantsCommand.ExecuteReaderAsync();
            while (await participantReader.ReadAsync())
            {
                conversation.ParticipantIds.Add(participantReader.GetString(0));
            }

            return conversation;
        }
    }
}
