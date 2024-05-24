using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Channels;

namespace ActorWebServerPlayground;

public sealed class ConnectionActor : IDisposable
{
    private readonly Channel<object> _messages;
    private readonly Task _processingTask;
    private readonly Socket _connection;
    private readonly byte[] _buffer = new byte[1024];

    public ConnectionActor(Socket connection)
    {
        _connection = connection;
        _messages = Channel.CreateUnbounded<object>();
        _processingTask = Task.Factory.StartNew(ProcessMessages, CancellationToken.None, TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
        _ = StartListeningAsync(_connection);
    }

    private async Task ProcessMessages()
    {
        await foreach (var message in _messages.Reader.ReadAllAsync())
        {
            await ReceiveAsync(message);
        }
    }

    private async Task ReceiveAsync(object message)
    {
        switch (message)
        {
            case string response:
                var responseBytes = Encoding.UTF8.GetBytes(response);
                await Task.Factory.FromAsync(
                _connection!.BeginSend(responseBytes, 0, response.Length, SocketFlags.None, null, null),
                _connection.EndSend);
                break;
        }
    }

    private async Task StartListeningAsync(Socket connection)
    {
        while (true)
        {
            int bytesRead = await connection.ReceiveAsync(new ArraySegment<byte>(_buffer), SocketFlags.None);
            if (bytesRead == 0)
            {
                break;
            }

            string message = Encoding.UTF8.GetString(_buffer, 0, bytesRead);
            Console.WriteLine($"Received: {message}");

            await _messages.Writer.WriteAsync(message);
        }
    }

    public void Dispose()
    {
        _connection?.Close();
        _connection?.Dispose();

        _messages?.Writer?.Complete();
        _processingTask?.Dispose();
    }
}

internal sealed class ActorSystem : IDisposable
{
    internal readonly NonBlocking.ConcurrentDictionary<IPEndPoint, ConnectionActor> _connectionActors = new();

    private ConnectionActor CreateActor(IPEndPoint endpoint, Socket connection)
    {
        var actor = new ConnectionActor(connection);
        if (!_connectionActors.TryAdd(endpoint, actor))
        {
            throw new InvalidOperationException($"Actor with name {endpoint} already exists.");
        }
        return actor;
    }

    public ConnectionActor GetActor(IPEndPoint endpoint, Socket connection)
        => _connectionActors.TryGetValue(endpoint, out var actor) ? actor : CreateActor(endpoint, connection);

    public void Dispose()
    {
        foreach (var actor in _connectionActors.Values)
        {
            actor.Dispose();
        }
    }
}
