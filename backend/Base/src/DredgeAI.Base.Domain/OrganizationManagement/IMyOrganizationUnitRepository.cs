using Volo.Abp.Identity;

namespace DredgeAI.OrganizationManagement;

public interface IMyOrganizationUnitRepository : IOrganizationUnitRepository
{
    Task<List<OrganizationUnit>> GetAllListAsync();
    Task<List<OrganizationUnit>> GetPagedListAsync(string? keyword, int skipCount, int maxResultCount);
    Task<int> GetCountAsync(string? keyword);
}
