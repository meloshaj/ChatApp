using ChatCore.Interfaces;
using ChatCore.Models;
using ChatCore.Persistence;
using Microsoft.Data.Sqlite;

namespace ChatCore.Repositories
{
    public sealed class SqliteMessageRepository : IMessageRepository
    {
        private readonly SqliteDatabase database;

        public SqliteMessageRepository(SqliteDatabase database)
        {
            this.database = database;
        }

        public async Task<Message?> GetByIdAsync(string id)
        {
            await using var connection = database.CreateConnection();
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT id, sender_id, conversation_id, timestamp, is_edited, edited_at, is_deleted,
                       message_type, text_content, file_name, file_path, duration_seconds, image_data
                FROM messages
                WHERE id = $id;
                """;
            command.Parameters.AddWithValue("$id", id);

            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return null;
            }

            return await ReadMessageAsync(connection, reader);
        }

        public async Task<IEnumerable<Message>> GetAllAsync()
        {
            List<Message> messages = [];

            await using var connection = database.CreateConnection();
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT id, sender_id, conversation_id, timestamp, is_edited, edited_at, is_deleted,
                       message_type, text_content, file_name, file_path, duration_seconds, image_data
                FROM messages
                ORDER BY timestamp;
                """;

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                messages.Add(await ReadMessageAsync(connection, reader));
            }

            return messages;
        }

        public async Task AddAsync(Message entity)
        {
            await UpsertAsync(entity, false);
        }

        public async Task UpdateAsync(Message entity)
        {
            await UpsertAsync(entity, true);
        }

        public async Task DeleteAsync(string id)
        {
            await using var connection = database.CreateConnection();
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM messages WHERE id = $id;";
            command.Parameters.AddWithValue("$id", id);
            await command.ExecuteNonQueryAsync();
        }

        public async Task<bool> ExistsAsync(string id)
        {
            await using var connection = database.CreateConnection();
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT EXISTS(SELECT 1 FROM messages WHERE id = $id);";
            command.Parameters.AddWithValue("$id", id);
            return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
        }

        public async Task<IEnumerable<Message>> GetConversationMessagesAsync(string conversationId, int limit = 100)
        {
            List<Message> messages = [];

            await using var connection = database.CreateConnection();
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT id, sender_id, conversation_id, timestamp, is_edited, edited_at, is_deleted,
                       message_type, text_content, file_name, file_path, duration_seconds, image_data
                FROM (
                    SELECT id, sender_id, conversation_id, timestamp, is_edited, edited_at, is_deleted,
                           message_type, text_content, file_name, file_path, duration_seconds, image_data
                    FROM messages
                    WHERE conversation_id = $conversationId
                    ORDER BY timestamp DESC
                    LIMIT $limit
                )
                ORDER BY timestamp;
                """;
            command.Parameters.AddWithValue("$conversationId", conversationId);
            command.Parameters.AddWithValue("$limit", limit);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                messages.Add(await ReadMessageAsync(connection, reader));
            }

            return messages;
        }

        public async Task<IEnumerable<Message>> GetMessagesSinceAsync(string conversationId, DateTime since)
        {
            List<Message> messages = [];

            await using var connection = database.CreateConnection();
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT id, sender_id, conversation_id, timestamp, is_edited, edited_at, is_deleted,
                       message_type, text_content, file_name, file_path, duration_seconds, image_data
                FROM messages
                WHERE conversation_id = $conversationId
                  AND timestamp > $since
                ORDER BY timestamp;
                """;
            command.Parameters.AddWithValue("$conversationId", conversationId);
            command.Parameters.AddWithValue("$since", since.ToString("O"));

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                messages.Add(await ReadMessageAsync(connection, reader));
            }

