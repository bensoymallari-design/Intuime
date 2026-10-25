using System.Globalization;
using System.Text;

namespace Intuime.Core;

public sealed class OscMessage
{
    public string Address { get; init; } = "";
    public string? Text { get; init; }
    public double? Number { get; init; }
    public bool Integer { get; init; }
}

public static class OscAddresses
{
    public static string Canonical(string? address)
    {
        var trimmed = (address ?? "").Trim();
        if (trimmed.Length == 0)
            return "";
        return trimmed.StartsWith('/') ? trimmed : "/" + trimmed;
    }

    public static bool Same(string? left, string? right)
    {
        var a = Canonical(left);
        var b = Canonical(right);
        return a.Length > 1 && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}

public static class OscCodec
{
    public static byte[] Encode(OscMessage message)
    {
        var address = OscAddresses.Canonical(message.Address);
        if (address.Length <= 1)
            return Array.Empty<byte>();
        var buffer = new List<byte>(64);
        WriteString(buffer, address);
        if (message.Text != null)
        {
            WriteString(buffer, ",s");
            WriteString(buffer, message.Text);
        }
        else if (message.Number != null && message.Integer && message.Number is >= int.MinValue and <= int.MaxValue && message.Number == Math.Truncate(message.Number.Value))
        {
            WriteString(buffer, ",i");
            WriteInt(buffer, (int)message.Number.Value);
        }
        else if (message.Number != null)
        {
            WriteString(buffer, ",f");
            WriteInt(buffer, BitConverter.SingleToInt32Bits((float)message.Number.Value));
        }
        else
            WriteString(buffer, ",");
        return buffer.ToArray();
    }

    public static List<OscMessage> Decode(ReadOnlySpan<byte> packet)
    {
        var messages = new List<OscMessage>();
        Collect(packet, messages, 0);
        return messages;
    }

    public static bool TryArgument(string? raw, out OscMessage message, string address)
    {
        var value = raw?.Trim() ?? "";
        if (value.Length == 0)
        {
            message = new OscMessage { Address = address, Number = 1 };
            return true;
        }
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            var whole = value.IndexOf('.') < 0 && value.IndexOf('e') < 0 && value.IndexOf('E') < 0;
            message = new OscMessage { Address = address, Number = number, Integer = whole };
            return true;
        }
        message = new OscMessage { Address = address, Text = value };
        return true;
    }

    private static void Collect(ReadOnlySpan<byte> data, List<OscMessage> into, int depth)
    {
        if (depth > 4 || data.Length < 4)
            return;
        if (data[0] == (byte)'#')
        {
            var offset = 0;
            if (!TryString(data, ref offset, out var name) || name != "#bundle")
                return;
            offset += 8;
            while (offset + 4 <= data.Length)
            {
                if (!TryInt(data, ref offset, out var size) || size <= 0 || offset + size > data.Length)
                    return;
                Collect(data.Slice(offset, size), into, depth + 1);
                offset += size;
            }
            return;
        }
        if (TryMessage(data, out var message) && message != null)
            into.Add(message);
    }

    private static bool TryMessage(ReadOnlySpan<byte> data, out OscMessage? message)
    {
        message = null;
        var offset = 0;
        if (!TryString(data, ref offset, out var address) || address.Length == 0 || address[0] != '/')
            return false;
        string? text = null;
        double? number = null;
        var integer = false;
        if (offset < data.Length && data[offset] == (byte)',')
        {
            if (!TryString(data, ref offset, out var tags))
                return false;
            if (tags.Length > 1)
            {
                switch (tags[1])
                {
                    case 'i':
                        if (!TryInt(data, ref offset, out var whole))
                            return false;
                        number = whole;
                        integer = true;
                        break;
                    case 'f':
                        if (!TryFloat(data, ref offset, out var fraction))
                            return false;
                        number = fraction;
                        break;
                    case 's':
                        if (!TryString(data, ref offset, out var word))
                            return false;
                        text = word;
                        break;
                    case 'T':
                    case 'I':
                        break;
                    case 'F':
                        number = 0;
                        break;
                    default:
                        return false;
                }
            }
        }
        message = new OscMessage { Address = address, Text = text, Number = number, Integer = integer };
        return true;
    }

    private static bool TryString(ReadOnlySpan<byte> data, ref int offset, out string value)
    {
        value = "";
        if (offset >= data.Length)
            return false;
        var start = offset;
        while (offset < data.Length && data[offset] != 0)
            offset++;
        if (offset >= data.Length)
            return false;
        value = Encoding.ASCII.GetString(data.Slice(start, offset - start));
        offset++;
        var padded = (offset + 3) & ~3;
        if (padded > data.Length)
            return false;
        offset = padded;
        return true;
    }

    private static bool TryInt(ReadOnlySpan<byte> data, ref int offset, out int value)
    {
        value = 0;
        if (offset + 4 > data.Length)
            return false;
        value = (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];
        offset += 4;
        return true;
    }

    private static bool TryFloat(ReadOnlySpan<byte> data, ref int offset, out float value)
    {
        value = 0;
        if (!TryInt(data, ref offset, out var bits))
            return false;
        value = BitConverter.Int32BitsToSingle(bits);
        return true;
    }

    private static void WriteString(List<byte> buffer, string value)
    {
        buffer.AddRange(Encoding.ASCII.GetBytes(value));
        buffer.Add(0);
        while (buffer.Count % 4 != 0)
            buffer.Add(0);
    }

    private static void WriteInt(List<byte> buffer, int value)
    {
        buffer.Add((byte)(value >> 24));
        buffer.Add((byte)(value >> 16));
        buffer.Add((byte)(value >> 8));
        buffer.Add((byte)value);
    }
}
