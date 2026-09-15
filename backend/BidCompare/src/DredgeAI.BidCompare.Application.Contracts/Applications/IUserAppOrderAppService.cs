using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace DredgeAI.BidCompare.Applications;

public interface IUserAppOrderAppService : IApplicationService
{
    Task<UserApplicationOrderResult> GetUserOrderAsync();

    Task<UserApplicationOrderResult> SetUserOrderAsync(SetUserApplicationOrderInput input);

    Task<ResetUserOrdersResult> ResetUserOrdersAsync();
}
