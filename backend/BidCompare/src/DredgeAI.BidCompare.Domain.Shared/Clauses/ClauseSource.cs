namespace DredgeAI.BidCompare.Clauses;

// spec §6.1: 'extracted'|'manual'|'template'
public enum ClauseSource : byte
{
    Extracted = 0,
    Manual = 1,
    Template = 2
}
