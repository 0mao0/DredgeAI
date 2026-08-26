using System;
using System.Collections.Generic;

namespace DredgeAI.BidCompare.Evidences;

/// <summary>spec §6.1 locations: { docId, blockIds[] }[]。</summary>
public class EvidenceLocationDto
{
    public Guid DocId { get; set; }

    public List<string> BlockIds { get; set; } = new();
}
