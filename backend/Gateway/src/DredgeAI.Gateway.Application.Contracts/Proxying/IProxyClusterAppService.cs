using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace DredgeAI.Gateway.Proxying;

public interface IProxyClusterAppService : IApplicationService
{
    Task<ListResultDto<ProxyClusterDto>> GetListAsync();

    Task<ProxyClusterDto> GetAsync(Guid id);

    Task<ProxyClusterDto> CreateAsync(ProxyClusterCreateUpdateDto input);

    Task<ProxyClusterDto> UpdateAsync(Guid id, ProxyClusterCreateUpdateDto input);

    Task DeleteAsync(Guid id);
}
