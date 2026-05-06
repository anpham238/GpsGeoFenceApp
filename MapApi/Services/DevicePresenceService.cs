using System.Collections.Concurrent;

namespace MapApi.Services;

public interface IDevicePresenceService
{
    void MarkConnected(string deviceId, string connectionId);
    bool MarkDisconnected(string deviceId, string connectionId);
    bool IsOnline(string deviceId);
    int OnlineCount { get; }
    int TotalConnectionCount { get; }
}

public sealed class DevicePresenceService : IDevicePresenceService
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _onlineConnections
        = new(StringComparer.OrdinalIgnoreCase);

    public void MarkConnected(string deviceId, string connectionId)
    {
        var conns = _onlineConnections.GetOrAdd(deviceId, _ => new ConcurrentDictionary<string, byte>());
        conns[connectionId] = 1;
    }

    public bool MarkDisconnected(string deviceId, string connectionId)
    {
        if (_onlineConnections.TryGetValue(deviceId, out var conns))
        {
            conns.TryRemove(connectionId, out _);
            if (conns.IsEmpty)
                _onlineConnections.TryRemove(deviceId, out _);
        }
        return IsOnline(deviceId);
    }

    public bool IsOnline(string deviceId) =>
        _onlineConnections.TryGetValue(deviceId, out var conns) && !conns.IsEmpty;

    public int OnlineCount => _onlineConnections.Count(x => !x.Value.IsEmpty);

    // Tổng số kết nối SignalR (n thiết bị × số tab/instance mỗi thiết bị)
    public int TotalConnectionCount => _onlineConnections.Sum(x => x.Value.Count);
}
