using DredgeAI.OrganizationManagement;
using DredgeAI.Common;
using DredgeAI.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Identity;

namespace DredgeAI.OrganizationManagement;

public class OrganizationUnitAppService : DredgeAIBaseAppService, IOrganizationUnitAppService
{
    private readonly IMyOrganizationUnitRepository _repository;
    private readonly OrganizationUnitManager _organizationUnitManager;

    public OrganizationUnitAppService(
        IMyOrganizationUnitRepository repository,
        OrganizationUnitManager organizationUnitManager)
    {
        _repository = repository;
        _organizationUnitManager = organizationUnitManager;
    }

    public async Task<List<OrganizationUnitDto>> GetTreeAsync()
    {
        var all = await _repository.GetAllListAsync();

        var roots = all.Where(ou => ou.ParentId == null)
                       .OrderBy(ou => ou.DisplayName)
                       .ToList();

        return roots.Select(r => BuildTreeNode(r, all)).ToList();
    }

    public async Task<List<AndtTreeDto>> GetAndtTreeAsync()
    {
        var all = await _repository.GetAllListAsync();

        var roots = all.Where(ou => ou.ParentId == null)
                       .OrderBy(ou => ou.DisplayName)
                       .ToList();

        return roots.Select(r => BuildAndtTreeNode(r, all)).ToList();
    }

    public async Task<OrganizationUnitDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return MapToDto(entity);
    }

    public async Task<PagedResultDto<OrganizationUnitDto>> GetListAsync(GetOrganizationUnitListInput input)
    {
        var items = await _repository.GetPagedListAsync(
            input.Keyword, input.SkipCount, input.MaxResultCount);

        var totalCount = await _repository.GetCountAsync(input.Keyword);

        return new PagedResultDto<OrganizationUnitDto>(
            totalCount,
            items.Select(MapToDto).ToList());
    }

    public async Task<OrganizationUnitDto> CreateAsync(CreateOrganizationUnitDto input)
    {
        var entity = new OrganizationUnit(
            GuidGenerator.Create(),
            input.DisplayName,
            input.ParentId,
            CurrentTenant.Id);

        await _organizationUnitManager.CreateAsync(entity);

        return MapToDto(entity);
    }

    public async Task<OrganizationUnitDto> UpdateAsync(Guid id, UpdateOrganizationUnitDto input)
    {
        if (input.ParentId.HasValue)
        {
            var entity = await _repository.GetAsync(id);
            if (input.ParentId != entity.ParentId)
            {
                await EnsureNoCircularReferenceAsync(id, input.ParentId.Value);
                await _organizationUnitManager.MoveAsync(id, input.ParentId);
            }
        }

        if (!string.IsNullOrWhiteSpace(input.DisplayName))
        {
            var entity = await _repository.GetAsync(id);
            if (input.DisplayName != entity.DisplayName)
            {
                entity.DisplayName = input.DisplayName;
                await _organizationUnitManager.UpdateAsync(entity);
            }
        }

        var result = await _repository.GetAsync(id);
        return MapToDto(result);
    }

    public async Task DeleteAsync(Guid id)
    {
        var children = await _repository.GetChildrenAsync(id);
        if (children.Count > 0)
        {
            throw new BusinessException(code: "OrganizationUnit:CannotDeleteWithChildren");
        }

        await _organizationUnitManager.DeleteAsync(id);
    }

    public async Task HierarchyVerificationAsync(Guid id, Guid? parentId)
    {
        if (parentId.HasValue)
        {
            await EnsureNoCircularReferenceAsync(id, parentId.Value);
        }
    }

    private OrganizationUnitDto BuildTreeNode(OrganizationUnit entity, List<OrganizationUnit> all)
    {
        var node = MapToDto(entity);
        node.Children = all.Where(ou => ou.ParentId == entity.Id)
                           .OrderBy(ou => ou.DisplayName)
                           .Select(child => BuildTreeNode(child, all))
                           .ToList();
        return node;
    }

    private AndtTreeDto BuildAndtTreeNode(OrganizationUnit entity, List<OrganizationUnit> all)
    {
        var node = new AndtTreeDto
        {
            Key = entity.Id.ToString(),
            ParentKey = entity.ParentId?.ToString() ?? string.Empty,
            Title = entity.DisplayName,
        };

        var children = all.Where(ou => ou.ParentId == entity.Id)
                          .OrderBy(ou => ou.DisplayName)
                          .ToList();

        foreach (var child in children)
        {
            node.AddChildren(BuildAndtTreeNode(child, all));
        }

        return node;
    }

    private static OrganizationUnitDto MapToDto(OrganizationUnit entity)
    {
        return new OrganizationUnitDto
        {
            Id = entity.Id,
            Code = entity.Code,
            DisplayName = entity.DisplayName,
            ParentId = entity.ParentId,
            CreationTime = entity.CreationTime,
        };
    }

    private async Task EnsureNoCircularReferenceAsync(Guid id, Guid parentId)
    {
        if (id == parentId)
        {
            throw new BusinessException(code: "OrganizationUnit:CircularReference");
        }

        var current = await _repository.FindAsync(parentId);
        while (current != null)
        {
            if (current.Id == id)
            {
                throw new BusinessException(code: "OrganizationUnit:CircularReference");
            }

            if (current.ParentId == null)
            {
                break;
            }

            current = await _repository.FindAsync(current.ParentId.Value);
        }
    }
}
