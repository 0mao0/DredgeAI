using System;
using Riok.Mapperly.Abstractions;
using Volo.Abp.Account;
using Volo.Abp.Identity;
using Volo.Abp.Mapperly;

namespace DredgeAI.Account;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
[MapExtraProperties]
public partial class IdentityUserToProfileDtoMapper : MapperBase<IdentityUser, ProfileDto>
{
    [MapperIgnoreTarget(nameof(ProfileDto.HasPassword))]
    public override partial ProfileDto Map(IdentityUser source);

    public override void Map(IdentityUser source, ProfileDto destination)
    {
        throw new InvalidOperationException();
    }
}
