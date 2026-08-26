using DredgeAI.BidCompare.TenderReadings;
using DredgeAI.BidCompare.TenderReadings.Extractors;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Application;
using Volo.Abp.AutoMapper;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Modularity;

namespace DredgeAI.BidCompare;

[DependsOn(
    typeof(BidCompareDomainModule),
    typeof(BidCompareApplicationContractsModule),
    typeof(AbpDddApplicationModule),
    typeof(AbpAutoMapperModule),
    typeof(AbpBackgroundWorkersModule)
    )]
public class BidCompareApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpAutoMapperOptions>(options =>
        {
            options.AddMaps<BidCompareApplicationModule>();
        });

        // 读标抽取器显式注册，确保后台任务中 IEnumerable<IBaselineFieldExtractor> 能拿到全部实现
        context.Services.AddTransient<IBaselineFieldExtractor, ProjectInfoExtractor>();
        context.Services.AddTransient<IBaselineFieldExtractor, CommercialDataExtractor>();
        context.Services.AddTransient<IBaselineFieldExtractor, OutlineExtractor>();
        context.Services.AddTransient<IBaselineFieldExtractor, RejectionClausesExtractor>();
        context.Services.AddTransient<IBaselineFieldExtractor, EvaluationCriteriaExtractor>();
        context.Services.AddTransient<IBaselineFieldExtractor, TechnicalParametersExtractor>();
        context.Services.AddTransient<IBaselineFieldExtractor, SealRulesExtractor>();
        context.Services.AddTransient<IBaselineFieldExtractor, DarkBidFormatRulesExtractor>();
    }
}
