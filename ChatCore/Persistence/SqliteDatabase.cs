using Microsoft.Data.Sqlite;

namespace ChatCore.Persistence
{
    public sealed class SqliteDatabase
    {
        private readonly string connectionString;

        public SqliteDatabase(string databasePath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(databasePath) ?? AppContext.BaseDirectory);
            connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                ForeignKeys = true
            }.ToString();
        }

        public async Task InitializeAsync()
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();

            string commandText = """
                PRAGMA foreign_keys = ON;

                CREATE TABLE IF NOT EXISTS users (
                    id TEXT PRIMARY KEY,
                    username TEXT NOT NULL COLLATE NOCASE UNIQUE,
                    password_hash TEXT NOT NULL,
                    rsa_public_key TEXT NOT NULL DEFAULT '',
                    rsa_private_key TEXT NOT NULL DEFAULT '',
                    active_conversation_id TEXT NULL,
                    status INTEGER NOT NULL,
                    last_seen TEXT NOT NULL,
                    is_typing INTEGER NOT NULL,
                    typing_in_conversation_id TEXT NULL
                );

                CREATE TABLE IF NOT EXISTS friendships (
                    user_id TEXT NOT NULL,
                    friend_id TEXT NOT NULL,
                    PRIMARY KEY (user_id, friend_id),
                    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
                    FOREIGN KEY (friend_id) REFERENCES users(id) ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS conversations (
                    id TEXT PRIMARY KEY,
                    name TEXT NOT NULL,
                    type TEXT NOT NULL,
                    owner_id TEXT NULL
                );

                CREATE TABLE IF NOT EXISTS conversation_participants (
                    conversation_id TEXT NOT NULL,
                    user_id TEXT NOT NULL,
                    PRIMARY KEY (conversation_id, user_id),
                    FOREIGN KEY (conversation_id) REFERENCES conversations(id) ON DELETE CASCADE,
                    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS messages (
                    id TEXT PRIMARY KEY,
                    sender_id TEXT NOT NULL,
                    conversation_id TEXT NOT NULL,
                    timestamp TEXT NOT NULL,
                    is_edited INTEGER NOT NULL,
                    edited_at TEXT NULL,
                    is_deleted INTEGER NOT NULL,
                    message_type TEXT NOT NULL,
                    text_content TEXT NULL,
                    file_name TEXT NULL,
                    file_path TEXT NULL,
                    duration_seconds INTEGER NULL,
                    image_data BLOB NULL,
                    FOREIGN KEY (conversation_id) REFERENCES conversations(id) ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS read_receipts (
                    message_id TEXT NOT NULL,
                    user_id TEXT NOT NULL,
                    PRIMARY KEY (message_id, user_id),
                    FOREIGN KEY (message_id) REFERENCES messages(id) ON DELETE CASCADE,
                    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE
                );

                CREATE INDEX IF NOT EXISTS idx_messages_conversation_timestamp
                    ON messages(conversation_id, timestamp);

                CREATE INDEX IF NOT EXISTS idx_conversation_participants_user
                    ON conversation_participants(user_id);
                """;

            await using var command = connection.CreateCommand();
            command.CommandText = commandText;
            await command.ExecuteNonQueryAsync();

            await AddColumnIfMissingAsync(connection, "users", "rsa_public_key", "TEXT NOT NULL DEFAULT ''");
            await AddColumnIfMissingAsync(connection, "users", "rsa_private_key", "TEXT NOT NULL DEFAULT ''");
            await ClearStoredUserPrivateKeysAsync(connection);
        }

        public SqliteConnection CreateConnection() => new(connectionString);

        private static async Task AddColumnIfMissingAsync(
            SqliteConnection connection,
            string tableName,
            string columnName,
            string columnDefinition)
        {
            await using var checkCommand = connection.CreateCommand();
            checkCommand.CommandText = $"PRAGMA table_info({tableName});";

            await using var reader = await checkCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            await using var alterCommand = connection.CreateCommand();
            alterCommand.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition};";
            await alterCommand.ExecuteNonQueryAsync();
        }

        private static async Task ClearStoredUserPrivateKeysAsync(SqliteConnection connection)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE users
                SET rsa_private_key = ''
                WHERE rsa_private_key <> '';
                """;
            await command.ExecuteNonQueryAsync();
        }
    }
}
