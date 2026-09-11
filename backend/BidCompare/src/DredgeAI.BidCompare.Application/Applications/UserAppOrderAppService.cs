using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;

namespace DredgeAI.BidCompare.Applications;

[RemoteService(false)]
public class UserAppOrderAppService : BidCompareAppService, IUserAppOrderAppService
{
    private readonly IRepository<AppOrder, Guid> _orderRepository;

    public UserAppOrderAppService(IRepository<AppOrder, Guid> orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public async Task<UserApplicationOrderResult> GetUserOrderAsync()
    {
        var uid = CurrentUser.Id ?? Guid.Empty;
        var rows = await _orderRepository.GetListAsync(x => x.Level == AppOrderLevel.User && x.UserId == uid);
        return new UserApplicationOrderResult
        {
            RouteIds = rows.Count == 0
                ? null
                : rows.OrderBy(x => x.SortOrder).Select(x => x.TargetId).ToList()
        };
    }

    public async Task<UserApplicationOrderResult> SetUserOrderAsync(SetUserApplicationOrderInput input)
    {
        var routes = (input.RouteIds ?? new System.Collections.Generic.List<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToArray();
        var uid = CurrentUser.Id ?? Guid.Empty;

        await _orderRepository.DeleteAsync(x => x.Level == AppOrderLevel.User && x.UserId == uid, autoSave: true);
        for (var i = 0; i < routes.Length; i++)
        {
            await _orderRepository.InsertAsync(
                new AppOrder(GuidGenerator.Create(), AppOrderLevel.User, uid, routes[i], i));
        }

        return new UserApplicationOrderResult { RouteIds = routes.ToList() };
    }

    public async Task<ResetUserOrdersResult> ResetUserOrdersAsync()
    {
        var all = await _orderRepository.GetListAsync(x => x.Level == AppOrderLevel.User);
        var count = all.Select(x => x.UserId).Distinct().Count();
        await _orderRepository.DeleteManyAsync(all, autoSave: true);
        return new ResetUserOrdersResult { Count = count };
    }
}
