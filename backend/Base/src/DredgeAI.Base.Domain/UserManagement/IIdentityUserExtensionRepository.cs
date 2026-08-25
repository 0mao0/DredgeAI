using Volo.Abp.Domain.Repositories;

namespace DredgeAI;

public interface IIdentityUserExtensionRepository : IRepository<IdentityUserExtension, Guid>
{
    Task<List<IdentityUserExtension>> GetListByUserIdsAsync(List<Guid> userIds, CancellationToken cancellationToken = default);
}
