using System;
using System.Threading.Tasks;
using DredgeAI.BidCompare.AI;
using Xunit;

namespace DredgeAI.BidCompare.AI;

public class AiUsageRecordAppServiceTests : BidCompareApplicationTestBase<BidCompareApplicationTestModule>
{
    [Fact]
    public async Task Create_And_List_Usage_Records()
    {
        var appService = GetRequiredService<IAiUsageRecordAppService>();

        await appService.CreateAsync(new CreateAiUsageRecordDto
        {
            Business = "bid-compare",
            UsedConfig = "Qwen3.6-A3B",
            UsedModel = "Qwen3.6-35B-A3B-FP8",
            InputTokens = 100,
            OutputTokens = 50,
            TotalTokens = 150,
            FinishReason = "stop",
            Attempts = 1,
            LatencySeconds = 0.5,
            CircuitBreakerState = "closed",
            Success = true
        });
        await appService.CreateAsync(new CreateAiUsageRecordDto
        {
            Business = "standard-qa",
            UsedConfig = "Qwen3.6-A3B",
            UsedModel = "Qwen3.6-35B-A3B-FP8",
            Success = false,
            ErrorType = "PROVIDER_UNAVAILABLE",
            ErrorMessage = "all down"
        });

        var list = await appService.GetListAsync(new GetAiUsageRecordsInput
        {
            MaxResultCount = 10
        });
        Assert.Equal(2, list.TotalCount);

        var filtered = await appService.GetListAsync(new GetAiUsageRecordsInput
        {
            Business = "bid-compare",
            MaxResultCount = 10
        });
        Assert.Equal(1, filtered.TotalCount);

        var stats = await appService.GetStatsAsync();
        Assert.Equal(2, stats.TotalCalls);
        Assert.Equal(150, stats.TotalTokens);

        var series = await appService.GetTimeSeriesAsync("7d");
        Assert.Equal(7, series.Categories.Count);
        Assert.Contains(series.ByModel, x => x.Name == "Qwen3.6-35B-A3B-FP8");
    }
}
