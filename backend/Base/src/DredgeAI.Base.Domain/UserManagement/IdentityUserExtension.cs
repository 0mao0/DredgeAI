using Volo.Abp.Domain.Entities.Auditing;

namespace DredgeAI;

public class IdentityUserExtension : FullAuditedAggregateRoot<Guid>
{
    public Guid UserId { get; private set; }
    public DateTime? ExpireTime { get; private set; }

    protected IdentityUserExtension() { }

    internal IdentityUserExtension(Guid id, Guid userId, DateTime? expireTime) : base(id)
    {
        UserId = userId;
        ExpireTime = expireTime;
    }

    internal void SetExpireTime(DateTime? expireTime) => ExpireTime = expireTime;
}
