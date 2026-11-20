using System.IO;
using System.Text;
using HidSharp;

namespace Batto.Logitech;

/// <summary>A paired Logitech device reached through a receiver, identified by receiver PID and slot.</summary>
public sealed record LogitechSlot(string Id, string Name);

/// <summary>
/// Finds Logitech devices paired to a receiver and reads their battery over HID++.
/// Each request opens the receiver only for its duration, so Logi Options+ keeps working.
/// All HID I/O is serialised through one lock.
/// </summary>
public sealed class LogitechDeviceScanner
{
    public const string IdPrefix = "logitech:";

    private static readonly byte[] SlotIndexes = { 1, 2, 3, 4, 5, 6 };
    private static readonly TimeSpan FullScanInterval = TimeSpan.FromSeconds(60);
    private const int ReplyTimeoutMs = 800;

    private readonly SemaphoreSlim _io = new(1, 1);
    private readonly Dictionary<string, SlotInfo> _slots = new();
    private readonly Dictionary<int, string> _workingPathByReceiver = new();
    private DateTime _lastFullScan = DateTime.MinValue;

    private sealed record SlotInfo(int ReceiverPid, byte Index, string Name, byte? BatteryFeatureIndex);

    private sealed class ReceiverSession : IDisposable
    {
        public ReceiverSession(HidStream stream, string devicePath)
        {
            Stream = stream;
            DevicePath = devicePath;
        }

        public HidStream Stream { get; }
        public string DevicePath { get; }

        public void Dispose() => Stream.Dispose();
    }

    public static bool IsLogitechId(string id) => id.StartsWith(IdPrefix, StringComparison.Ordinal);

    /// <summary>Returns every known slot. Probes for new slots at most once per interval.</summary>
    public Task<IReadOnlyList<LogitechSlot>> DiscoverAsync() => RunExclusiveAsync(DiscoverCore);

    /// <summary>Battery percentage for a known slot, or null when the device does not answer.</summary>
    public Task<int?> ReadBatteryAsync(string id) => RunExclusiveAsync(() => ReadBatteryCore(id));

    private async Task<T> RunExclusiveAsync<T>(Func<T> work)
    {
        await _io.WaitAsync().ConfigureAwait(false);
        try
        {
            return await Task.Run(work).ConfigureAwait(false);
        }
        finally
        {
            _io.Release();
        }
    }

    private IReadOnlyList<LogitechSlot> DiscoverCore()
    {
        bool fullScan = DateTime.UtcNow - _lastFullScan >= FullScanInterval;

        if (fullScan)
        {
            foreach (int pid in GetReceiverPids())
            {
                using var session = OpenReceiver(pid);
                if (session is null) continue;

                foreach (byte index in SlotIndexes)
                {
                    string id = $"{IdPrefix}{pid:X4}:{index}";
                    if (_slots.ContainsKey(id)) continue;

                    byte? batteryFeature = LookupFeature(session.Stream, index, HidppProtocol.FeatureUnifiedBattery);
                    if (batteryFeature is null) continue;

                    string name = ReadDeviceName(session.Stream, index) ?? "Dispositivo Logitech";
                    _slots[id] = new SlotInfo(pid, index, name, batteryFeature);
                }
            }

            _lastFullScan = DateTime.UtcNow;
        }

        return _slots.Select(pair => new LogitechSlot(pair.Key, pair.Value.Name)).ToList();
    }

    private int? ReadBatteryCore(string id)
    {
        if (!_slots.TryGetValue(id, out var info)) return null;

        using var session = OpenReceiver(info.ReceiverPid);
        if (session is null) return null;

        byte? batteryFeature = info.BatteryFeatureIndex ?? LookupFeature(session.Stream, info.Index, HidppProtocol.FeatureUnifiedBattery);
        if (batteryFeature is null) return null;

        // Remember the feature index so later reads skip the IRoot lookup
        _slots[id] = info with { BatteryFeatureIndex = batteryFeature };

        byte[]? reply = Request(session.Stream, info.Index, batteryFeature.Value, HidppProtocol.BatteryGetStatusFunction);
        return reply is null ? null : HidppProtocol.ParseBatteryPercent(reply);
    }

