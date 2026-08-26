using System;
using System.Collections.Generic;
using System.Linq;
using DredgeAI.BidCompare.Applications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Users;

namespace DredgeAI.BidCompare.Controllers;

/// <summary>
/// 应用展示顺序服务：
/// - admin 维护全局默认顺序（上移/下移）；
/// - 用户维护个性化顺序（user-web 拖拽/上移下移后写入）；
/// 前端合并规则：个性化优先，未个性化用户按默认顺序，新应用按默认顺序稳定插入。
/// </summary>
[Route("api/admin/app-order")]
[Authorize]
public class ApplicationOrderController : AbpControllerBase
{
    private readonly ApplicationOrderStore _store;
    private readonly ICurrentUser _currentUser;

    public ApplicationOrderController(ApplicationOrderStore store, ICurrentUser currentUser)
    {
        _store = store;
        _currentUser = currentUser;
    }

    /// <summary>GET /api/app-order 获取 admin 全局默认顺序（应用 id 列表）。</summary>
    [HttpGet]
    public ApplicationOrderResult Get()
    {
        var (appIds, subOrders) = _store.GetDefaultOrder();
        return new ApplicationOrderResult(appIds, subOrders);
    }

    /// <summary>POST /api/app-order/move 上移/下移一个应用，返回重排后的默认顺序。</summary>
    [HttpPost("move")]
    public ApplicationOrderResult Move([FromBody] MoveApplicationOrderInput input)
    {
        if (string.IsNullOrWhiteSpace(input.AppId))
        {
            throw new BusinessException("AppOrder:InvalidAppId", "缺少应用 id");
        }

        if (input.Direction is not ("up" or "down"))
        {
            throw new BusinessException("AppOrder:InvalidDirection", "direction 只能是 up 或 down");
        }

        var (appIds, subOrders) = _store.Move(input.AppId, input.Direction == "up");
        return new ApplicationOrderResult(appIds, subOrders);
    }

    /// <summary>POST /api/app-order/seed 合并默认顺序：保留已有位置，仅追加新出现的应用/子应用 id（admin 前端每次加载时调用，幂等）。</summary>
    [HttpPost("seed")]
    public ApplicationOrderResult Seed([FromBody] SeedApplicationOrderInput input)
    {
        var ids = (input.AppIds ?? new List<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToArray();
        var subOrders = (input.SubOrders ?? new Dictionary<string, List<string>>())
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .ToDictionary(
                x => x.Key,
                x => (x.Value ?? new List<string>())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct()
                    .ToArray());
        _store.MergeAdminOrder(ids, subOrders);
        var (appIds, resultSubOrders) = _store.GetDefaultOrder();
        return new ApplicationOrderResult(appIds, resultSubOrders);
    }

    /// <summary>GET /api/app-order/user 获取当前用户的个性化顺序（route 列表；未个性化返回 null）。</summary>
    [HttpGet("user")]
    public UserApplicationOrderResult GetUserOrder()
        => new(_store.GetUserOrder(CurrentUserId()));

    /// <summary>PUT /api/app-order/user 保存当前用户的个性化顺序（route 列表）。</summary>
    [HttpPut("user")]
    public UserApplicationOrderResult SetUserOrder([FromBody] SetUserApplicationOrderInput input)
    {
        var routes = (input.RouteIds ?? new List<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToArray();
        _store.SetUserOrder(CurrentUserId(), routes);
        return new UserApplicationOrderResult(routes);
    }

    /// <summary>POST /api/app-order/reset 清空所有用户的个性化顺序（管理员显式动作）。</summary>
    [HttpPost("reset")]
    public ResetUserOrdersResult Reset()
        => new(_store.ResetUserOrders());

    private Guid CurrentUserId()
        => _currentUser.Id ?? Guid.Empty;
}

public class ApplicationOrderResult
{
    public ApplicationOrderResult(string[] appIds, Dictionary<string, string[]> subOrders)
    {
        AppIds = appIds;
        SubOrders = subOrders;
    }

    public string[] AppIds { get; set; }

    /// <summary>各母项应用下的子应用默认顺序（母项 id → 子应用 id 列表）。</summary>
    public Dictionary<string, string[]> SubOrders { get; set; }
}

public class UserApplicationOrderResult
{
    public UserApplicationOrderResult(string[]? routeIds)
    {
        RouteIds = routeIds;
    }

    public string[]? RouteIds { get; set; }
}

public class ResetUserOrdersResult
{
    public ResetUserOrdersResult(int count)
    {
        Count = count;
    }

    public int Count { get; set; }
}

public class MoveApplicationOrderInput
{
    public string AppId { get; set; } = string.Empty;

    public string Direction { get; set; } = string.Empty;
}

public class SetUserApplicationOrderInput
{
    public List<string>? RouteIds { get; set; }
}

public class SeedApplicationOrderInput
{
    public List<string>? AppIds { get; set; }

    public Dictionary<string, List<string>>? SubOrders { get; set; }
}
