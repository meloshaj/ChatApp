using System.Net.Sockets;
using System.IO;
using ChatClient.Wpf.Models;

namespace ChatClient.Wpf.Services;

public class AuthenticationClient
{
    private TcpClient? client;
    private NetworkStream? stream;
    private readonly NetworkManager networkManager = new();
    public string LastHost { get; private set; } = string.Empty;
    public string LastPort { get; private set; } = string.Empty;

    public async Task<AuthResult> AuthenticateAsync(string host, string port, string username, string password, bool isRegister)
    {
        if (!int.TryParse(port, out int portNumber))
        {
            return new AuthResult { Success = false, ErrorMessage = "Invalid port number." };
        }

        try
        {
            LastHost = host.Trim();
            LastPort = port.Trim();
            client = new TcpClient();
            await client.ConnectAsync(LastHost, portNumber);
            stream = client.GetStream();

            // Wait for auth prompt
            var authPrompt = await networkManager.ReceiveMessage(stream);
            if (authPrompt == null || !authPrompt.Text.Contains("AUTH_REQUIRED"))
            {
                return new AuthResult { Success = false, ErrorMessage = "Server did not request authentication." };
            }

            // Send auth command
            string command = isRegister ? "REGISTER" : "LOGIN";
            var authMessage = new NetworkMessage
            {
                Sender = username,
                Type = "text",
                Text = $"{command} {username} {password}",
                Timestamp = DateTime.UtcNow
            };

            await networkManager.SendMessage(stream, authMessage);

            // Wait for response
            var response = await networkManager.ReceiveMessage(stream);
            if (response == null)
            {
                return new AuthResult { Success = false, ErrorMessage = "No response from server." };
            }

            if (response.Text.Contains("AUTH_SUCCESS"))
            {
                return new AuthResult 
                { 
                    Success = true, 
                    Username = username,
                    Client = client,
                    Stream = stream
                };
            }
            else if (response.Text.Contains("AUTH_FAILED"))
            {
                var parts = response.Text.Split('|');
                string error = parts.Length >= 2 ? parts[1] : "Authentication failed.";
                client.Close();
                return new AuthResult { Success = false, ErrorMessage = error };
            }

            client.Close();
            return new AuthResult { Success = false, ErrorMessage = "Unexpected server response." };
        }
        catch (Exception ex)
        {
            client?.Close();
            return new AuthResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public TcpClient? GetClient() => client;
    public NetworkStream? GetStream() => stream;
}

public class AuthResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? Username { get; set; }
    public TcpClient? Client { get; set; }
    public NetworkStream? Stream { get; set; }
}
