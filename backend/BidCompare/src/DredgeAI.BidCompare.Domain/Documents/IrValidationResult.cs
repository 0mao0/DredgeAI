using System.Collections.Generic;
using System.Linq;

namespace DredgeAI.BidCompare.Documents;

public class IrValidationResult
{
    public IReadOnlyList<string> Errors { get; }

    public bool IsValid => Errors.Count == 0;

    public IrValidationResult(IEnumerable<string> errors)
    {
        Errors = errors.ToList();
    }
}
