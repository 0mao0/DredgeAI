using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DredgeAI.BidCompare.Applications;

/// <summary>admin 发布管理下的子应用。</summary>
public class CatalogSubApp
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string ParentAppId { get; set; } = string.Empty;

    public string ParentAppName { get; set; } = string.Empty;

    public string Route { get; set; } = string.Empty;

    public string Icon { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string Status { get; set; } = "已发布";

    public string? Scope { get; set; }

    public string? Description { get; set; }
}

/// <summary>应用目录条目（admin 发布管理 / user 端应用列表共用同一份数据）。</summary>
public class CatalogApp
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string Manager { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string Status { get; set; } = "运营中";

    public int UserCount { get; set; }

    public int ApiCalls { get; set; }

    public string CreatedAt { get; set; } = string.Empty;

    public string Icon { get; set; } = string.Empty;

    /// <summary>admin 侧路由（可选）。</summary>
    public string? Route { get; set; }

    /// <summary>user-web 侧边栏路由（仅无子应用的主应用使用；子应用用 sub.Route）。</summary>
    public string? UserRoute { get; set; }

    public string? Scope { get; set; }

    public List<CatalogSubApp>? SubApps { get; set; }
}

/// <summary>user-web 应用卡片（由目录按发布状态推导）。</summary>
public class UserAppCardDto
{
    public string Id { get; set; } = string.Empty;

    public string? ParentAppId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string Icon { get; set; } = string.Empty;

    public string Status { get; set; } = "已授权";

    public string Route { get; set; } = string.Empty;

    public string? Version { get; set; }

    public bool Pinned { get; set; }
}

/// <summary>
/// 应用目录存储（JSON 文件持久化，后端重启不丢）：
/// - admin 发布管理读写同一份目录（发布/下架、分类、图标）；
/// - user-web 应用列表由目录按发布状态实时推导；
/// 首次运行以内置种子目录（Resources/seed-app-catalog.json）初始化，落盘 App_Data/app-catalog.json。
/// </summary>
public class ApplicationCatalogStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly object _lock = new();
    private readonly string _filePath;
    private readonly string _seedPath;
    private List<CatalogApp>? _apps;
    private bool _loaded;

    public ApplicationCatalogStore(string filePath, string seedPath)
    {
        _filePath = filePath;
        _seedPath = seedPath;
    }

    public List<CatalogApp> GetAll()
    {
        lock (_lock)
        {
            EnsureLoaded();
            return new List<CatalogApp>(_apps ?? new List<CatalogApp>());
        }
    }

    /// <summary>按发布状态推导 user-web 可见应用列表（与旧 mock 推导逻辑一致）。</summary>
    public List<UserAppCardDto> GetUserApps()
    {
        lock (_lock)
        {
            EnsureLoaded();
            var cards = new List<UserAppCardDto>();
            foreach (var app in _apps ?? new List<CatalogApp>())
            {
                if (app.SubApps is { Count: > 0 })
                {
                    foreach (var sub in app.SubApps)
                    {
                        if (sub.Status != "已发布")
                        {
                            continue;
                        }

                        cards.Add(new UserAppCardDto
                        {
                            Id = sub.Id,
                            ParentAppId = app.Id,
                            Title = sub.Name,
                            Description = string.IsNullOrWhiteSpace(sub.Description) ? $"{app.Name}的子应用" : sub.Description,
                            Category = sub.Category,
                            Icon = sub.Icon,
                            Status = "已授权",
                            Route = sub.Route,
                            Version = sub.Version,
                            Pinned = false,
                        });
                    }
                }
                else
                {
                    cards.Add(new UserAppCardDto
                    {
                        Id = app.Id,
                        Title = app.Name,
                        Description = $"{app.Name}应用模块",
                        Category = app.Category,
                        Icon = app.Icon,
                        Status = app.Status == "已下架" ? "已下架" : "已授权",
                        Route = app.UserRoute ?? string.Empty,
                        Version = app.Version,
                        Pinned = false,
                    });
                }
            }

            return cards;
        }
    }

    public List<CategoryConfigDto> GetCategories()
        => new()
        {
            new CategoryConfigDto { Name = "通用", Color = "blue" },
            new CategoryConfigDto { Name = "经营", Color = "green" },
            new CategoryConfigDto { Name = "设计", Color = "purple" },
            new CategoryConfigDto { Name = "施工", Color = "gold" },
        };

    public bool SetAppStatus(string appId, string status)
    {
        lock (_lock)
        {
            EnsureLoaded();
            var app = FindApp(appId);
            if (app == null)
            {
                return false;
            }

            app.Status = status;
            Save();
            return true;
        }
    }

    public bool SetSubStatus(string subId, string status)
    {
        lock (_lock)
        {
            EnsureLoaded();
            var sub = FindSub(subId);
            if (sub == null)
            {
                return false;
            }

            sub.Status = status;
            Save();
            return true;
        }
    }

    public bool SetCategory(string id, string category)
    {
        lock (_lock)
        {
            EnsureLoaded();
            var app = FindApp(id);
            if (app != null)
            {
                app.Category = category;
                Save();
                return true;
            }

            var sub = FindSub(id);
            if (sub != null)
            {
                sub.Category = category;
                Save();
                return true;
            }

            return false;
        }
    }

    public bool SetIcon(string id, string icon)
    {
        lock (_lock)
        {
            EnsureLoaded();
            var app = FindApp(id);
            if (app != null)
            {
                app.Icon = icon;
                Save();
                return true;
            }

            var sub = FindSub(id);
            if (sub != null)
            {
                sub.Icon = icon;
                Save();
                return true;
            }

            return false;
        }
    }

    private CatalogApp? FindApp(string id)
        => (_apps ?? new List<CatalogApp>()).FirstOrDefault(a => a.Id == id);

    private CatalogSubApp? FindSub(string id)
        => (_apps ?? new List<CatalogApp>())
            .SelectMany(a => a.SubApps ?? new List<CatalogSubApp>())
            .FirstOrDefault(s => s.Id == id);

    private void EnsureLoaded()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                var data = JsonSerializer.Deserialize<List<CatalogApp>>(json, Options);
                if (data != null)
                {
                    _apps = data;
                    return;
                }
            }
        }
        catch
        {
            // 文件损坏时回退到种子目录
        }

        try
        {
            var seedJson = File.ReadAllText(_seedPath);
            _apps = JsonSerializer.Deserialize<List<CatalogApp>>(seedJson, Options) ?? new List<CatalogApp>();
            Save();
        }
        catch
        {
            _apps = new List<CatalogApp>();
        }
    }

    private void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(_filePath, JsonSerializer.Serialize(_apps ?? new List<CatalogApp>(), Options));
        }
        catch
        {
            // 持久化失败不影响运行
        }
    }
}

/// <summary>应用分类配置（名称 + 标签色）。</summary>
public class CategoryConfigDto
{
    public string Name { get; set; } = string.Empty;

    public string Color { get; set; } = string.Empty;
}
