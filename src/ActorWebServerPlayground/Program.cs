using System.Collections.Immutable;
using System.Net;
using System.Net.Sockets;

namespace ActorWebServerPlayground;

public class Program
{
    public static async Task Main()
    {
        var server = new WebServer(9876);

        await server.Listen();
    }
}

public static class WebServerStatsKeys
{
    public const string ActorsCount = nameof(ActorsCount);
}

public sealed class WebServer : IDisposable
{
    private readonly Socket _listener;

    private readonly ActorSystem _actorSystem;

    public WebServer(int port)
    {
        _actorSystem = new ActorSystem();
        _listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        _listener.Bind(new IPEndPoint(IPAddress.Any, port));
        _listener.Listen();

        Console.WriteLine("WebServer listening on port: 9876");
    }

    public async Task Listen()
    {
        while (true)
        {
            try
            {
                var connection = await Task.Factory.FromAsync(
                    _listener.BeginAccept(null, null),
                    _listener.EndAccept);

                _ = _actorSystem.GetActor((IPEndPoint)connection.RemoteEndPoint!, connection);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
    }

    public void Dispose()
    {
        _actorSystem.Dispose();
    }

    public IReadOnlyDictionary<string, object> ServerStats => new Dictionary<string, object>
    {
        [WebServerStatsKeys.ActorsCount] = _actorSystem._connections.Count,
    };
}
