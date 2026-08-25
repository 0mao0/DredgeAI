using System.Text.Json;

namespace DredgeAI;

public class MaskEmailConverter : MaskConverter
{
    public MaskEmailConverter()
        : base(4, 4)
    {
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.Contains("@"))
        {
            writer.WriteStringValue(value);
        }
        else
        {
            var emailUser = value.Split('@').FirstOrDefault();
            var emailHost = value.Split('@').LastOrDefault();
            if (string.IsNullOrWhiteSpace(emailUser) || string.IsNullOrWhiteSpace(emailHost))
            {
                writer.WriteStringValue(value);
                return;
            }

            var length = emailUser.Length;
            if (length <= LeftLength)
            {
                writer.WriteStringValue(emailUser);
            }
            else
            {
                var left = emailUser.Substring(0, LeftLength);
                var mask = new string(MaskChar, MaskLength);
                var right = string.Empty;
                if (length > LeftLength + MaskLength)
                    right = emailUser.Substring(LeftLength + MaskLength);
                writer.WriteStringValue($"{left}{mask}{right}@{emailHost}");
            }
        }
    }
}
