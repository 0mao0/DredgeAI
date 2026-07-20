import type MockAdapter from 'axios-mock-adapter'
import { dubbingTasks, dubbingUsageSummary, dubbingUsageTimeSeries } from '@shared/mock/data/dubbing'

export function registerDubbingMock(mock: MockAdapter, wrap: (handler: () => unknown) => () => Promise<[number, unknown]>): void {
  mock.onGet('/api/admin/dubbing/admin/tasks').reply(wrap(() => ({
    items: dubbingTasks,
    totalCount: dubbingTasks.length,
  })))

  mock.onDelete(new RegExp('/api/admin/dubbing/admin/tasks/(.+)$')).reply((config) => {
    const match = config.url?.match(/\/api\/admin\/dubbing\/admin\/tasks\/(.+)$/)
    if (!match) return [404, {}]
    const id = match[1]
    const idx = dubbingTasks.findIndex(t => t.id === id)
    if (idx === -1) return [404, {}]
    if (!dubbingTasks[idx].deletedByUser) {
      return [403, {
        error: {
          code: null,
          message: '用户未删除，受隐私限制不可彻底删除',
          details: null,
          data: null,
          validationErrors: null,
        },
      }]
    }
    dubbingTasks.splice(idx, 1)
    return [204]
  })

  mock.onGet('/api/admin/dubbing/admin/usage/summary').reply(wrap(() => dubbingUsageSummary))

  mock.onGet('/api/admin/dubbing/admin/usage/timeseries').reply(wrap(() => dubbingUsageTimeSeries))
}