    private static IEnumerable<int> GetReceiverPids()
    {
        return DeviceList.Local.GetHidDevices(HidppProtocol.VendorId)
            .Where(d => d.GetMaxOutputReportLength() >= HidppProtocol.LongReportLength)
            .Select(d => d.ProductID)
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// Opens the HID++ interface of a receiver. The interface that answered last time is tried first,
    /// and a new one is accepted only when a paired slot answers an IRoot request.
    /// </summary>
    private ReceiverSession? OpenReceiver(int pid)
    {
        var candidates = DeviceList.Local.GetHidDevices(HidppProtocol.VendorId)
            .Where(d => d.ProductID == pid && d.GetMaxOutputReportLength() >= HidppProtocol.LongReportLength)
            .OrderBy(d => _workingPathByReceiver.TryGetValue(pid, out var path) && path == d.DevicePath ? 0 : 1)
            .ToList();

        foreach (var candidate in candidates)
        {
            if (!candidate.TryOpen(out var stream)) continue;

            bool isKnownWorkingPath = _workingPathByReceiver.TryGetValue(pid, out var cached) && cached == candidate.DevicePath;
            if (isKnownWorkingPath || AnySlotAnswers(stream))
            {
                _workingPathByReceiver[pid] = candidate.DevicePath;
                return new ReceiverSession(stream, candidate.DevicePath);
            }

            stream.Dispose();
        }

        return null;
    }

    private static bool AnySlotAnswers(HidStream stream)
    {
        foreach (byte index in SlotIndexes)
        {
            if (LookupFeature(stream, index, HidppProtocol.FeatureUnifiedBattery) is not null) return true;
        }
        return false;
    }

    private static byte? LookupFeature(HidStream stream, byte deviceIndex, ushort featureId)
    {
        byte[]? reply = Request(
            stream,
            deviceIndex,
            HidppProtocol.RootFeatureIndex,
            HidppProtocol.RootGetFeatureFunction,
            (byte)(featureId >> 8),
            (byte)(featureId & 0xFF));

        return reply is null ? null : HidppProtocol.ParseFeatureIndex(reply);
    }

    private static string? ReadDeviceName(HidStream stream, byte deviceIndex)
    {
        byte? nameFeature = LookupFeature(stream, deviceIndex, HidppProtocol.FeatureDeviceName);
        if (nameFeature is null) return null;

        byte[]? countReply = Request(stream, deviceIndex, nameFeature.Value, HidppProtocol.NameGetCountFunction);
        if (countReply is null) return null;

        int length = HidppProtocol.ParseNameLength(countReply);
        if (length <= 0) return null;

        var name = new StringBuilder();
        while (name.Length < length)
        {
            byte[]? chunkReply = Request(stream, deviceIndex, nameFeature.Value, HidppProtocol.NameGetCharFunction, (byte)name.Length);
            if (chunkReply is null) break;

            string chunk = HidppProtocol.ParseNameChunk(chunkReply);
            if (chunk.Length == 0) break;
            name.Append(chunk);
        }

        return name.Length == 0 ? null : name.ToString(0, Math.Min(name.Length, length));
    }

    /// <summary>Sends one request and waits for its matching reply. Returns null on timeout or I/O error.</summary>
    private static byte[]? Request(HidStream stream, byte deviceIndex, byte featureIndex, byte function, params byte[] parameters)
    {
        byte[] request = HidppProtocol.BuildRequest(deviceIndex, featureIndex, function, parameters);

        try
        {
            stream.ReadTimeout = ReplyTimeoutMs;
            stream.Write(request);

            var buffer = new byte[HidppProtocol.LongReportLength];
            var deadline = DateTime.UtcNow.AddMilliseconds(ReplyTimeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                int read = stream.Read(buffer, 0, buffer.Length);
                if (read < 7) continue;

                if (HidppProtocol.IsReplyFor(buffer, deviceIndex, featureIndex, function))
                {
                    return (byte[])buffer.Clone();
                }
            }
        }
        catch (IOException)
        {
            return null;
        }
        catch (TimeoutException)
        {
            return null;
        }

        return null;
    }
}
