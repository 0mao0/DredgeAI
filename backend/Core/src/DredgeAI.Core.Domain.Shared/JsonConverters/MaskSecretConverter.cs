namespace DredgeAI;

public class MaskSecretConverter : MaskConverter
{
    public MaskSecretConverter()
        : this(6, 18)
    {
    }

    public MaskSecretConverter(byte leftLength, byte maskLength) : base(leftLength, maskLength)
    {
    }
}
