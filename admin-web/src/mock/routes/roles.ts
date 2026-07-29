import type MockAdapter from 'axios-mock-adapter'
import { mockRoles } from '@shared/mock/data/roles'
import { mockOrgUsers } from '@shared/mock/data/org-users'
import type { Role } from '@shared/types'

function parseBody(data: unknown): Record<string, unknown> {
  if (typeof data === 'string') {
    try { return JSON.parse(data) as Record<string, unknown> } catch { return {} }
  }
  return (data as Record<string, unknown>) ?? {}
}

let roles = [...mockRoles]
let userRolesMap = new Map<string, string[]>(
  mockOrgUsers.map((u) => [u.id, [...u.roleIds]]),
)

function updateUserCounts(): void {
  roles.forEach((r) => {
    let count = 0
    userRolesMap.forEach((roleIds) => {
      if (roleIds.includes(r.id)) count++
    })
    r.userCount = count
  })
}

function extractId(url: string | undefined, pattern: RegExp, group = 1): string | undefined {
  if (!url) return undefined
  return pattern.exec(url)?.[group]
}

export function registerRolesMock(mock: MockAdapter, wrap: (handler: () => unknown) => () => Promise<[number, unknown]>): void {
  mock.onGet('/api/admin/roles').reply(wrap(() => [...roles]))

  mock.onPost('/api/admin/roles').reply((config) => {
    const body = parseBody(config.data)
    const r: Role = {
      id: String(Date.now()),
      name: body.name as string,
      description: (body.description as string) || '',
      menuKeys: [],
      appIds: [],
      userCount: 0,
      createdAt: new Date().toISOString().slice(0, 10),
    }
    roles.push(r)
    return [200, r]
  })

  mock.onPut(/\/api\/admin\/roles\/[^/]+$/).reply((config) => {
    const id = extractId(config.url, /\/roles\/([^/]+)$/)
    if (!id) return [404, null]
    const body = parseBody(config.data)
    const r = roles.find((r) => r.id === id)
    if (r) {
      if (body.name !== undefined) r.name = body.name as string
      if (body.description !== undefined) r.description = body.description as string
    }
    return [200, null]
  })

  mock.onDelete(/\/api\/admin\/roles\/[^/]+$/).reply((config) => {
    const id = extractId(config.url, /\/roles\/([^/]+)$/)
    if (!id) return [404, null]
    roles = roles.filter((r) => r.id !== id)
    userRolesMap.forEach((roleIds, uid) => {
      userRolesMap.set(uid, roleIds.filter((rid) => rid !== id))
    })
    return [200, null]
  })

  mock.onGet(/\/api\/admin\/roles\/[^/]+\/users$/).reply((config) => {
    const roleId = extractId(config.url, /\/roles\/([^/]+)\/users/)
    if (!roleId) return [200, []]
    return [200, mockOrgUsers.filter((u) => {
      const rids = userRolesMap.get(u.id) || []
      return rids.includes(roleId)
    })]
  })

  mock.onPost(/\/api\/admin\/roles\/[^/]+\/users$/).reply((config) => {
    const roleId = extractId(config.url, /\/roles\/([^/]+)\/users/)
    if (!roleId) return [404, null]
    const body = parseBody(config.data)
    const { userIds } = body as { userIds: string[] }
    for (const uid of (userIds || [])) {
      const current = userRolesMap.get(uid) || []
      if (!current.includes(roleId)) {
        userRolesMap.set(uid, [...current, roleId])
      }
    }
    updateUserCounts()
    return [200, null]
  })

  mock.onDelete(/\/api\/admin\/roles\/[^/]+\/users\/[^/]+$/).reply((config) => {
    const m = /\/roles\/([^/]+)\/users\/([^/]+)/.exec(config.url || '')
    if (!m) return [404, null]
    const roleId = m[1]
    const userId = m[2]
    const current = userRolesMap.get(userId) || []
    userRolesMap.set(userId, current.filter((rid) => rid !== roleId))
    updateUserCounts()
    return [200, null]
  })

  mock.onPut(/\/api\/admin\/roles\/[^/]+\/permissions$/).reply((config) => {
    const roleId = extractId(config.url, /\/roles\/([^/]+)\/permissions/)
    if (!roleId) return [404, null]
    const body = parseBody(config.data) as { menuKeys: string[]; appIds: string[] }
    const r = roles.find((r) => r.id === roleId)
    if (r) {
      r.menuKeys = body.menuKeys || []
      r.appIds = body.appIds || []
    }
    return [200, null]
  })
}
