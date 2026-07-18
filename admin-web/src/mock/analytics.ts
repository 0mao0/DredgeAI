import type { LineChartData, PieChartData } from '@/types'

export const mockDailyApiCalls: LineChartData = {
  categories: ['06:00', '08:00', '10:00', '12:00', '14:00', '16:00', '18:00', '20:00', '22:00'],
  series: [
    { name: '今日', data: [120, 450, 890, 560, 920, 1100, 780, 430, 180] },
    { name: '昨日', data: [100, 420, 850, 520, 890, 1050, 720, 400, 160] },
  ],
}

export const mockModelUsage: PieChartData = {
  name: '模型调用分布',
  data: [
    { name: 'GPT-4', value: 45 },
    { name: 'GPT-3.5', value: 30 },
    { name: 'Claude-3', value: 15 },
    { name: '通义千问', value: 10 },
  ],
}

export const mockUserGrowth: LineChartData = {
  categories: ['1月', '2月', '3月', '4月', '5月', '6月', '7月'],
  series: [
    { name: '新增用户', data: [120, 180, 240, 200, 310, 380, 420] },
  ],
}

export const mockErrorRate: LineChartData = {
  categories: ['06:00', '08:00', '10:00', '12:00', '14:00', '16:00', '18:00', '20:00', '22:00'],
  series: [
    { name: '错误率', data: [0.5, 1.2, 0.8, 0.3, 1.5, 2.1, 1.8, 0.9, 0.4] },
  ],
}
