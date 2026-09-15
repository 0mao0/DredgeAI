namespace DredgeAI.BidCompare.Applications;

/// <summary>应用分类配置（名称为枚举 wire 值 + 标签色；中文展示走前端 label map）。</summary>
public class CategoryConfigDto
{
    public string Name { get; set; } = default!;

    public string Color { get; set; } = default!;
}
