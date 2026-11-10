using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace MeltInterfaceTechnologies.ContactGlove.V2
{
    public enum ConnectionState
    {
        Unknown = 0, Connected = 1, Unconnected = 2,
        /// <summary>Protocol connected_state=3: connecting. This name does not indicate a pairing operation.</summary>
        Pairing = 3
    }
    public enum TrackingState { Tracked, IkFailed, NotAttempted }
    public enum ButtonState { Unpressed, Touching, Pressed }
    public enum DeviceFamily { ContactGlove3 = 3 }
    /// <summary>Device display-color setting.</summary>
    public enum DeviceColor { Magenta, Red, Orange, Yellow, Green, Cyan, Blue, Achromatic }
    public enum StreamKind { Device, Controller, Ergonomics, Skeleton, RawSkeleton, RawSensorTransforms, ProfileResponse }
    public enum SessionState { Stopped, Starting, WaitingForData, Receiving, Stale, Faulted }
    public enum ResultCode { Success, InvalidArgument, UnknownHost, UnknownDevice, DataUnavailable, NotConnected, Unsupported, TransportUnavailable, Disposed }
    public enum ProfileStatus { Success, NotFound, InvalidRequest }

    public readonly struct OperationResult
    {
        public ResultCode Code { get; }
        public string Detail { get; }
        public bool Succeeded => Code == ResultCode.Success;
        public OperationResult(ResultCode code, string detail = "") { Code = code; Detail = detail; }
    }

    public readonly struct DeviceKey : IEquatable<DeviceKey>
    {
        public string HostId { get; }
        public string GloveId { get; }
        public DeviceKey(string hostId, string gloveId) { HostId = hostId; GloveId = gloveId; }
        public bool Equals(DeviceKey other) => HostId == other.HostId && GloveId == other.GloveId;
        public override bool Equals(object obj) => obj is DeviceKey key && Equals(key);
        public override int GetHashCode() => ((HostId?.GetHashCode() ?? 0) * 397) ^ (GloveId?.GetHashCode() ?? 0);
        public override string ToString() => HostId + "/" + GloveId;
    }

    public readonly struct WireVector3
    {
        public float X { get; } public float Y { get; } public float Z { get; }
        public WireVector3(float x, float y, float z) { X = x; Y = y; Z = z; }
    }
    public readonly struct WireQuaternion
    {
        public float W { get; } public float X { get; } public float Y { get; } public float Z { get; }
        public WireQuaternion(float w, float x, float y, float z) { W = w; X = x; Y = y; Z = z; }
    }
    public readonly struct WirePose
    {
        public WireVector3 Position { get; } public WireQuaternion Rotation { get; }
        public WirePose(WireVector3 position, WireQuaternion rotation) { Position = position; Rotation = rotation; }
    }

    /// <summary>Owns a private array. No receiver buffers or mutable cache arrays escape.</summary>
    public sealed class FixedValues<T> : IReadOnlyList<T>
    {
        private readonly T[] values;
        internal FixedValues(T[] source) { values = (T[])source.Clone(); }
        public int Count => values.Length;
        public T this[int index] => values[index];
        public void CopyTo(T[] destination, int offset = 0) => values.CopyTo(destination, offset);
        public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)values).GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => values.GetEnumerator();
    }

    public static class MonotonicTime
    {
        public static double Now => (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency;
    }

    public abstract class Snapshot
    {
        public DeviceKey Key { get; }
        public bool IsLeft { get; }
        public double ReceivedAt { get; }
        public double AgeSeconds => Math.Max(0, MonotonicTime.Now - ReceivedAt);
        protected Snapshot(DeviceKey key, bool left, double receivedAt) { Key = key; IsLeft = left; ReceivedAt = receivedAt; }
    }
    public sealed class DeviceSnapshot : Snapshot
    {
        /// <summary>Protocol pair_id: the paired counterpart glove's device ID, or an empty string when absent.</summary>
        public string PairId { get; }
        public int BatteryPercentage { get; }
        public bool SupportsHaptics { get; } public DeviceFamily Family { get; }
        public DeviceColor Color { get; } public ConnectionState Connection { get; }
        public string Name { get; } public bool ExternalPowerPresent { get; }
        internal DeviceSnapshot(DeviceKey key, bool left, double time, string pair, int battery, bool haptics,
            DeviceFamily family, DeviceColor color, ConnectionState connection, string name, bool power) : base(key, left, time)
        { PairId = pair; BatteryPercentage = battery; SupportsHaptics = haptics; Family = family; Color = color; Connection = connection; Name = name; ExternalPowerPresent = power; }
    }
    public sealed class ControllerSnapshot : Snapshot
    {
        public ButtonState A { get; } public ButtonState B { get; } public ButtonState System { get; }
        public ButtonState TriggerButton { get; } public ButtonState JoystickButton { get; } public ButtonState TrackpadButton { get; }
        public float Trigger { get; } public float GripValue { get; } public float GripForce { get; }
        public float JoystickX { get; } public float JoystickY { get; } public float TrackpadX { get; } public float TrackpadY { get; }
        internal ControllerSnapshot(DeviceKey key, bool left, double time, ButtonState[] buttons, float[] axes) : base(key, left, time)
        { A = buttons[0]; B = buttons[1]; System = buttons[2]; TriggerButton = buttons[3]; JoystickButton = buttons[4]; TrackpadButton = buttons[5];
            Trigger = axes[0]; GripValue = axes[1]; GripForce = axes[2]; JoystickX = axes[3]; JoystickY = axes[4]; TrackpadX = axes[5]; TrackpadY = axes[6]; }
    }
    public abstract class GeneratedSnapshot : Snapshot
    {
        public ulong GeneratedAt { get; }
        protected GeneratedSnapshot(DeviceKey key, bool left, double time, ulong generated) : base(key, left, time) { GeneratedAt = generated; }
    }
    public sealed class ErgonomicsSnapshot : GeneratedSnapshot
    {
        /// <summary>Only Tracked data may drive control, interpolation or velocity estimation.</summary>
        public TrackingState Tracking { get; }
        /// <summary>Degrees, thumb/index/middle/ring/little, 4 angles each: three flexion angles, then tilt.</summary>
        public FixedValues<float> Angles { get; }
        internal ErgonomicsSnapshot(DeviceKey key, bool left, double time, ulong generated, TrackingState tracking, float[] angles) : base(key, left, time, generated)
        { Tracking = tracking; Angles = new FixedValues<float>(angles); }
    }
    public abstract class HandSnapshot : GeneratedSnapshot
    {
        public TrackingState Tracking { get; }
        /// <summary>25 positions in wrist-root model space, meters.</summary>
        public FixedValues<WireVector3> Positions { get; }
        /// <summary>19 parent-local rotations, w,x,y,z. No wrist or tip rotation.</summary>
        public FixedValues<WireQuaternion> Rotations { get; }
        protected HandSnapshot(DeviceKey key, bool left, double time, ulong generated, TrackingState tracking, WireVector3[] positions, WireQuaternion[] rotations) : base(key, left, time, generated)
        { Tracking = tracking; Positions = new FixedValues<WireVector3>(positions); Rotations = new FixedValues<WireQuaternion>(rotations); }
    }
    public sealed class SkeletonSnapshot : HandSnapshot
    {
        public string ProfileUuid { get; }
        internal SkeletonSnapshot(DeviceKey key, bool left, double time, ulong generated, TrackingState tracking, WireVector3[] positions, WireQuaternion[] rotations, string uuid) : base(key, left, time, generated, tracking, positions, rotations) { ProfileUuid = uuid; }
    }
    public sealed class RawSkeletonSnapshot : HandSnapshot
    {
        internal RawSkeletonSnapshot(DeviceKey key, bool left, double time, ulong generated, TrackingState tracking, WireVector3[] positions, WireQuaternion[] rotations) : base(key, left, time, generated, tracking, positions, rotations) { }
    }
    public sealed class RawSensorSnapshot : GeneratedSnapshot
    {
        /// <summary>Sensor-local to model-root poses. The wire supplies no tracking state.</summary>
        public FixedValues<WirePose> Poses { get; }
        internal RawSensorSnapshot(DeviceKey key, bool left, double time, ulong generated, WirePose[] poses) : base(key, left, time, generated) { Poses = new FixedValues<WirePose>(poses); }
    }
    public sealed class ProfileResponseSnapshot
    {
        public string HostId { get; } public int RequestId { get; } public string ProfileUuid { get; }
        public ProfileStatus Status { get; } public string Name { get; } public string Description { get; } public double ReceivedAt { get; }
        internal ProfileResponseSnapshot(string host, int request, string uuid, ProfileStatus status, string name, string description, double time)
        { HostId = host; RequestId = request; ProfileUuid = uuid; Status = status; Name = name; Description = description; ReceivedAt = time; }
    }
    public sealed class ProfileTimeout
    {
        public string HostId { get; } public int RequestId { get; } public string ProfileUuid { get; }
        internal ProfileTimeout(string host, int id, string uuid) { HostId = host; RequestId = id; ProfileUuid = uuid; }
    }
    public static class HandTopology
    {
        public static readonly FixedValues<int> Parents = new FixedValues<int>(new[] { -1,0,1,2,3,0,5,6,7,8,0,10,11,12,13,0,15,16,17,18,0,20,21,22,23 });
        public static readonly FixedValues<int> RotationNodes = new FixedValues<int>(new[] { 1,2,3,5,6,7,8,10,11,12,13,15,16,17,18,20,21,22,23 });
    }
}
