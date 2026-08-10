using System.Threading.Tasks;

namespace DredgeAI.BidCompare.Data;

public interface IBidCompareDbSchemaMigrator
{
    Task MigrateAsync();
}
