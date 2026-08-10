namespace DredgeAI.BidCompare.Documents;

public enum DocumentParseStatus : byte
{
    Pending = 0,
    Parsing = 1,
    Parsed = 2,
    Failed = 3
}
