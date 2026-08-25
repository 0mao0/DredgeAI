namespace DredgeAI;

public class MaskPhoneConverter : MaskConverter
{
    public MaskPhoneConverter()
        : this(3, 4)
    {
    }

    public MaskPhoneConverter(byte leftLength, byte maskLength) : base(leftLength, maskLength)
    {
    }
}
