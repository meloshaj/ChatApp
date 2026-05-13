using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace ChatClient.Wpf.Services;

public sealed record DiscoveredServer(string Host, string Name, int Port);

public sealed class ServerDiscoveryClient
{
    private const string DiscoveryRequest = "CHATAPP_DISCOVER_V1";
    private const string DiscoveryResponse = "CHATAPP_SERVER_V1";

    public async Task<DiscoveredServer?> FindServerAsync(int port, TimeSpan timeout)
    {
        using UdpClient udp = new(0);
        udp.EnableBroadcast = true;

        byte[] request = Encoding.UTF8.GetBytes(DiscoveryRequest);
        foreach (IPEndPoint endpoint in GetBroadcastEndPoints(port))
        {
            await udp.SendAsync(request, request.Length, endpoint);
        }

        using CancellationTokenSource cancellation = new(timeout);
        while (!cancellation.IsCancellationRequested)
        {
            UdpReceiveResult result;
            try
            {
                result = await udp.ReceiveAsync(cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                return null;
            }

            string response = Encoding.UTF8.GetString(result.Buffer);
            string[] parts = response.Split('|', 3);
            if (parts.Length != 3 || !string.Equals(parts[0], DiscoveryResponse, StringComparison.Ordinal))
            {
                continue;
            }

            return int.TryParse(parts[2], out int serverPort)
                ? new DiscoveredServer(result.RemoteEndPoint.Address.ToString(), parts[1], serverPort)
                : new DiscoveredServer(result.RemoteEndPoint.Address.ToString(), parts[1], port);
        }

        return null;
    }

    private static IEnumerable<IPEndPoint> GetBroadcastEndPoints(int port)
    {
        yield return new IPEndPoint(IPAddress.Broadcast, port);

        foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus != OperationalStatus.Up ||
                networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
            {
                continue;
            }

            foreach (UnicastIPAddressInformation address in networkInterface.GetIPProperties().UnicastAddresses)
            {
                if (address.Address.AddressFamily != AddressFamily.InterNetwork || address.IPv4Mask == null)
                {
                    continue;
                }

                byte[] ipBytes = address.Address.GetAddressBytes();
                byte[] maskBytes = address.IPv4Mask.GetAddressBytes();
                byte[] broadcastBytes = new byte[ipBytes.Length];
                for (int i = 0; i < ipBytes.Length; i++)
                {
                    broadcastBytes[i] = (byte)(ipBytes[i] | ~maskBytes[i]);
                }

                yield return new IPEndPoint(new IPAddress(broadcastBytes), port);
            }
        }
    }
}
