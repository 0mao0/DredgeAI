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
public class ProxyClusterAppService : ApplicationService, IProxyClusterAppService
{
    private readonly IRepository<ProxyCluster, Guid> _repository;
    private readonly ProxyConfigManager _configManager;
    private readonly DatabaseProxyConfigProvider _configProvider;

    public ProxyClusterAppService(
        IRepository<ProxyCluster, Guid> repository,
        ProxyConfigManager configManager,
        DatabaseProxyConfigProvider configProvider)
    {
        _repository = repository;
        _configManager = configManager;
        _configProvider = configProvider;
    }

    public async Task<ListResultDto<ProxyClusterDto>> GetListAsync()
    {
        var items = await _repository.GetListAsync();
        return new ListResultDto<ProxyClusterDto>(
            items.Select(x => ObjectMapper.Map<ProxyCluster, ProxyClusterDto>(x)).ToList());
    }

    public async Task<ProxyClusterDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return ObjectMapper.Map<ProxyCluster, ProxyClusterDto>(entity);
    }

    public async Task<ProxyClusterDto> CreateAsync(ProxyClusterCreateUpdateDto input)
    {
        var config = ObjectMapper.Map<ProxyClusterCreateUpdateDto, ClusterConfig>(input);
        var entity = new ProxyCluster(GuidGenerator.Create(), config, input.Description);
        if (!input.IsEnabled)
        {
            entity.Disable();
        }

        await _configManager.ValidateClusterAsync(entity);
        await _repository.InsertAsync(entity, autoSave: true);
        await _configProvider.ReloadAsync();
        return ObjectMapper.Map<ProxyCluster, ProxyClusterDto>(entity);
    }

    public async Task<ProxyClusterDto> UpdateAsync(Guid id, ProxyClusterCreateUpdateDto input)
    {
        var entity = await _repository.GetAsync(id);
        var config = ObjectMapper.Map<ProxyClusterCreateUpdateDto, ClusterConfig>(input);
        entity.Update(config, input.Description);
        if (input.IsEnabled)
        {
            entity.Enable();
        }
        else
        {
            entity.Disable();
        }

        await _configManager.ValidateClusterAsync(entity, excludeId: id);
        await _repository.UpdateAsync(entity, autoSave: true);
        await _configProvider.ReloadAsync();
        return ObjectMapper.Map<ProxyCluster, ProxyClusterDto>(entity);
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        await _configManager.ValidateClusterDeleteAsync(entity.ClusterId);
        await _repository.DeleteAsync(entity, autoSave: true);
        await _configProvider.ReloadAsync();
    }
}
