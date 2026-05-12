using System.Text.Json;

namespace ChatCore.Security
{
    public sealed class ServerIdentityStore
    {
        private readonly string identityPath;
        private readonly RsaService rsaService = new();

        public ServerIdentityStore(string dataDirectory)
        {
            identityPath = Path.Combine(dataDirectory, "security", "server-rsa-identity.json");
        }

        public RsaKeyPair LoadOrCreate()
        {
            if (File.Exists(identityPath))
            {
                string json = File.ReadAllText(identityPath);
                RsaKeyPair? existingKeys = JsonSerializer.Deserialize<RsaKeyPair>(json);
                if (existingKeys != null &&
                    !string.IsNullOrWhiteSpace(existingKeys.PublicKey) &&
                    !string.IsNullOrWhiteSpace(existingKeys.PrivateKey))
                {
                    return existingKeys;
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(identityPath) ?? AppContext.BaseDirectory);
            RsaKeyPair newKeys = rsaService.GenerateKeyPair();
            File.WriteAllText(identityPath, JsonSerializer.Serialize(newKeys, new JsonSerializerOptions { WriteIndented = true }));
            return newKeys;
        }
    }
}
