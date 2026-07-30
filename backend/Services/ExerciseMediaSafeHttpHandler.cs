using System.Net;
using System.Net.Sockets;

namespace backend.Services;

public interface IExerciseMediaHostResolver
{
    Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken = default);
}

public sealed class ExerciseMediaHostResolver : IExerciseMediaHostResolver
{
    public Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken = default)
    {
        return Dns.GetHostAddressesAsync(host, cancellationToken);
    }
}

public static class ExerciseMediaSafeHttpHandler
{
    private static readonly HashSet<string> BlockedHostnames = new(StringComparer.OrdinalIgnoreCase)
    {
        "localhost",
        "localhost.localdomain",
    };

    public static SocketsHttpHandler Create(
        IExerciseMediaHostResolver hostResolver,
        TimeSpan connectTimeout)
    {
        return new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            UseProxy = false,
            Credentials = null,
            PreAuthenticate = false,
            ConnectTimeout = connectTimeout,
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            ConnectCallback = (context, cancellationToken) =>
                ConnectToValidatedAddressAsync(
                    context.DnsEndPoint,
                    hostResolver,
                    cancellationToken),
        };
    }

    internal static async Task<IPAddress[]> ResolveAllowedAddressesAsync(
        string host,
        IExerciseMediaHostResolver hostResolver,
        CancellationToken cancellationToken)
    {
        if (IsBlockedHostname(host))
        {
            throw new ExerciseMediaRemoteTargetException("URL points to a blocked network location.");
        }

        IPAddress[] addresses;
        try
        {
            addresses = IPAddress.TryParse(host, out var parsedAddress)
                ? [parsedAddress]
                : await hostResolver.ResolveAsync(host, cancellationToken);
        }
        catch (Exception exception) when (exception is SocketException or ArgumentException)
        {
            throw new ExerciseMediaRemoteTargetException("Unable to resolve the remote host for validation.");
        }

        if (addresses.Length == 0)
        {
            throw new ExerciseMediaRemoteTargetException("Unable to resolve the remote host for validation.");
        }

        if (addresses.Any(IsBlockedAddress))
        {
            throw new ExerciseMediaRemoteTargetException("URL points to a blocked network location.");
        }

        return addresses
            .Select(NormalizeAddress)
            .Distinct()
            .ToArray();
    }

    private static async ValueTask<Stream> ConnectToValidatedAddressAsync(
        DnsEndPoint target,
        IExerciseMediaHostResolver hostResolver,
        CancellationToken cancellationToken)
    {
        IPAddress[] addresses;
        try
        {
            addresses = await ResolveAllowedAddressesAsync(
                target.Host,
                hostResolver,
                cancellationToken);
        }
        catch (ExerciseMediaRemoteTargetException exception)
        {
            throw new HttpRequestException(exception.Message);
        }

        foreach (var address in addresses)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true,
            };

            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, target.Port), cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (SocketException)
            {
                socket.Dispose();
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }

        throw new HttpRequestException("Unable to connect to the remote server for validation.");
    }

    private static bool IsBlockedAddress(IPAddress address)
    {
        var normalizedAddress = NormalizeAddress(address);

        if (IPAddress.IsLoopback(normalizedAddress)
            || normalizedAddress.Equals(IPAddress.Any)
            || normalizedAddress.Equals(IPAddress.None)
            || normalizedAddress.Equals(IPAddress.IPv6Any)
            || normalizedAddress.Equals(IPAddress.IPv6None)
            || normalizedAddress.IsIPv6Multicast)
        {
            return true;
        }

        var bytes = normalizedAddress.GetAddressBytes();

        if (normalizedAddress.AddressFamily == AddressFamily.InterNetwork)
        {
            return bytes[0] == 0
                || bytes[0] == 10
                || bytes[0] == 127
                || (bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127)
                || (bytes[0] == 169 && bytes[1] == 254)
                || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                || (bytes[0] == 192 && bytes[1] == 168)
                || bytes[0] >= 224;
        }

        if (normalizedAddress.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return (bytes[0] & 0xFE) == 0xFC
                || (bytes[0] == 0xFE && (bytes[1] & 0xC0) == 0x80)
                || bytes.All(static value => value == 0);
        }

        return true;
    }

    private static IPAddress NormalizeAddress(IPAddress address)
    {
        return address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
    }

    private static bool IsBlockedHostname(string host)
    {
        var normalizedHost = host.Trim().TrimEnd('.');
        return BlockedHostnames.Contains(normalizedHost)
            || normalizedHost.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed class ExerciseMediaRemoteTargetException : Exception
{
    public ExerciseMediaRemoteTargetException(string message)
        : base(message)
    {
    }
}
