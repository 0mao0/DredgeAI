using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace DredgeAI.Gateway.Proxying;

public interface IProxyRouteAppService : IApplicationService
{
    Task<PagedResultDto<ProxyRouteDto>> GetListAsync(GetProxyRoutesInput input);

    Task<ProxyRouteDto> GetAsync(Guid id);

    Task<ProxyRouteDto> CreateAsync(ProxyRouteCreateUpdateDto input);

    Task<ProxyRouteDto> UpdateAsync(Guid id, ProxyRouteCreateUpdateDto input);

    Task DeleteAsync(Guid id);
}
