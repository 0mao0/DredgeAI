import type MockAdapter from 'axios-mock-adapter'
import { mockApplications } from '@shared/mock/data/applications'
import type { AppCategory, AppMainStatus, SubAppStatus } from '@shared/types'

/** axios-mock-adapter 的 config.data 为 JSON 字符串，按需解析 */
function parseBody(data: unknown): Record<string, unknown> {
  if (typeof data === 'string') {
    try { return JSON.parse(data) as Record<string, unknown> } catch { return {} }
  }
  return (data as Record<string, unknown>) ?? {}
}

const defaultCategories = [
  { name: 'general', color: 'blue' },
  { name: 'operation', color: 'green' },
  { name: 'design', color: 'purple' },
  { name: 'construction', color: 'gold' },
]

export function registerApplicationMock(mock: MockAdapter, wrap: (handler: () => unknown) => () => Promise<[number, unknown]>): void {
  mock.onGet('/api/bidcompare/app-catalog').reply(wrap(() => mockApplications))

  mock.onGet('/api/bidcompare/app-catalog/categories').reply(wrap(() => defaultCategories))

  mock.onGet('/api/bidcompare/app-catalog/sub').reply((config) => {
    const appId = config.params?.appId as string | undefined
    const app = mockApplications.find((a) => a.id === appId)
    return [200, app?.subApps ?? []]
  })

  mock.onPost('/api/bidcompare/app-catalog/sub/status').reply((config) => {
    const body = parseBody(config.data) as { subId: string, status: SubAppStatus }
    for (const app of mockApplications) {
      const sub = app.subApps?.find((s) => s.id === body.subId)
      if (sub) sub.status = body.status
    }
    return [200, null]
  })

  mock.onPost('/api/bidcompare/app-catalog/status').reply((config) => {
    const body = parseBody(config.data) as { appId: string, status: AppMainStatus }
    const app = mockApplications.find((a) => a.id === body.appId)
    if (app) app.status = body.status
    return [200, null]
  })

  mock.onPost('/api/bidcompare/app-catalog/category').reply((config) => {
    const body = parseBody(config.data) as { appId: string, category: string }
    const app = mockApplications.find((a) => a.id === body.appId)
    if (app) app.category = body.category as AppCategory
    return [200, null]
  })

  mock.onPost('/api/bidcompare/app-catalog/sub/category').reply((config) => {
    const body = parseBody(config.data) as { subId: string, category: string }
    for (const app of mockApplications) {
      const sub = app.subApps?.find((s) => s.id === body.subId)
      if (sub) sub.category = body.category as AppCategory
    }
    return [200, null]
  })

  mock.onPost('/api/bidcompare/app-catalog/icon').reply((config) => {
    const body = parseBody(config.data) as { appId: string, icon: string }
    const app = mockApplications.find((a) => a.id === body.appId)
    if (app) app.icon = body.icon
    return [200, null]
  })

  mock.onPost('/api/bidcompare/app-catalog/sub/icon').reply((config) => {
    const body = parseBody(config.data) as { subId: string, icon: string }
    for (const app of mockApplications) {
      const sub = app.subApps?.find((s) => s.id === body.subId)
      if (sub) sub.icon = body.icon
    }
    return [200, null]
  })

  mock.onPost('/api/bidcompare/app-catalog/move').reply((config) => {
    const body = parseBody(config.data) as { appId: string, direction: 'up' | 'down' }
    const index = mockApplications.findIndex((a) => a.id === body.appId)
    const target = body.direction === 'up' ? index - 1 : index + 1
    if (index >= 0 && target >= 0 && target < mockApplications.length) {
      ;[mockApplications[index], mockApplications[target]] = [mockApplications[target], mockApplications[index]]
    }
    return [200, mockApplications]
  })

  mock.onPost('/api/bidcompare/app-catalog/sub/move').reply((config) => {
    const body = parseBody(config.data) as { subId: string, direction: 'up' | 'down' }
    for (const app of mockApplications) {
      const subs = app.subApps
      if (!subs) continue
      const index = subs.findIndex((s) => s.id === body.subId)
      const target = body.direction === 'up' ? index - 1 : index + 1
      if (index >= 0 && target >= 0 && target < subs.length) {
        ;[subs[index], subs[target]] = [subs[target], subs[index]]
      }
    }
    return [200, mockApplications]
  })
}
