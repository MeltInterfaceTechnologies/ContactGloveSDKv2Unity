using System;
using System.Collections.Generic;
using System.Threading;

namespace MeltInterfaceTechnologies.ContactGlove.V2
{
    /// <summary>Owns host sessions. Polling is thread-safe. Call PumpNotifications on one owner thread (Unity Update).</summary>
    public sealed class ContactGloveClient : IDisposable
    {
        private readonly object gate = new object();
        private readonly Dictionary<string, HostSession> hosts = new Dictionary<string, HostSession>(StringComparer.Ordinal);
        private bool disposed;
        private int pumpThread;
        private bool pumping;
        private int nextRequestId = Guid.NewGuid().GetHashCode();
        /// <summary>Starts a host, or retries registration on its existing sockets when the endpoint and configured ports match.</summary>
        public OperationResult Connect(HostOptions options)
        {
            if (options == null || !options.TryValidate(out var address)) return new OperationResult(ResultCode.InvalidArgument);
            lock (gate)
            {
                if (disposed) return new OperationResult(ResultCode.Disposed);
                if (hosts.TryGetValue(options.HostId, out var existing))
                {
                    if (!existing.MatchesConnection(options, address)) return new OperationResult(ResultCode.InvalidArgument, "HostId is already started with a different endpoint or configured ports.");
                    return existing.Register();
                }
                var session = new HostSession(options, address); var result = session.Start();
                if (result.Succeeded) hosts.Add(options.HostId, session); return result;
            }
        }
        private HostSession Find(string id) { lock (gate) { return id != null && hosts.TryGetValue(id, out var session) ? session : null; } }
        public OperationResult Disconnect(string hostId)
        {
            HostSession session;
            lock (gate) { if (hostId == null || !hosts.TryGetValue(hostId, out session)) return new OperationResult(ResultCode.UnknownHost); hosts.Remove(hostId); }
            session.Dispose(); return new OperationResult(ResultCode.Success);
        }
        public OperationResult Resume(string hostId) => Find(hostId)?.Register() ?? new OperationResult(ResultCode.UnknownHost);
        public SessionDiagnostics GetDiagnostics(string hostId) => Find(hostId)?.Diagnostics();
        public DeviceSnapshot[] GetDevices(string hostId) => Find(hostId)?.Devices() ?? Array.Empty<DeviceSnapshot>();
        private bool Get<T>(DeviceKey key, StreamKind kind, out T value) where T : Snapshot
        { value = null; var session = Find(key.HostId); return session != null && session.TryGet(kind, key.GloveId, out value); }
        public bool TryGetDevice(DeviceKey key, out DeviceSnapshot value) => Get(key, StreamKind.Device, out value);
        public bool TryGetController(DeviceKey key, out ControllerSnapshot value) => Get(key, StreamKind.Controller, out value);
        public bool TryGetErgonomics(DeviceKey key, out ErgonomicsSnapshot value) => Get(key, StreamKind.Ergonomics, out value);
        public bool TryGetSkeleton(DeviceKey key, out SkeletonSnapshot value) => Get(key, StreamKind.Skeleton, out value);
        public bool TryGetRawSkeleton(DeviceKey key, out RawSkeletonSnapshot value) => Get(key, StreamKind.RawSkeleton, out value);
        public bool TryGetRawSensorTransforms(DeviceKey key, out RawSensorSnapshot value) => Get(key, StreamKind.RawSensorTransforms, out value);
        /// <summary>Distinguishes missing host, device metadata and an as-yet unreceived stream.</summary>
        public ResultCode GetDataStatus(DeviceKey key, StreamKind stream)
        {
            var session = Find(key.HostId); if (session == null) return ResultCode.UnknownHost;
            if ((int)stream < 0 || (int)stream >= 6 || key.GloveId == null) return ResultCode.InvalidArgument;
            if (session.TryGet<Snapshot>(stream, key.GloveId, out _)) return ResultCode.Success;
            return session.TryGet<DeviceSnapshot>(StreamKind.Device, key.GloveId, out _) ? ResultCode.DataUnavailable : ResultCode.UnknownDevice;
        }
        /// <summary>Success means local UDP submission; the protocol provides no actuation acknowledgement.</summary>
        public OperationResult Vibrate(DeviceKey key, float frequency, float amplitude, float duration) => Find(key.HostId)?.Vibrate(key.GloveId, frequency, amplitude, duration) ?? new OperationResult(ResultCode.UnknownHost);
        public OperationResult RequestRetargetProfileInfo(string hostId, string uuid, out int requestId)
        { requestId = 0; return Find(hostId)?.RequestProfile(uuid, Interlocked.Increment(ref nextRequestId), out requestId) ?? new OperationResult(ResultCode.UnknownHost); }
        private IDisposable Subscribe<T>(string host, Action<T> callback)
        { var session = Find(host); if (session == null) throw new ArgumentException("Start the host before subscribing.", nameof(host)); return session.Subscribe(callback); }
        public IDisposable SubscribeDevice(string host, Action<DeviceSnapshot> callback) => Subscribe(host, callback);
        public IDisposable SubscribeController(string host, Action<ControllerSnapshot> callback) => Subscribe(host, callback);
        public IDisposable SubscribeErgonomics(string host, Action<ErgonomicsSnapshot> callback) => Subscribe(host, callback);
        public IDisposable SubscribeSkeleton(string host, Action<SkeletonSnapshot> callback) => Subscribe(host, callback);
        public IDisposable SubscribeRawSkeleton(string host, Action<RawSkeletonSnapshot> callback) => Subscribe(host, callback);
        public IDisposable SubscribeRawSensorTransforms(string host, Action<RawSensorSnapshot> callback) => Subscribe(host, callback);
        public IDisposable SubscribeProfileResponse(string host, Action<ProfileResponseSnapshot> callback) => Subscribe(host, callback);
        public IDisposable SubscribeProfileTimeout(string host, Action<ProfileTimeout> callback) => Subscribe(host, callback);
        public IDisposable SubscribeNotificationLoss(string host, Action<NotificationLoss> callback) => Subscribe(host, callback);
        public int PumpNotifications(int maximumPerHost = 256)
        {
            if (maximumPerHost <= 0) throw new ArgumentOutOfRangeException(nameof(maximumPerHost));
            HostSession[] sessions;
            lock (gate)
            {
                if (disposed) return 0;
                int id = Thread.CurrentThread.ManagedThreadId;
                if (pumpThread != 0 && pumpThread != id) throw new InvalidOperationException("Notifications require a single owner thread.");
                if (pumping) throw new InvalidOperationException("Recursive notification pumping is not supported.");
                pumpThread = id; pumping = true; sessions = new List<HostSession>(hosts.Values).ToArray();
            }
            try { int count = 0; foreach (var session in sessions) count += session.Pump(maximumPerHost); return count; }
            finally { lock (gate) pumping = false; }
        }
        public void Dispose()
        {
            HostSession[] sessions;
            lock (gate) { if (disposed) return; disposed = true; sessions = new List<HostSession>(hosts.Values).ToArray(); hosts.Clear(); }
            foreach (var session in sessions) session.Dispose();
        }
    }
}