            return messages;
        }

        public async Task MarkAsReadAsync(string messageId, string userId)
        {
            await using var connection = database.CreateConnection();
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT OR IGNORE INTO read_receipts (message_id, user_id)
                VALUES ($messageId, $userId);
                """;
            command.Parameters.AddWithValue("$messageId", messageId);
            command.Parameters.AddWithValue("$userId", userId);
            await command.ExecuteNonQueryAsync();
        }

        private async Task UpsertAsync(Message entity, bool updateExisting)
        {
            await using var connection = database.CreateConnection();
            await connection.OpenAsync();
            using SqliteTransaction transaction = connection.BeginTransaction();

            string sql = updateExisting
                ? """
                    UPDATE messages
                    SET sender_id = $senderId,
                        conversation_id = $conversationId,
                        timestamp = $timestamp,
                        is_edited = $isEdited,
                        edited_at = $editedAt,
                        is_deleted = $isDeleted,
                        message_type = $messageType,
                        text_content = $textContent,
                        file_name = $fileName,
                        file_path = $filePath,
                        duration_seconds = $durationSeconds,
                        image_data = $imageData
                    WHERE id = $id;
                    """
                : """
                    INSERT INTO messages (
                        id, sender_id, conversation_id, timestamp, is_edited, edited_at, is_deleted,
                        message_type, text_content, file_name, file_path, duration_seconds, image_data)
                    VALUES (
                        $id, $senderId, $conversationId, $timestamp, $isEdited, $editedAt, $isDeleted,
                        $messageType, $textContent, $fileName, $filePath, $durationSeconds, $imageData);
                    """;

            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = sql;
                FillMessageParameters(command, entity);
                await command.ExecuteNonQueryAsync();
            }

            await using (var deleteReadReceipts = connection.CreateCommand())
            {
                deleteReadReceipts.Transaction = transaction;
                deleteReadReceipts.CommandText = "DELETE FROM read_receipts WHERE message_id = $messageId;";
                deleteReadReceipts.Parameters.AddWithValue("$messageId", entity.Id);
                await deleteReadReceipts.ExecuteNonQueryAsync();
            }

            foreach (string userId in entity.ReadBy)
            {
                await using var insertReadReceipt = connection.CreateCommand();
                insertReadReceipt.Transaction = transaction;
                insertReadReceipt.CommandText = """
                    INSERT INTO read_receipts (message_id, user_id)
                    VALUES ($messageId, $userId);
                    """;
                insertReadReceipt.Parameters.AddWithValue("$messageId", entity.Id);
                insertReadReceipt.Parameters.AddWithValue("$userId", userId);
                await insertReadReceipt.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }

        private static void FillMessageParameters(SqliteCommand command, Message entity)
        {
            command.Parameters.AddWithValue("$id", entity.Id);
            command.Parameters.AddWithValue("$senderId", entity.SenderId);
            command.Parameters.AddWithValue("$conversationId", entity.ConversationId);
            command.Parameters.AddWithValue("$timestamp", entity.Timestamp.ToString("O"));
            command.Parameters.AddWithValue("$isEdited", entity.IsEdited ? 1 : 0);
            command.Parameters.AddWithValue("$editedAt", entity.EditedAt?.ToString("O") ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$isDeleted", entity.IsDeleted ? 1 : 0);
            command.Parameters.AddWithValue("$messageType", GetMessageType(entity));
            command.Parameters.AddWithValue("$textContent", entity is TextMessage textMessage ? textMessage.Text : DBNull.Value);
            command.Parameters.AddWithValue("$fileName", entity is ImageMessage imageMessage ? imageMessage.FileName :
                entity is AudioMessage audioMessage ? audioMessage.FileName :
                entity is VoiceMessage voiceMessage ? voiceMessage.FileName :
                entity is VideoMessage videoMessage ? videoMessage.FileName : DBNull.Value);
            command.Parameters.AddWithValue("$filePath", entity is ImageMessage imageEntityForPath ? imageEntityForPath.FilePath :
                entity is AudioMessage audioEntityForPath ? audioEntityForPath.FilePath :
                entity is VoiceMessage voiceEntityForPath ? voiceEntityForPath.FilePath :
                entity is VideoMessage videoEntityForPath ? videoEntityForPath.FilePath : DBNull.Value);
            command.Parameters.AddWithValue("$durationSeconds", entity is VoiceMessage voiceEntityForDuration ? voiceEntityForDuration.DurationSeconds : DBNull.Value);
            command.Parameters.Add("$imageData", SqliteType.Blob).Value = entity switch
            {
                ImageMessage imageEntity => imageEntity.ImageData,
                AudioMessage audioEntity => audioEntity.AudioData,
                VoiceMessage voiceEntity => voiceEntity.AudioData,
                VideoMessage videoEntity => videoEntity.VideoData,
                _ => Array.Empty<byte>()
            };
        }

        private async Task<Message> ReadMessageAsync(SqliteConnection connection, SqliteDataReader reader)
        {
            Message message = reader.GetString(7) switch
            {
                "image" => new ImageMessage
                {
                    FileName = reader.IsDBNull(9) ? string.Empty : reader.GetString(9),
                    FilePath = reader.IsDBNull(10) ? string.Empty : reader.GetString(10),
                    ImageData = reader.IsDBNull(12) ? [] : (byte[])reader[12]
                },
                "voice" => new VoiceMessage
                {
                    FileName = reader.IsDBNull(9) ? string.Empty : reader.GetString(9),
                    FilePath = reader.IsDBNull(10) ? string.Empty : reader.GetString(10),
                    DurationSeconds = reader.IsDBNull(11) ? 0 : reader.GetInt32(11),
                    AudioData = reader.IsDBNull(12) ? [] : (byte[])reader[12]
                },
                "audio" => new AudioMessage
                {
                    FileName = reader.IsDBNull(9) ? string.Empty : reader.GetString(9),
                    FilePath = reader.IsDBNull(10) ? string.Empty : reader.GetString(10),
                    AudioData = reader.IsDBNull(12) ? [] : (byte[])reader[12]
                },
                "video" => new VideoMessage
                {
                    FileName = reader.IsDBNull(9) ? string.Empty : reader.GetString(9),
                    FilePath = reader.IsDBNull(10) ? string.Empty : reader.GetString(10),
                    VideoData = reader.IsDBNull(12) ? [] : (byte[])reader[12]
                },
                _ => new TextMessage
                {
                    Text = reader.IsDBNull(8) ? string.Empty : reader.GetString(8)
                }
            };

            message.Id = reader.GetString(0);
            message.SenderId = reader.GetString(1);
            message.ConversationId = reader.GetString(2);
            message.Timestamp = DateTime.Parse(reader.GetString(3), null, System.Globalization.DateTimeStyles.RoundtripKind);
            message.IsEdited = reader.GetInt32(4) == 1;
            message.EditedAt = reader.IsDBNull(5) ? null : DateTime.Parse(reader.GetString(5), null, System.Globalization.DateTimeStyles.RoundtripKind);
            message.IsDeleted = reader.GetInt32(6) == 1;

            await using var readReceiptCommand = connection.CreateCommand();
            readReceiptCommand.CommandText = """
                SELECT user_id
                FROM read_receipts
                WHERE message_id = $messageId;
                """;
            readReceiptCommand.Parameters.AddWithValue("$messageId", message.Id);
            await using var readReceiptReader = await readReceiptCommand.ExecuteReaderAsync();
            while (await readReceiptReader.ReadAsync())
            {
                message.ReadBy.Add(readReceiptReader.GetString(0));
            }

            return message;
        }

        private static string GetMessageType(Message entity)
        {
            return entity switch
            {
                ImageMessage => "image",
                AudioMessage => "audio",
                VideoMessage => "video",
                VoiceMessage => "voice",
                _ => "text"
            };
        }
    }
}
