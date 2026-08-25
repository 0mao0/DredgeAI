using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;

namespace DredgeAI;

[ConnectionStringName(DredgeAIBaseDbProperties.ConnectionStringName)]
public interface IDredgeAIBaseDbContext : IEfCoreDbContext
{
    DbSet<DictType> DictTypes { get; set; }
    DbSet<DictData> DictDatas { get; set; }
    DbSet<MenuInfo> Menus { get; set; }
    DbSet<IdentityUserExtension> UserExtensions { get; set; }
}
