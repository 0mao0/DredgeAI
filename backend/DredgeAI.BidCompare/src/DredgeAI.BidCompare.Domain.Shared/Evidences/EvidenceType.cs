namespace DredgeAI.BidCompare.Evidences;

// spec §6.1: 'similarity'|'pricing'|'metadata'|'clause'|'indicator'
public enum EvidenceType : byte
{
    Similarity = 0,
    Pricing = 1,
    Metadata = 2,
    Clause = 3,
    Indicator = 4
}
