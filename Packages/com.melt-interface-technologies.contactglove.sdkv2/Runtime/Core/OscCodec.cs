using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MeltInterfaceTechnologies.ContactGlove.V2
{
    public sealed class OscMessage
    {
        public string Address { get; }
        public FixedValues<object> Arguments { get; }
        public int ByteLength { get; }
        internal OscMessage(string address, object[] args, int bytes) { Address = address; Arguments = new FixedValues<object>(args); ByteLength = bytes; }
    }

    /// <summary>Strict OSC i/f/s codec with bounded bundle depth, message and argument counts.</summary>
    public static class OscCodec
    {
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        public const int MaximumDatagramBytes = 65507;
        public static byte[] Encode(string address, params object[] arguments)
        {
            if (string.IsNullOrEmpty(address) || address[0] != '/') throw new ArgumentException("OSC address must start with /.");
            if (arguments == null || arguments.Length > 512) throw new ArgumentException("Too many OSC arguments.");
            using (var output = new MemoryStream())
            {
                WriteString(output, address);
                var tags = new StringBuilder(",");
                foreach (var value in arguments)
                {
                    if (value is int) tags.Append('i'); else if (value is float) tags.Append('f');
                    else if (value is string) tags.Append('s'); else throw new ArgumentException("Only int32, float32 and strings are supported.");
                }
                WriteString(output, tags.ToString());
                foreach (var value in arguments)
                {
                    if (value is int integer) WriteInt(output, integer);
                    else if (value is float number) WriteInt(output, BitConverter.SingleToInt32Bits(number));
                    else WriteString(output, (string)value);
                }
                if (output.Length > MaximumDatagramBytes) throw new ArgumentException("Oversized datagram.");
                return output.ToArray();
            }
        }
        private static void WriteString(Stream output, string value)
        {
            if (value.IndexOf('\0') >= 0) throw new ArgumentException("Embedded NUL.");
            var bytes = Utf8.GetBytes(value); output.Write(bytes, 0, bytes.Length); output.WriteByte(0);
            while ((output.Position & 3) != 0) output.WriteByte(0);
        }
        private static void WriteInt(Stream output, int value)
        {
            output.WriteByte((byte)(value >> 24)); output.WriteByte((byte)(value >> 16));
            output.WriteByte((byte)(value >> 8)); output.WriteByte((byte)value);
        }
        public static bool TryDecode(byte[] packet, out IReadOnlyList<OscMessage> messages, out string error)
        {
            messages = Array.Empty<OscMessage>(); error = "";
            if (packet == null || packet.Length == 0 || packet.Length > MaximumDatagramBytes) { error = "Invalid datagram size."; return false; }
            try
            {
                var decoded = new List<OscMessage>(); Decode(packet, 0, packet.Length, 0, decoded);
                messages = decoded.AsReadOnly(); return true;
            }
            catch (Exception ex) when (ex is FormatException || ex is DecoderFallbackException)
            { error = ex.Message; return false; }
        }
        private static void Decode(byte[] packet, int start, int end, int depth, List<OscMessage> output)
        {
            if (depth > 8 || output.Count >= 64 || end - start < 4 || ((end - start) & 3) != 0) throw new FormatException("Invalid OSC container bounds.");
            var reader = new ByteReader(packet, start, end); string address = reader.String();
            if (address == "#bundle")
            {
                reader.Int(); reader.Int(); // OSC timetag; receipt order remains authoritative.
                while (reader.Position < end)
                {
                    int size = reader.Int();
                    if (size <= 0 || size > end - reader.Position) throw new FormatException("Invalid bundle element length.");
                    Decode(packet, reader.Position, reader.Position + size, depth + 1, output); reader.Skip(size);
                }
                return;
            }
            if (address.Length == 0 || address[0] != '/') throw new FormatException("Invalid OSC address.");
            string tags = reader.String();
            if (tags.Length == 0 || tags[0] != ',' || tags.Length > 513) throw new FormatException("Invalid OSC type tags.");
            var values = new object[tags.Length - 1];
            for (int i = 1; i < tags.Length; i++)
            {
                switch (tags[i])
                {
                    case 'i': values[i - 1] = reader.Int(); break;
                    case 'f': values[i - 1] = BitConverter.Int32BitsToSingle(reader.Int()); break;
                    case 's': values[i - 1] = reader.String(); break;
                    default: throw new FormatException("Unsupported OSC type tag.");
                }
            }
            if (reader.Position != end) throw new FormatException("Trailing OSC bytes.");
            output.Add(new OscMessage(address, values, end - start));
        }
        private sealed class ByteReader
        {
            private readonly byte[] bytes; private readonly int end;
            public int Position { get; private set; }
            public ByteReader(byte[] bytes, int start, int end) { this.bytes = bytes; Position = start; this.end = end; }
            public void Skip(int count) { Position += count; }
            public int Int()
            {
                if (end - Position < 4) throw new FormatException("Truncated OSC int/float.");
                int i = Position; Position += 4;
                return (bytes[i] << 24) | (bytes[i + 1] << 16) | (bytes[i + 2] << 8) | bytes[i + 3];
            }
            public string String()
            {
                int start = Position;
                while (Position < end && bytes[Position] != 0) Position++;
                if (Position == end) throw new FormatException("Unterminated OSC string.");
                string value = Utf8.GetString(bytes, start, Position - start); Position++;
                while (((Position - start) & 3) != 0)
                {
                    if (Position >= end || bytes[Position++] != 0) throw new FormatException("Invalid OSC string padding.");
                }
                return value;
            }
        }
    }
}
