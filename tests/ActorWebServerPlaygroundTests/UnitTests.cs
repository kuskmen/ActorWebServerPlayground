using ActorWebServerPlayground;
using System.Net.Sockets;
using System.Text;

namespace ActorWebServerPlaygroundTests;

public class TcpServerTests
{
    private WebServer _server;
    private readonly int _port = 9876;
    private readonly string _ip = "127.0.0.1";


    [OneTimeSetUp]
    public async Task OneTimeSetup()
    {
        _server = new WebServer();
        _server.Listen();

        await Task.Delay(500);
    }

    [Test]
    public async Task SingleSocket_ShouldHaveOnlyOneActor()
    {
        // Connect to the server as a client
        using var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        await client.ConnectAsync(_ip, _port);

        // Send a message to the server
        var message = "Hello, Server!";
        var messageBytes = Encoding.UTF8.GetBytes(message);
        var bytesSent = await client.SendAsync(messageBytes);

        // Receive the response
        var buffer = new byte[1024];
        var bytesRead = await client.ReceiveAsync(buffer);
        var response = Encoding.UTF8.GetString(buffer, 0, bytesRead);

        // Verify the response
        Assert.That(response, Is.EqualTo(message));
        Assert.That((int)_server.ServerStats[WebServerStatsKeys.ActorsCount], Is.EqualTo(1));

        message = "Hello, Server for the second time!";
        messageBytes = Encoding.UTF8.GetBytes(message);
        bytesSent = await client.SendAsync(messageBytes);

        bytesRead = await client.ReceiveAsync(buffer);
        response = Encoding.UTF8.GetString(buffer, 0, bytesRead);

        Assert.That(response, Is.EqualTo(message));
        Assert.That((int)_server.ServerStats[WebServerStatsKeys.ActorsCount], Is.EqualTo(1));

    }

    [Test]
    public async Task MultipleSockets_ShouldGoToDifferentActors()
    {
        await Task.WhenAll(
            [
                Task.Run(SingleSocket_ShouldHaveOnlyOneActor),
                Task.Run(SingleSocket_ShouldHaveOnlyOneActor)
            ]);

        Assert.That((int)_server.ServerStats[WebServerStatsKeys.ActorsCount], Is.EqualTo(2));
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {

    }
}