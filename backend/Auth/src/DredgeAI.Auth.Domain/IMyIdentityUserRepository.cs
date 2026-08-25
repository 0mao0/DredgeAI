using Volo.Abp.Identity;

namespace DredgeAI;

public interface IMyIdentityUserRepository:IIdentityUserRepository
{
    /// <summary>
    /// 通过手机号精确查找用户。
    /// </summary>
    Task<IdentityUser?> FindByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default);

}