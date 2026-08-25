using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.EntityFrameworkCore;

namespace DredgeAI;

public class MenuInfoManager : DomainService
{
    private readonly IRepository<MenuInfo, Guid> _menuRepo;

    public MenuInfoManager(IRepository<MenuInfo, Guid> menuRepo)
    {
        _menuRepo = menuRepo;
    }

    public async Task EnsureNameUniqueAsync(string name, Guid? parentId, Guid? excludeId)
    {
        var query = await _menuRepo.GetQueryableAsync();
        var exists = await AsyncExecuter.AnyAsync(
            query.Where(m => m.Name == name && m.ParentId == parentId)
                 .WhereIf(excludeId.HasValue, m => m.Id != excludeId!.Value));
        if (exists)
            throw new BusinessException("DredgeAIBase:MenuNameAlreadyExists")
                .WithData("Name", name);
    }

    public async Task EnsureParentExistsAsync(Guid parentId)
    {
        var parent = await _menuRepo.FindAsync(parentId);
        if (parent == null)
            throw new BusinessException("DredgeAIBase:MenuParentNotFound")
                .WithData("ParentId", parentId);
    }

    public async Task ValidateNoCircularReferenceAsync(Guid menuId, Guid? newParentId)
    {
        if (newParentId == null)
            return;

        var currentId = newParentId.Value;
        while (true)
        {
            if (currentId == menuId)
                throw new BusinessException("DredgeAIBase:MenuCircularReference");

            var parent = await _menuRepo.FindAsync(currentId);
            if (parent == null || parent.ParentId == null)
                break;

            currentId = parent.ParentId.Value;
        }
    }

    public async Task ValidateNotStaticAsync(MenuInfo menuInfo)
    {
        if (menuInfo.IsStatic)
            throw new BusinessException("DredgeAIBase:MenuStaticNotAllowed");
    }
}
