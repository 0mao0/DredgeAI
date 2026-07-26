import type MockAdapter from 'axios-mock-adapter'
import { mockApplications } from '@shared/mock/data/applications'

interface CollectionCategory {
  key: string
  name: string
  description: string
  published: boolean
  subAppId?: string
}

/** axios-mock-adapter 的 config.data 为 JSON 字符串，按需解析 */
function parseBody(data: unknown): Record<string, unknown> {
  if (typeof data === 'string') {
    try { return JSON.parse(data) as Record<string, unknown> } catch { return {} }
  }
  return (data as Record<string, unknown>) ?? {}
}

// 情报采集模块可发布的采集分类配置（发布后生成对应子应用）
const collectionCategories: Record<string, CollectionCategory[]> = {
  8: [
    { key: 'dredge', name: '疏浚情报', description: '聚焦疏浚行业的科技与工程情报', published: true, subAppId: '8-1' },
    { key: 'tech', name: '科技情报', description: '通用科技前沿情报，支持订阅推送', published: true, subAppId: '8-2' },
    { key: 'policy', name: '政策情报', description: '行业政策与标准动态追踪', published: false },
  ],
}

export function registerApplicationMock(mock: MockAdapter, wrap: (handler: () => unknown) => () => Promise<[number, unknown]>): void {
  mock.onGet('/api/admin/applications').reply(wrap(() => mockApplications))

  mock.onGet('/api/admin/applications/sub').reply((config) => {
    const appId = config.params?.appId as string | undefined
    const app = mockApplications.find((a) => a.id === appId)
    return [200, app?.subApps ?? []]
  })

  mock.onPost('/api/admin/applications/sub/status').reply((config) => {
    const body = parseBody(config.data) as { subId: string, status: '已发布' | '已下架' }
    for (const app of mockApplications) {
      const sub = app.subApps?.find((s) => s.id === body.subId)
      if (sub) sub.status = body.status
    }
    return [200, null]
  })

  mock.onPost('/api/admin/applications/status').reply((config) => {
    const body = parseBody(config.data) as { appId: string, status: '运营中' | '已下架' }
    const app = mockApplications.find((a) => a.id === body.appId)
    if (app) app.status = body.status
    return [200, null]
  })

  mock.onPost('/api/admin/applications/icon').reply((config) => {
    const body = parseBody(config.data) as { appId: string, icon: string }
    const app = mockApplications.find((a) => a.id === body.appId)
    if (app) app.icon = body.icon
    return [200, null]
  })

  mock.onPost('/api/admin/applications/sub/icon').reply((config) => {
    const body = parseBody(config.data) as { subId: string, icon: string }
    for (const app of mockApplications) {
      const sub = app.subApps?.find((s) => s.id === body.subId)
      if (sub) sub.icon = body.icon
    }
    return [200, null]
  })

  mock.onPost('/api/admin/applications/scope').reply((config) => {
    const body = parseBody(config.data) as { appId: string, scope: '所有' | '部分' }
    const app = mockApplications.find((a) => a.id === body.appId)
    if (app) app.scope = body.scope
    return [200, null]
  })

  mock.onPost('/api/admin/applications/sub/scope').reply((config) => {
    const body = parseBody(config.data) as { subId: string, scope: '所有' | '部分' }
    for (const app of mockApplications) {
      const sub = app.subApps?.find((s) => s.id === body.subId)
      if (sub) sub.scope = body.scope
    }
    return [200, null]
  })

  mock.onGet('/api/admin/applications/collection-categories').reply((config) => {
    const appId = config.params?.appId as string | undefined
    return [200, collectionCategories[appId ?? ''] ?? []]
  })

  mock.onPost('/api/admin/applications/collection-categories/publish').reply((config) => {
    const { appId, categoryKey } = parseBody(config.data) as { appId: string, categoryKey: string }
    const app = mockApplications.find((a) => a.id === appId)
    const cat = collectionCategories[appId]?.find((c) => c.key === categoryKey)
    if (!app || !cat) return [404, { message: '未找到采集分类' }]
    const subId = `${appId}-${categoryKey}`
    const subApp = {
      id: subId,
      name: cat.name,
      category: app.category,
      parentAppId: app.id,
      parentAppName: app.name,
      route: `/intelligence/${categoryKey}`,
      icon: 'ExperimentOutlined',
      version: 'v1.0.0',
      status: '已发布' as const,
      description: cat.description,
    }
    app.subApps = app.subApps ?? []
    const existing = app.subApps.find((s) => s.id === subId)
    if (existing) existing.status = '已发布'
    else app.subApps.push(subApp)
    cat.published = true
    cat.subAppId = subId
    return [200, subApp]
  })
}
