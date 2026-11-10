using System;
using System.Collections.Generic;
using System.Text;

namespace MeltInterfaceTechnologies.ContactGlove.V2
{
    public sealed class ProtocolUpdate
    {
        public StreamKind Kind { get; }
        public object Payload { get; }
        internal ProtocolUpdate(StreamKind kind, object value) { Kind = kind; Payload = value; }
    }
    public static class ProtocolV2
    {
        public const string Prefix = "/DS2/HC";
        public static bool IsUuid(string value)
        {
            if (value == null || value.Length != 36) return false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (i == 8 || i == 13 || i == 18 || i == 23) { if (c != '-') return false; }
                else if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))) return false;
            }
            return true;
        }
        public static bool InRange(float value, float min, float max) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= min && value <= max;

        /// <summary>Returns an entire validated message. Unknown addresses return an empty successful result.</summary>
        public static bool TryParse(OscMessage message, string hostId, double receivedAt, int datagramBytes,
            out IReadOnlyList<ProtocolUpdate> updates, out string error)
        {
            updates = Array.Empty<ProtocolUpdate>(); error = "";
            string address = message.Address;
            int width;
            StreamKind kind;
            switch (address)
            {
                case Prefix + "/Device": width = 10; kind = StreamKind.Device; break;
                case Prefix + "/Controller": width = 15; kind = StreamKind.Controller; break;
                case Prefix + "/Ergonomics": width = 25; kind = StreamKind.Ergonomics; break;
                case Prefix + "/RawSensorTransforms": width = 39; kind = StreamKind.RawSensorTransforms; break;
                case Prefix + "/HandSkeleton": width = 157; kind = StreamKind.Skeleton; break;
                case Prefix + "/HandSkeletonRaw": width = 156; kind = StreamKind.RawSkeleton; break;
                case Prefix + "/HandSkeleton/Profile": width = 5; kind = StreamKind.ProfileResponse; break;
                default: return true;
            }
            try
            {
                var reader = new ArgumentReader(message.Arguments);
                if (reader.Int() != 2) throw new FormatException("Unsupported protocol version.");
                int count = (message.Arguments.Count - 1) / width;
                if ((message.Arguments.Count - 1) % width != 0 || count > 8) throw new FormatException("Invalid record count.");
                if ((kind == StreamKind.Skeleton || kind == StreamKind.RawSkeleton || kind == StreamKind.ProfileResponse) && count != 1) throw new FormatException("Exactly one record is required.");
                if ((kind == StreamKind.Skeleton || kind == StreamKind.RawSkeleton) && datagramBytes > 1200) throw new FormatException("Skeleton datagram exceeds 1200 bytes.");
                var result = new List<ProtocolUpdate>(count);
                for (int record = 0; record < count; record++)
                {
                    object snapshot;
                    if (kind == StreamKind.ProfileResponse)
                    {
                        int id = reader.Int(); string uuid = reader.String(); var status = (ProfileStatus)reader.Enum(2);
                        string name = reader.String(128), description = reader.String(768);
                        if (status == ProfileStatus.InvalidRequest ? uuid != "" || name != "" || description != "" : !IsUuid(uuid)) throw new FormatException("Invalid profile response UUID.");
                        if (status == ProfileStatus.NotFound && (name != "" || description != "")) throw new FormatException("Missing profile must have empty display fields.");
                        snapshot = new ProfileResponseSnapshot(hostId, id, uuid, status, name, description, receivedAt);
                    }
                    else
                    {
                        string id = reader.String(kind == StreamKind.Skeleton || kind == StreamKind.RawSkeleton ? 64 : int.MaxValue);
                        var key = new DeviceKey(hostId, id);
                        if (kind == StreamKind.Device)
                        {
                            string pair = reader.String(); int battery = reader.Int();
                            if (battery < -1 || battery > 100) throw new FormatException("Invalid battery percentage.");
                            bool haptics = reader.Bool(), left = reader.Bool();
                            if (reader.Int() != 3) throw new FormatException("Unsupported device family.");
                            var color = (DeviceColor)reader.Enum(7); var connection = (ConnectionState)reader.Enum(3);
                            string name = reader.String(); bool power = reader.Bool();
                            snapshot = new DeviceSnapshot(key, left, receivedAt, pair, battery, haptics, DeviceFamily.ContactGlove3, color, connection, name, power);
                        }
                        else
                        {
                            string profile = kind == StreamKind.Skeleton ? reader.String() : "";
                            if (kind == StreamKind.Skeleton && !IsUuid(profile)) throw new FormatException("Invalid profile UUID.");
                            bool left = reader.Bool();
                            if (kind == StreamKind.Controller)
                            {
                                var buttons = new ButtonState[6]; for (int i = 0; i < 6; i++) buttons[i] = (ButtonState)reader.Enum(2);
                                var axes = new float[7]; for (int i = 0; i < 7; i++) axes[i] = reader.Range(i < 3 ? 0 : -1, 1);
                                snapshot = new ControllerSnapshot(key, left, receivedAt, buttons, axes);
                            }
                            else
                            {
                                ulong timestamp = ((ulong)(uint)reader.Int() << 32) | (uint)reader.Int();
                                if (kind == StreamKind.RawSensorTransforms)
                                {
                                    var poses = new WirePose[5]; for (int i = 0; i < 5; i++) poses[i] = new WirePose(reader.Vector(), reader.Quaternion());
                                    snapshot = new RawSensorSnapshot(key, left, receivedAt, timestamp, poses);
                                }
                                else
                                {
                                    var tracking = (TrackingState)reader.Enum(2);
                                    if (kind == StreamKind.Ergonomics)
                                    {
                                        var angles = new float[20]; for (int i = 0; i < 20; i++) angles[i] = reader.Float();
                                        snapshot = new ErgonomicsSnapshot(key, left, receivedAt, timestamp, tracking, angles);
                                    }
                                    else
                                    {
                                        var positions = new WireVector3[25]; for (int i = 0; i < 25; i++) positions[i] = reader.Vector();
                                        var rotations = new WireQuaternion[19]; for (int i = 0; i < 19; i++) rotations[i] = reader.Quaternion();
                                        snapshot = kind == StreamKind.Skeleton ? (object)new SkeletonSnapshot(key, left, receivedAt, timestamp, tracking, positions, rotations, profile)
                                            : new RawSkeletonSnapshot(key, left, receivedAt, timestamp, tracking, positions, rotations);
                                    }
                                }
                            }
                        }
                    }
                    result.Add(new ProtocolUpdate(kind, snapshot));
                }
                updates = result.AsReadOnly(); return true;
            }
            catch (FormatException ex) { error = ex.Message; return false; }
        }
        private sealed class ArgumentReader
        {
            private readonly IReadOnlyList<object> values; private int index;
            public ArgumentReader(IReadOnlyList<object> values) { this.values = values; }
            private T Read<T>()
            {
                if (index >= values.Count || !(values[index] is T value)) throw new FormatException("Incorrect OSC type or missing argument.");
                index++; return value;
            }
            public int Int() => Read<int>();
            public int Enum(int maximum) { int value = Int(); if (value < 0 || value > maximum) throw new FormatException("Invalid enum."); return value; }
            public bool Bool() => Enum(1) == 1;
            public string String(int maxBytes = int.MaxValue)
            { string value = Read<string>(); if (Encoding.UTF8.GetByteCount(value) > maxBytes) throw new FormatException("UTF-8 string is too long."); return value; }
            public float Float() { float value = Read<float>(); if (float.IsNaN(value) || float.IsInfinity(value)) throw new FormatException("Non-finite float."); return value; }
            public float Range(float min, float max) { float value = Float(); if (!InRange(value, min, max)) throw new FormatException("Out-of-range float."); return value; }
            public WireVector3 Vector() => new WireVector3(Float(), Float(), Float());
            public WireQuaternion Quaternion()
            {
                var value = new WireQuaternion(Float(), Float(), Float(), Float());
                double norm = (double)value.W * value.W + (double)value.X * value.X + (double)value.Y * value.Y + (double)value.Z * value.Z;
                if (Math.Abs(norm - 1) > 0.01) throw new FormatException("Quaternion is not normalized.");
                return value;
            }
        }
    }
}
