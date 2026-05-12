using System.IO;
using System.Text.Json;

namespace ChatClient.Wpf.Services;

public sealed class TrustedServerStore
{
    private readonly string trustPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ChatApp",
        "trusted-servers.json");

    public bool IsTrustedOrTrustFirstUse(string host, string port, string serverRsaPublicKey)
    {
        Dictionary<string, string> trustedServers = LoadTrustedServers();
        string trustKey = $"{host.Trim().ToLowerInvariant()}:{port.Trim()}";

        if (trustedServers.TryGetValue(trustKey, out string? pinnedPublicKey))
        {
            return string.Equals(pinnedPublicKey, serverRsaPublicKey, StringComparison.Ordinal);
        }

        trustedServers[trustKey] = serverRsaPublicKey;
        SaveTrustedServers(trustedServers);
        return true;
    }

    private Dictionary<string, string> LoadTrustedServers()
    {
        if (!File.Exists(trustPath))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        string json = File.ReadAllText(trustPath);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
            ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    private void SaveTrustedServers(Dictionary<string, string> trustedServers)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(trustPath) ?? AppContext.BaseDirectory);
        File.WriteAllText(trustPath, JsonSerializer.Serialize(trustedServers, new JsonSerializerOptions { WriteIndented = true }));
    }
}
