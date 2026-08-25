using Microsoft.EntityFrameworkCore;
using Shiw.Abp.BaseEntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace DredgeAI;

public static class DredgeAIBaseDbContextModelCreatingExtensions
{
    public static void ConfigureDredgeAIBase(
        this ModelBuilder builder, IShiwDbContextHandler handler)
    {
        Check.NotNull(builder, nameof(builder));

        // DictType — 字典类型（树形结构）
        // 以 Code 为业务唯一标识，以 FullCode 表示层级路径。
        builder.Entity<DictType>(b =>
        {
            b.ToTable(
                handler.TableNameHandler($"{DredgeAIBaseDbProperties.DbTablePrefix}{nameof(DictType)}"),
                DredgeAIBaseDbProperties.DbSchema);
            b.ConfigureByConvention(handler);

            b.Property(x => x.Name)
                .HasColumnName(handler.FieldNameHandler(nameof(DictType.Name)))
                .IsRequired()
                .HasMaxLength(DictTypeConsts.MaxNameLength)
                .HasComment("字典类型名称，同层级内唯一");

            b.Property(x => x.Code)
                .HasColumnName(handler.FieldNameHandler(nameof(DictType.Code)))
                .IsRequired()
                .HasMaxLength(DictTypeConsts.MaxCodeLength)
                .HasComment("字典类型编码（业务唯一标识），格式：{模块前缀}_{6位随机大写字母数字}，如 DEFAULT_A7K3X9");

            b.Property(x => x.FullCode)
                .HasColumnName(handler.FieldNameHandler(nameof(DictType.FullCode)))
                .IsRequired()
                .HasMaxLength(DictTypeConsts.MaxFullCodeLength)
                .HasComment("层级路径编码（点分4位数字自增），如 0001 / 0001.0002，表示树形层级关系");

            b.Property(x => x.ParentId)
                .HasColumnName(handler.FieldNameHandler(nameof(DictType.ParentId)))
                .HasComment("父类型ID，null 表示根节点");

            b.Property(x => x.ModuleCode)
                .HasColumnName(handler.FieldNameHandler(nameof(DictType.ModuleCode)))
                .HasMaxLength(DictTypeConsts.MaxModuleCodeLength)
                .HasComment("绑定功能模块编号，用于 Code 编码前缀，null 时默认使用 DEFAULT");

            b.Property(x => x.Sort)
                .HasColumnName(handler.FieldNameHandler(nameof(DictType.Sort)))
                .IsRequired()
                .HasComment("排序号，同级内按此升序排列");

            b.Property(x => x.Remark)
                .HasColumnName(handler.FieldNameHandler(nameof(DictType.Remark)))
                .HasMaxLength(DictTypeConsts.MaxRemarkLength)
                .HasComment("备注");

            b.Property(x => x.IsStatic)
                .HasColumnName(handler.FieldNameHandler(nameof(DictType.IsStatic)))
                .IsRequired()
                .HasDefaultValue(false)
                .HasComment("是否静态数据，静态数据不允许修改和删除");

            var dictTypeCodeIndex = b.HasIndex(x => x.Code).IsUnique();
            var dictTypeNameIndex = b.HasIndex(x => x.Name).IsUnique();
            var dictTypeFullCodeIndex = b.HasIndex(x => x.FullCode).IsUnique();
            b.HasIndex(x => x.ParentId);

            if (typeof(ISoftDelete).IsAssignableFrom(typeof(DictType)))
            {
                var filter = $"{handler.FieldNameHandler(nameof(ISoftDelete.IsDeleted))} = false";
                dictTypeCodeIndex.HasFilter(filter);
                dictTypeNameIndex.HasFilter(filter);
                dictTypeFullCodeIndex.HasFilter(filter);
            }
        });

        // DictData — 字典数据值（同类型内层级结构）
        // 以 Code 为业务唯一标识，以 TypeId+ParentId+Value 组合唯一。
        builder.Entity<DictData>(b =>
        {
            b.ToTable(
                handler.TableNameHandler($"{DredgeAIBaseDbProperties.DbTablePrefix}{nameof(DictData)}"),
                DredgeAIBaseDbProperties.DbSchema);
            b.ConfigureByConvention(handler);

            b.Property(x => x.TypeId)
                .HasColumnName(handler.FieldNameHandler(nameof(DictData.TypeId)))
                .IsRequired()
                .HasComment("所属字典类型ID，外键关联 DictType");

            b.Property(x => x.ParentId)
                .HasColumnName(handler.FieldNameHandler(nameof(DictData.ParentId)))
                .HasComment("父数据值ID，null 表示根数据值，同类型内构建层级");

            b.Property(x => x.Code)
                .HasColumnName(handler.FieldNameHandler(nameof(DictData.Code)))
                .IsRequired()
                .HasMaxLength(DictDataConsts.MaxCodeLength)
                .HasComment("数据值编码（业务唯一标识），格式：{TypeCode}_{8位随机大写字母数字}，如 DEFAULT_GENDER_A7K3X9M2");

            b.Property(x => x.Name)
                .HasColumnName(handler.FieldNameHandler(nameof(DictData.Name)))
                .IsRequired()
                .HasMaxLength(DictDataConsts.MaxNameLength)
                .HasComment("数据值显示名称");

            b.Property(x => x.Value)
                .HasColumnName(handler.FieldNameHandler(nameof(DictData.Value)))
                .IsRequired()
                .HasMaxLength(DictDataConsts.MaxValueLength)
                .HasComment("数据值，同类型同层级内唯一");

            b.Property(x => x.Sort)
                .HasColumnName(handler.FieldNameHandler(nameof(DictData.Sort)))
                .IsRequired()
                .HasComment("排序号，同层级内按此升序排列");

            b.Property(x => x.IsEnabled)
                .HasColumnName(handler.FieldNameHandler(nameof(DictData.IsEnabled)))
                .IsRequired()
                .HasComment("是否启用（true:启用 false:禁用），禁用后不在下拉选项中展示");

            b.Property(x => x.Remark)
                .HasColumnName(handler.FieldNameHandler(nameof(DictData.Remark)))
                .HasMaxLength(DictDataConsts.MaxRemarkLength)
                .HasComment("备注");

            b.Property(x => x.IsStatic)
                .HasColumnName(handler.FieldNameHandler(nameof(DictData.IsStatic)))
                .IsRequired()
                .HasDefaultValue(false)
                .HasComment("是否静态数据，静态数据不允许修改和删除");

            // ExtraProperties 由 ConfigureByConvention 自动处理为 JSON 列，存储扩展属性（如 Extend、ExtendDescribe）

            var dictDataCodeIndex = b.HasIndex(x => x.Code).IsUnique();
            var dictDataValueIndex = b.HasIndex(x => new { x.TypeId, x.ParentId, x.Value }).IsUnique();
            b.HasIndex(x => x.TypeId);
            b.HasIndex(x => x.ParentId);

            if (typeof(ISoftDelete).IsAssignableFrom(typeof(DictData)))
            {
                var filter = $"{handler.FieldNameHandler(nameof(ISoftDelete.IsDeleted))} = false";
                dictDataCodeIndex.HasFilter(filter);
                dictDataValueIndex.HasFilter(filter);
            }
        });

        // MenuInfo — 菜单管理（树形结构）
        builder.Entity<MenuInfo>(b =>
        {
            b.ToTable(
                handler.TableNameHandler($"{DredgeAIBaseDbProperties.DbTablePrefix}{nameof(MenuInfo)}"),
                DredgeAIBaseDbProperties.DbSchema);
            b.ConfigureByConvention(handler);

            b.Property(x => x.ParentId)
                .HasColumnName(handler.FieldNameHandler(nameof(MenuInfo.ParentId)))
                .HasComment("上级菜单ID");
            b.Property(x => x.Type)
                .HasColumnName(handler.FieldNameHandler(nameof(MenuInfo.Type)))
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired()
                .HasComment("菜单类型（Directory=目录 Menu=菜单 Button=按钮）");
            b.Property(x => x.Name)
                .HasColumnName(handler.FieldNameHandler(nameof(MenuInfo.Name)))
                .IsRequired()
                .HasMaxLength(MenuInfoConsts.MaxNameLength)
                .HasComment("路由名称");
            b.Property(x => x.Title)
                .HasColumnName(handler.FieldNameHandler(nameof(MenuInfo.Title)))
                .IsRequired()
                .HasMaxLength(MenuInfoConsts.MaxTitleLength)
                .HasComment("显示标题");
            b.Property(x => x.ComponentPath)
                .HasColumnName(handler.FieldNameHandler(nameof(MenuInfo.ComponentPath)))
                .HasMaxLength(MenuInfoConsts.MaxComponentPathLength)
                .HasComment("前端组件路径");
            b.Property(x => x.RoutePath)
                .HasColumnName(handler.FieldNameHandler(nameof(MenuInfo.RoutePath)))
                .HasMaxLength(MenuInfoConsts.MaxRoutePathLength)
                .HasComment("路由路径");
            b.Property(x => x.RedirectPath)
                .HasColumnName(handler.FieldNameHandler(nameof(MenuInfo.RedirectPath)))
                .HasMaxLength(MenuInfoConsts.MaxRedirectPathLength)
                .HasComment("重定向地址");
            b.Property(x => x.Icon)
                .HasColumnName(handler.FieldNameHandler(nameof(MenuInfo.Icon)))
                .HasMaxLength(MenuInfoConsts.MaxIconLength)
                .HasComment("图标");
            b.Property(x => x.IconType)
                .HasColumnName(handler.FieldNameHandler(nameof(MenuInfo.IconType)))
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired()
                .HasComment("图标类型（Ali/Element/FontAwesome）");
            b.Property(x => x.RouteType)
                .HasColumnName(handler.FieldNameHandler(nameof(MenuInfo.RouteType)))
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired()
                .HasComment("路由类型（Default/IframeUrl/OpenWindow）");
            b.Property(x => x.PermissionCode)
                .HasColumnName(handler.FieldNameHandler(nameof(MenuInfo.PermissionCode)))
                .IsRequired()
                .HasMaxLength(MenuInfoConsts.MaxPermissionCodeLength)
                .HasComment("权限编码");
            b.Property(x => x.SortId)
                .HasColumnName(handler.FieldNameHandler(nameof(MenuInfo.SortId)))
                .IsRequired()
                .HasComment("排序号，同级内按此升序排列");
            b.Property(x => x.IsEnabled)
                .HasColumnName(handler.FieldNameHandler(nameof(MenuInfo.IsEnabled)))
                .IsRequired()
                .HasComment("是否启用");
            b.Property(x => x.IsCache)
                .HasColumnName(handler.FieldNameHandler(nameof(MenuInfo.IsCache)))
                .IsRequired()
                .HasComment("是否缓存");
            b.Property(x => x.IsFixed)
                .HasColumnName(handler.FieldNameHandler(nameof(MenuInfo.IsFixed)))
                .IsRequired()
                .HasComment("是否固定");
            b.Property(x => x.IsHidden)
                .HasColumnName(handler.FieldNameHandler(nameof(MenuInfo.IsHidden)))
                .IsRequired()
                .HasComment("是否隐藏");
            b.Property(x => x.IsStatic)
                .HasColumnName(handler.FieldNameHandler(nameof(MenuInfo.IsStatic)))
                .IsRequired()
                .HasComment("系统菜单保护标识");
            b.Property(x => x.Remark)
                .HasColumnName(handler.FieldNameHandler(nameof(MenuInfo.Remark)))
                .HasMaxLength(MenuInfoConsts.MaxRemarkLength)
                .HasComment("备注");

            var menuNameIndex = b.HasIndex(x => new { x.Name, x.ParentId }).IsUnique();
            b.HasIndex(x => x.Type);
            b.HasIndex(x => x.ParentId);

            if (typeof(ISoftDelete).IsAssignableFrom(typeof(MenuInfo)))
            {
                var filter = $"{handler.FieldNameHandler(nameof(ISoftDelete.IsDeleted))} = false";
                menuNameIndex.HasFilter(filter);
            }
        });

        // UserExtension — IdentityUser 业务扩展（1:1 关联）
        builder.Entity<IdentityUserExtension>(b =>
        {
            b.ToTable(
                handler.TableNameHandler($"{DredgeAIBaseDbProperties.DbTablePrefix}{nameof(IdentityUserExtension)}"),
                DredgeAIBaseDbProperties.DbSchema);
            b.ConfigureByConvention(handler);

            b.Property(x => x.UserId)
                .HasColumnName(handler.FieldNameHandler(nameof(IdentityUserExtension.UserId)))
                .IsRequired()
                .HasComment("关联的 IdentityUser ID");

            b.Property(x => x.ExpireTime)
                .HasColumnName(handler.FieldNameHandler(nameof(IdentityUserExtension.ExpireTime)))
                .HasComment("账号过期时间，null 表示永不过期");

            b.HasIndex(x => x.UserId);
        });
    }
}
