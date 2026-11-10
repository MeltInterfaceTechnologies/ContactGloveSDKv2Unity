using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace MeltInterfaceTechnologies.ContactGlove.V2
{
    internal sealed class HostSession : IDisposable
    {
        private readonly object gate = new object();
        private readonly object sendGate = new object();
        private readonly object dispatchGate = new object();
        private readonly HostOptions options;
        private readonly IPAddress serverAddress;
        private UdpClient receiver, sender;
        private Thread worker;
        private volatile bool active;
        private readonly Dictionary<StreamKind, Dictionary<string, Snapshot>> cache = new Dictionary<StreamKind, Dictionary<string, Snapshot>>();
        private readonly Queue<object> notifications = new Queue<object>();
        private readonly List<Subscription> subscriptions = new List<Subscription>();
        private readonly Dictionary<int, PendingProfile> pendingProfiles = new Dictionary<int, PendingProfile>();
        private readonly HeartbeatSchedule heartbeat = new HeartbeatSchedule();
        private readonly SessionDiagnostics diagnostics;
        private long pendingLoss;
        public string HostId => options.HostId;
        public bool Active => active;
        private sealed class PendingProfile { public string Uuid; public double Deadline; }
        internal sealed class Subscription : IDisposable
        {
            private readonly HostSession owner;
            public readonly Type Type;
            public readonly Action<object> Callback;
            public volatile bool Active = true;
            public Subscription(HostSession owner, Type type, Action<object> callback) { this.owner = owner; Type = type; Callback = callback; }
            public void Dispose() { lock (owner.gate) { Active = false; owner.subscriptions.Remove(this); } }
        }
        public HostSession(HostOptions options, IPAddress address)
        {
            this.options = options.Copy(); serverAddress = address;
            diagnostics = new SessionDiagnostics { HostId = options.HostId, State = SessionState.Starting, LastError = "" };
            for (int i = 0; i < 6; i++) cache.Add((StreamKind)i, new Dictionary<string, Snapshot>(StringComparer.Ordinal));
        }
        public OperationResult Start()
        {
            try
            {
                receiver = Bind(options.ReceivePort); sender = Bind(options.SendPort);
                receiver.Client.ReceiveTimeout = 100;
                sender.Connect(serverAddress, options.ServerPort);
                diagnostics.ReceivePort = ((IPEndPoint)receiver.Client.LocalEndPoint).Port;
                diagnostics.SendPort = ((IPEndPoint)sender.Client.LocalEndPoint).Port;
                active = true;
                var result = Register();
                if (!result.Succeeded) { Dispose(); return result; }
                diagnostics.State = SessionState.WaitingForData;
                worker = new Thread(ReceiveLoop) { IsBackground = true, Name = "ContactGlove v2: " + HostId };
                worker.Start(); return new OperationResult(ResultCode.Success);
            }
            catch (Exception ex) when (ex is SocketException || ex is ArgumentException || ex is InvalidOperationException)
            { Dispose(); return new OperationResult(ResultCode.TransportUnavailable, ex.Message); }
        }
        private UdpClient Bind(int port)
        {
            var socket = new UdpClient(serverAddress.AddressFamily);
            try
            {
                socket.Client.ExclusiveAddressUse = true;
                socket.Client.Bind(new IPEndPoint(serverAddress.AddressFamily == AddressFamily.InterNetwork ? IPAddress.Any : IPAddress.IPv6Any, port));
                return socket;
            }
            catch { socket.Dispose(); throw; }
        }
        internal bool MatchesConnection(HostOptions candidate, IPAddress address) =>
            serverAddress.Equals(address) && options.ServerPort == candidate.ServerPort
            && options.ReceivePort == candidate.ReceivePort && options.SendPort == candidate.SendPort;
        public OperationResult Register()
        {
            lock (sendGate)
            {
                if (!active || sender == null) return new OperationResult(ResultCode.NotConnected);
                double now = MonotonicTime.Now;
                if (!heartbeat.CanRetry(now)) return new OperationResult(ResultCode.Success, "Registration rate limited.");
                heartbeat.MarkSent(now);
                var result = Send(ProtocolV2.Prefix + "/Connect", diagnostics.ReceivePort);
                lock (gate) { if (result.Succeeded) diagnostics.ConnectSends++; }
                return result;
            }
        }
        private OperationResult Send(string address, params object[] args)
        {
            lock (sendGate)
            {
                if (!active || sender == null) return new OperationResult(ResultCode.NotConnected);
                try { var packet = OscCodec.Encode(address, args); sender.Send(packet, packet.Length); return new OperationResult(ResultCode.Success); }
                catch (Exception ex) when (ex is SocketException || ex is ObjectDisposedException)
                { lock (gate) { diagnostics.LastError = ex.Message; } return new OperationResult(ResultCode.TransportUnavailable, ex.Message); }
            }
        }
        private void ReceiveLoop()
        {
            try
            {
                while (active)
                {
                    Maintenance();
                    IPEndPoint source = null;
                    byte[] packet;
                    try { packet = receiver.Receive(ref source); }
                    catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut || ex.SocketErrorCode == SocketError.WouldBlock || ex.SocketErrorCode == SocketError.ConnectionReset) { continue; }
                    if (!active) break;
                    if (!source.Address.Equals(serverAddress)) { lock (gate) diagnostics.UnexpectedSources++; continue; }
                    ReceivePacket(packet);
                }
            }
            catch (Exception ex) when (ex is SocketException || ex is ObjectDisposedException)
            { lock (gate) { if (active) { diagnostics.State = SessionState.Faulted; diagnostics.LastError = ex.Message; } } }
        }
        private void Maintenance()
        {
            double now = MonotonicTime.Now;
            lock (sendGate)
            {
                if (heartbeat.IsDue(now))
                {
                    var result = Register();
                    if (!result.Succeeded) lock (gate) diagnostics.State = SessionState.Faulted;
                }
            }
            lock (gate)
            {
                var expired = new List<int>();
                foreach (var pair in pendingProfiles) if (now >= pair.Value.Deadline) expired.Add(pair.Key);
                foreach (int id in expired) { var value = pendingProfiles[id]; pendingProfiles.Remove(id); Enqueue(new ProfileTimeout(HostId, id, value.Uuid)); }
            }
        }
        private void ReceivePacket(byte[] packet)
        {
            double time = MonotonicTime.Now;
            lock (gate) diagnostics.ReceivedDatagrams++;
            if (!OscCodec.TryDecode(packet, out var messages, out string error)) { ParseError(error); return; }
            foreach (var message in messages)
            {
                if (!ProtocolV2.TryParse(message, HostId, time, packet.Length, out var records, out error)) { ParseError(error); continue; }
                lock (gate)
                {
                    if (!active) return;
                    foreach (var update in records)
                    {
                        if (update.Payload is ProfileResponseSnapshot response)
                        {
                            if (!pendingProfiles.TryGetValue(response.RequestId, out var pending) ||
                                (response.Status != ProfileStatus.InvalidRequest && !string.Equals(pending.Uuid, response.ProfileUuid, StringComparison.OrdinalIgnoreCase)))
                            { diagnostics.UnmatchedProfileResponses++; continue; }
                            pendingProfiles.Remove(response.RequestId); Enqueue(response);
                        }
                        else
                        {
                            var snapshot = (Snapshot)update.Payload;
                            string id = snapshot.Key.GloveId;
                            var stream = cache[update.Kind];
                            bool disconnected = snapshot is DeviceSnapshot device && device.Connection == ConnectionState.Unconnected;
                            bool known = stream.ContainsKey(id);
                            // A disconnect must still clear an ID known only through another stream.
                            if (disconnected && !known) foreach (var cachedStream in cache.Values) if (cachedStream.ContainsKey(id)) { known = true; break; }
                            if (!known && stream.Count >= 16) { diagnostics.CacheLimitDrops++; continue; }
                            if (disconnected)
                            {
                                foreach (var cachedStream in cache.Values) cachedStream.Remove(id);
                                Enqueue(snapshot); // Observable even though polling removes the device.
                            }
                            else
                            {
                                // Overflow may clear Controller cache. Enqueue before publishing the current record.
                                Enqueue(snapshot); stream[id] = snapshot;
                                if (snapshot is ControllerSnapshot) diagnostics.InputResynchronizationRequired = false;
                            }
                        }
                        diagnostics.AcceptedRecords++; diagnostics.LastReceivedAt = time;
                        diagnostics.State = SessionState.Receiving;
                    }
                }
            }
        }
        private void ParseError(string error) { lock (gate) { diagnostics.ParseErrors++; diagnostics.LastError = error; } }
        private void Enqueue(object notification)
        {
            if (notifications.Count >= options.NotificationCapacity)
            {
                int lost = notifications.Count; notifications.Clear(); pendingLoss += lost; diagnostics.NotificationDrops += lost;
                cache[StreamKind.Controller].Clear(); diagnostics.InputResynchronizationRequired = true;
            }
            notifications.Enqueue(notification);
        }
        public IDisposable Subscribe<T>(Action<T> callback)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            lock (gate)
            {
                if (!active) throw new ObjectDisposedException(nameof(HostSession));
                var token = new Subscription(this, typeof(T), value => callback((T)value)); subscriptions.Add(token); return token;
            }
        }
        public int Pump(int budget)
        {
            int count = 0;
            while (count < budget && active)
            {
                object value; Subscription[] listeners;
                lock (gate)
                {
                    if (!active) break;
                    if (pendingLoss > 0) { value = new NotificationLoss(HostId, pendingLoss); pendingLoss = 0; }
                    else if (notifications.Count > 0) value = notifications.Dequeue(); else break;
                    listeners = subscriptions.ToArray();
                }
                foreach (var token in listeners)
                {
                    lock (dispatchGate)
                    {
                        if (!active) break;
                        if (!token.Active || !token.Type.IsInstanceOfType(value)) continue;
                        try { token.Callback(value); }
                        catch (Exception ex) { lock (gate) { diagnostics.CallbackExceptions++; diagnostics.LastError = ex.Message; } }
                    }
                }
                count++;
            }
            return count;
        }
        public bool TryGet<T>(StreamKind kind, string id, out T value) where T : Snapshot
        {
            lock (gate) { value = null; if (!active || id == null || !cache[kind].TryGetValue(id, out var snapshot)) return false; value = (T)snapshot; return true; }
        }
        public DeviceSnapshot[] Devices()
        {
            lock (gate) { var result = new DeviceSnapshot[cache[StreamKind.Device].Count]; int i = 0; foreach (var value in cache[StreamKind.Device].Values) result[i++] = (DeviceSnapshot)value; return result; }
        }
        public OperationResult Vibrate(string id, float frequency, float amplitude, float duration)
        {
            if (!IsLiteralAddressSegment(id) ||
                !ProtocolV2.InRange(frequency, 100, 500) || !ProtocolV2.InRange(amplitude, 0, 1) || !ProtocolV2.InRange(duration, 0.001f, 1)) return new OperationResult(ResultCode.InvalidArgument);
            if (!TryGet(StreamKind.Device, id, out DeviceSnapshot device)) return new OperationResult(ResultCode.UnknownDevice);
            if (device.Connection != ConnectionState.Connected) return new OperationResult(ResultCode.NotConnected);
            if (!device.SupportsHaptics) return new OperationResult(ResultCode.Unsupported);
            return Send(ProtocolV2.Prefix + "/" + id + "/Haptics/Body", 2, frequency, amplitude, duration);
        }
        private static bool IsLiteralAddressSegment(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            foreach (char character in value)
                if (char.IsControl(character) || "/ #*?[]{},".IndexOf(character) >= 0) return false;
            return true;
        }
        public OperationResult RequestProfile(string uuid, int id, out int requestId)
        {
            requestId = 0;
            if (!ProtocolV2.IsUuid(uuid)) return new OperationResult(ResultCode.InvalidArgument);
            lock (gate)
            {
                if (!active) return new OperationResult(ResultCode.NotConnected);
                if (pendingProfiles.Count >= 128) return new OperationResult(ResultCode.TransportUnavailable, "Pending profile limit reached.");
                if (pendingProfiles.ContainsKey(id)) return new OperationResult(ResultCode.TransportUnavailable, "Request ID collision.");
                pendingProfiles[id] = new PendingProfile { Uuid = uuid, Deadline = MonotonicTime.Now + options.ProfileTimeoutSeconds };
            }
            var result = Send(ProtocolV2.Prefix + "/HandSkeleton/Profile/Get", 2, id, uuid);
            if (!result.Succeeded) { lock (gate) pendingProfiles.Remove(id); return result; }
            requestId = id; return result;
        }
        public SessionDiagnostics Diagnostics()
        {
            lock (gate)
            {
                SessionState state = diagnostics.State;
                if (state == SessionState.Receiving && options.StaleAfterSeconds > 0 && MonotonicTime.Now - diagnostics.LastReceivedAt >= options.StaleAfterSeconds) state = SessionState.Stale;
                return new SessionDiagnostics { HostId = HostId, State = state, ReceivePort = diagnostics.ReceivePort, SendPort = diagnostics.SendPort, QueueDepth = notifications.Count,
                    ReceivedDatagrams = diagnostics.ReceivedDatagrams, AcceptedRecords = diagnostics.AcceptedRecords, ParseErrors = diagnostics.ParseErrors,
                    CacheLimitDrops = diagnostics.CacheLimitDrops, NotificationDrops = diagnostics.NotificationDrops, CallbackExceptions = diagnostics.CallbackExceptions,
                    UnexpectedSources = diagnostics.UnexpectedSources, UnmatchedProfileResponses = diagnostics.UnmatchedProfileResponses, ConnectSends = diagnostics.ConnectSends,
                    LastReceivedAt = diagnostics.LastReceivedAt, LastError = diagnostics.LastError, InputResynchronizationRequired = diagnostics.InputResynchronizationRequired };
            }
        }
        public void Dispose()
        {
            lock (dispatchGate)
            {
                lock (sendGate)
                {
                    if (active) Send(ProtocolV2.Prefix + "/Disconnect", diagnostics.ReceivePort);
                    active = false; receiver?.Close(); sender?.Close();
                }
            }
            if (worker != null && worker != Thread.CurrentThread) worker.Join();
            lock (gate)
            {
                foreach (var stream in cache.Values) stream.Clear(); notifications.Clear(); pendingProfiles.Clear(); pendingLoss = 0;
                foreach (var token in subscriptions) token.Active = false; subscriptions.Clear(); diagnostics.State = SessionState.Stopped;
            }
        }
    }
}
