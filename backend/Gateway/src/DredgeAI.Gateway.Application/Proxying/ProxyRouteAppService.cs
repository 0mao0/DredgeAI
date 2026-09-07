using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Yarp.ReverseProxy.Configuration;

namespace DredgeAI.Gateway.Proxying;

[RemoteService(false)]
public class ProxyRouteAppService : ApplicationService, IProxyRouteAppService
{
    private readonly IRepository<ProxyRoute, Guid> _repository;
    private readonly ProxyConfigManager _configManager;
    private readonly DatabaseProxyConfigProvider _configProvider;

    public ProxyRouteAppService(
        IRepository<ProxyRoute, Guid> repository,
        ProxyConfigManager configManager,
        DatabaseProxyConfigProvider configProvider)
    {
        _repository = repository;
        _configManager = configManager;
        _configProvider = configProvider;
    }

    public async Task<PagedResultDto<ProxyRouteDto>> GetListAsync(GetProxyRoutesInput input)
    {
        var queryable = await _repository.GetQueryableAsync();
        queryable = queryable
            .WhereIf(!input.Keyword.IsNullOrWhiteSpace(),
                x => x.RouteId.Contains(input.Keyword!) || x.ConfigJson.Contains(input.Keyword!))
            .WhereIf(!input.ClusterId.IsNullOrWhiteSpace(), x => x.ClusterId == input.ClusterId);

        var totalCount = await AsyncExecuter.CountAsync(queryable);
        var items = await AsyncExecuter.ToListAsync(queryable
            .OrderBy(x => x.Order).ThenBy(x => x.RouteId)
            .PageBy(input.SkipCount, input.MaxResultCount));

        return new PagedResultDto<ProxyRouteDto>(
            totalCount,
            items.Select(x => ObjectMapper.Map<ProxyRoute, ProxyRouteDto>(x)).ToList());
    }

    public async Task<ProxyRouteDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return ObjectMapper.Map<ProxyRoute, ProxyRouteDto>(entity);
    }

    public async Task<ProxyRouteDto> CreateAsync(ProxyRouteCreateUpdateDto input)
    {
        var config = ObjectMapper.Map<ProxyRouteCreateUpdateDto, RouteConfig>(input);
        var entity = new ProxyRoute(GuidGenerator.Create(), config, input.Description);
        if (!input.IsEnabled)
        {
            entity.Disable();
        }

        await _configManager.ValidateNewRouteAsync(entity);
        await _repository.InsertAsync(entity, autoSave: true);
        await _configProvider.ReloadAsync();
        return ObjectMapper.Map<ProxyRoute, ProxyRouteDto>(entity);
    }

    public async Task<ProxyRouteDto> UpdateAsync(Guid id, ProxyRouteCreateUpdateDto input)
    {
        var entity = await _repository.GetAsync(id);
        var config = ObjectMapper.Map<ProxyRouteCreateUpdateDto, RouteConfig>(input);
        entity.Update(config, input.Description);
        if (input.IsEnabled)
        {
            entity.Enable();
        }
        else
        {
            entity.Disable();
        }

        await _configManager.ValidateUpdateRouteAsync(entity);
        await _repository.UpdateAsync(entity, autoSave: true);
        await _configProvider.ReloadAsync();
        return ObjectMapper.Map<ProxyRoute, ProxyRouteDto>(entity);
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        await _repository.DeleteAsync(entity, autoSave: true);
        await _configProvider.ReloadAsync();
    }

}
