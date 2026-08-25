namespace DredgeAI;

public class MaskIdCardConverter : MaskConverter
{
    public MaskIdCardConverter()
        : this(6, 10)
    {
    }

    public MaskIdCardConverter(byte leftLength, byte maskLength) : base(leftLength, maskLength)
    {
    }
}
