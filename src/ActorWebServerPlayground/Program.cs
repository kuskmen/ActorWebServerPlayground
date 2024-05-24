using System.Collections.Immutable;
using System.Net;
using System.Net.Sockets;

namespace ActorWebServerPlayground;

public class Program
{
    public static async Task Main()
    {
        var server = new WebServer();

        await server.Listen();
    }
}

public static class WebServerStatsKeys
{
    public const string ActorsCount = nameof(ActorsCount);
}

public sealed class WebServer
{
    private readonly Socket _listener;

    private readonly ActorSystem _actorSystem;

    public WebServer()
    {
        _actorSystem = new ActorSystem();
        _listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        
        _listener.Bind(new IPEndPoint(IPAddress.Any, 9876));
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

    public IReadOnlyDictionary<string, object> ServerStats => new Dictionary<string, object>
    {
        [WebServerStatsKeys.ActorsCount] = _actorSystem._connectionActors.Count,
    };
}
