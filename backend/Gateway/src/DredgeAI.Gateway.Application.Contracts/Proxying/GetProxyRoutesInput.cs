using Volo.Abp.Application.Dtos;

namespace DredgeAI.Gateway.Proxying;

public class GetProxyRoutesInput : PagedAndSortedResultRequestDto
{
    /// <summary>匹配 RouteId / ConfigJson。</summary>
    public string? Keyword { get; set; }

    public string? ClusterId { get; set; }
}
