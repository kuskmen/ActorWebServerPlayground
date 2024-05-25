using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Channels;

namespace ActorWebServerPlayground;

public sealed class ConnectionActor : IDisposable
{
    private readonly Channel<string> _messages;
    private readonly Task _processingTask;
    private readonly Socket _connection;
    private readonly byte[] _buffer = new byte[1024];

    public ConnectionActor(Socket connection)
    {
        _connection = connection;
        _messages = Channel.CreateUnbounded<string>();
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

    private async Task ReceiveAsync(string message)
    {
        var responseBytes = Encoding.UTF8.GetBytes(message);
        await Task.Factory.FromAsync(
        _connection!.BeginSend(responseBytes, 0, message.Length, SocketFlags.None, null, null),
        _connection.EndSend);
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
    // by default hold up to 50k connections
    internal readonly LRUCache<IPEndPoint, ConnectionActor> _connections = new(50000);

    private ConnectionActor CreateActor(IPEndPoint endpoint, Socket connection)
    {
        var actor = new ConnectionActor(connection);
        _connections.Put(endpoint, actor);
        return actor;
    }

    public ConnectionActor GetActor(IPEndPoint endpoint, Socket connection)
        => _connections.Get(endpoint) ?? CreateActor(endpoint, connection);
    
    public void Dispose()
    {
        _connections.Dispose();
    }
}



// TODO: Fix multithreading
public sealed class LRUCache<TKey, TValue>(int capacity) : IDisposable
    where TValue : IDisposable
{
    private readonly int _capacity = capacity;
    private readonly Dictionary<TKey, (TValue Value, int Position)> _cache = new(capacity);
    private readonly CircularBuffer<TKey> _buffer = new(capacity);

    public TValue Get(TKey key)
    {
        if (_cache.TryGetValue(key, out var valuePos))
        {
            // Move the key to the end (most recently used)
            _buffer.Remove(valuePos.Position);
            int newPosition = _buffer.Add(key);
            _cache[key] = (valuePos.Value, newPosition);
            return valuePos.Value;
        }
        return default;
    }

    public void Put(TKey key, TValue value)
    {
        if (_cache.TryGetValue(key, out var cachedValue))
        {
            _buffer.Remove(cachedValue.Position);
        }
        else if (_cache.Count >= _capacity)
        {
            // Remove the least recently used key
            var oldestKey = _buffer.Get(_buffer.Start);
            _buffer.Remove(_buffer.Start);
            _cache.Remove(oldestKey);
        }

        // Add the new key-value pair
        int newPosition = _buffer.Add(key);
        _cache[key] = (value, newPosition);
    }

    public int Count => _cache.Count;

    public void Dispose()
    {
        foreach (var (Value, _) in _cache.Values)
        {
            Value.Dispose();
        }
    }

    private class CircularBuffer<TKey>(int capacity)
    {
        private readonly TKey[] _buffer = new TKey[capacity];
        private int _start = 0;
        private int _end = 0;
        private readonly int _capacity = capacity;

        public int Add(TKey item)
        {
            _buffer[_end] = item;
            int position = _end;
            _end = (_end + 1) % _capacity;
            if (_end == _start)
            {
                _start = (_start + 1) % _capacity;
            }
            return position;
        }

        public TKey Remove(int position)
        {
            TKey item = _buffer[position];
            _buffer[position] = default;
            return item;
        }

        public TKey Get(int position)
        {
            return _buffer[position];
        }

        public int Capacity => _capacity;

        public int Start => _start;

        public int End => _end;
    }
}
