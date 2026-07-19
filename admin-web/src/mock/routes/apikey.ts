import type MockAdapter from 'axios-mock-adapter'
import { apiKeys, modelTypes, usageByModel, usageByKey } from '@shared/mock/data/apikey'

function generateTimeSeries(days: number) {
  const now = new Date()
  const categories: string[] = []
  for (let i = days - 1; i >= 0; i--) {
    const d = new Date(now)
    d.setDate(d.getDate() - i)
    categories.push(`${d.getMonth() + 1}/${d.getDate()}`)
  }
  function randomData(base: number, amp: number) {
    return categories.map(() => Math.floor(base + Math.random() * amp))
  }
  return {
    categories,
    byModel: [
      { modelName: 'GPT-4o', data: randomData(1200, 3500) },
      { modelName: 'Claude 3.5 Sonnet', data: randomData(600, 2000) },
      { modelName: 'Claude 3 Haiku', data: randomData(800, 2500) },
      { modelName: '本地模型', data: randomData(200, 800) },
      { modelName: '通义千问-Max', data: randomData(50, 400) },
      { modelName: 'DeepSeek-V3', data: randomData(50, 300) },
    ],
    byKey: [
      { keyName: '生产环境-主入口', data: randomData(800, 2200) },
      { keyName: '用量统计采集器', data: randomData(600, 2500) },
      { keyName: 'BIM 分析专用', data: randomData(500, 1800) },
      { keyName: '文档分析-合同审查', data: randomData(400, 1500) },
      { keyName: 'AI 审标专用', data: randomData(300, 1200) },
      { keyName: '测试环境-内部', data: randomData(200, 1000) },
      { keyName: '海外节点-新加坡', data: randomData(100, 600) },
    ],
    byName: [
      { name: '生产环境-主入口', data: randomData(800, 2200) },
      { name: '用量统计采集器', data: randomData(600, 2500) },
      { name: 'BIM 分析专用', data: randomData(500, 1800) },
      { name: '文档分析-合同审查', data: randomData(400, 1500) },
      { name: 'AI 审标专用', data: randomData(300, 1200) },
    ],
  }
}

/**
 * 注册 API Key 相关�?mock 路由
 */
export function registerApiKeyMock(
  mock: MockAdapter,
  wrap: (h: () => unknown) => () => Promise<[number, unknown]>,
): void {
  mock.onGet('/api/admin/apikey/list').reply(wrap(() => apiKeys))
  mock.onGet('/api/admin/apikey/models').reply(wrap(() => modelTypes))
  mock.onGet('/api/admin/apikey/usage-by-model').reply(wrap(() => usageByModel))
  mock.onGet('/api/admin/apikey/usage-by-key').reply(wrap(() => usageByKey))
  mock.onGet('/api/admin/apikey/usage-stats').reply(wrap(() => ({
    totalTokens: 28640000,
    totalCalls: 72500,
  })))
  mock.onGet('/api/admin/apikey/usage-timeseries').reply((config) => {
    const now = new Date()
    const range = config.params?.range || '7d'
    if (range === 'custom') {
      const start = config.params?.startDate ? new Date(config.params.startDate) : null
      const end = config.params?.endDate ? new Date(config.params.endDate) : null
      if (start && end) {
        const days = Math.ceil((end.getTime() - start.getTime()) / (1000 * 60 * 60 * 24)) + 1
        return [200, generateTimeSeries(Math.max(days, 1))]
      }
    }
    const daysMap: Record<string, number> = { '7d': 7, '30d': 30, 'this-month': now.getDate(), 'last-month': 30 }
    const days = daysMap[range] || 7
    return [200, generateTimeSeries(days)]
  })
}
