using System.Text;
using TinyPinyin;
using Volo.Abp.DependencyInjection;

namespace DredgeAI;

public class DictCodeGenerator : ITransientDependency
{
    private static readonly char[] _chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789".ToCharArray();
    private readonly Random _random = new();

    public string GenerateRandomCode(int length)
    {
        var sb = new StringBuilder(length);
        for (var i = 0; i < length; i++)
            sb.Append(_chars[_random.Next(_chars.Length)]);
        return sb.ToString();
    }

    public string GetPinyinInitials(string text)
    {
        var sb = new StringBuilder();
        foreach (var ch in text)
        {
            if (ch >= 0x4e00 && ch <= 0x9fff)
            {
                var pinyin = PinyinHelper.GetPinyin(ch);
                if (pinyin.Length > 0)
                    sb.Append(char.ToUpperInvariant(pinyin[0]));
            }
            else if (char.IsLetterOrDigit(ch))
            {
                sb.Append(char.ToUpperInvariant(ch));
            }
        }
        return sb.ToString();
    }

    public string GetPinyinFull(string text)
    {
        var sb = new StringBuilder();
        foreach (var ch in text)
        {
            if (ch >= 0x4e00 && ch <= 0x9fff)
            {
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(PinyinHelper.GetPinyin(ch));
            }
            else
            {
                sb.Append(ch);
            }
        }
        return sb.ToString().ToUpperInvariant();
    }
}
