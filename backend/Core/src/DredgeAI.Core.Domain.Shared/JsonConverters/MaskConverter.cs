using System.Text.Json;
using System.Text.Json.Serialization;

namespace DredgeAI;

/// <summary>
/// 字段脱敏
/// </summary>
public class MaskConverter : JsonConverter<string>
{
    protected readonly byte LeftLength;
    protected readonly byte MaskLength;
    protected readonly char MaskChar;

    public MaskConverter()
    {
        LeftLength = 4;
        MaskLength = 4;
        MaskChar = '*';
    }

    public MaskConverter(byte leftLength)
        : this()
    {
        LeftLength = leftLength;
    }

    public MaskConverter(byte leftLength, byte maskLength)
        : this(leftLength)
    {
        MaskLength = maskLength;
        MaskChar = '*';
    }

    public MaskConverter(byte leftLength, byte maskLength, char maskChar)
        : this(leftLength, maskLength)
    {
        MaskChar = maskChar;
    }

    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.GetString();
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            writer.WriteStringValue(value);
        }
        else
        {
            var length = value.Length;
            if (length <= LeftLength)
            {
                writer.WriteStringValue(value);
            }
            else
            {
                var left = value.Substring(0, LeftLength);
                var mask = new string(MaskChar, MaskLength);
                var right = string.Empty;
                if (length > LeftLength + MaskLength)
                    right = value.Substring(LeftLength + MaskLength);
                writer.WriteStringValue($"{left}{mask}{right}");
            }
        }
    }
}
