import type { LineChartData, PieChartData } from '@/types'

export const efficiencyTrend: LineChartData = {
  categories: ['7/11', '7/12', '7/13', '7/14', '7/15', '7/16', '7/17'],
  series: [
    { name: '任务数', data: [3, 5, 2, 4, 6, 4, 7] },
    { name: '完成数', data: [2, 4, 2, 3, 5, 4, 5] },
  ],
}

export const apiKeyUsagePie: PieChartData = {
  name: 'API 用量分布',
  data: [
    { name: 'GPT-4o', value: 18100 },
    { name: 'Claude 3.5 Sonnet', value: 8300 },
    { name: '本地模型', value: 4200 },
    { name: '通义千问-Max', value: 1600 },
    { name: 'DeepSeek-V3', value: 800 },
  ],
}

export const apiKeyUsageBar = {
  categories: ['生产环境', '测试环境', 'AI 审标专用'],
  series: [{ name: '调用次数', data: [12500, 8300, 5600] }],
}
