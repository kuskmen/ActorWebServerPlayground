using ActorWebServerPlayground;
using System.Net.Sockets;
using System.Text;

namespace ActorWebServerPlaygroundTests;

public class TcpServerTests
{
    private readonly string _ip = "127.0.0.1";

    [Test]
    public async Task SingleSocket_ShouldHaveOnlyOneActor()
    {
        var port = 9876;
        using var server = new WebServer(port);
        server.Listen();

        await Task.Delay(500);

        using var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        await client.ConnectAsync(_ip, port);

        // Send a message to the server
        var message = "Hello, Server";
        var response = await PingPongServer(client, message);

        // Verify the response
        Assert.That(response, Is.EqualTo(message));
        Assert.That((int)server.ServerStats[WebServerStatsKeys.ActorsCount], Is.EqualTo(1));

        message = "Hello, Server for the second time!";
        response = await PingPongServer(client, message);

        Assert.That(response, Is.EqualTo(message));
        Assert.That((int)server.ServerStats[WebServerStatsKeys.ActorsCount], Is.EqualTo(1));

    }

    [Test]
    public async Task MultipleSockets_ShouldGoToDifferentActors()
    {
        var port = 9875;
        using var server = new WebServer(port);
        server.Listen();

        await Task.Delay(500);

        async Task? pingPong()
        {
            using var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            await client.ConnectAsync(_ip, port);
            await PingPongServer(client, "Hello, Server");
        }

        await Task.WhenAll(
            [
                Task.Run(pingPong),
                Task.Run(pingPong)
            ]);

        Assert.That((int)server.ServerStats[WebServerStatsKeys.ActorsCount], Is.EqualTo(2));
    }

    private static async Task<string> PingPongServer(Socket client, string message)
    {
        var messageBytes = Encoding.UTF8.GetBytes(message);
        await client.SendAsync(messageBytes);

        // Receive the response
        var buffer = new byte[1024];
        var bytesRead = await client.ReceiveAsync(buffer);
        return Encoding.UTF8.GetString(buffer, 0, bytesRead);
    }
}