import type MockAdapter from 'axios-mock-adapter'
import { mockOrgUnits } from '@shared/mock/data/org-units'
import type { OrgUnit } from '@shared/types'

function parseBody(data: unknown): Record<string, unknown> {
  if (typeof data === 'string') {
    try { return JSON.parse(data) as Record<string, unknown> } catch { return {} }
  }
  return (data as Record<string, unknown>) ?? {}
}

function extractId(url: string | undefined, pattern: RegExp, group = 1): string | undefined {
  if (!url) return undefined
  return pattern.exec(url)?.[group]
}

const tree = structuredClone(mockOrgUnits)

function findNode(nodes: OrgUnit[], id: string): OrgUnit | undefined {
  for (const node of nodes) {
    if (node.id === id) return node
    const hit = findNode(node.children, id)
    if (hit) return hit
  }
  return undefined
}

/** 从树中摘除指定节点（返回被摘节点；未找到返回 undefined） */
function detachNode(nodes: OrgUnit[], id: string): OrgUnit | undefined {
  for (let i = 0; i < nodes.length; i++) {
    if (nodes[i].id === id) return nodes.splice(i, 1)[0]
    const hit = detachNode(nodes[i].children, id)
    if (hit) return hit
  }
  return undefined
}

function abpError(code: string, msg: string): [number, unknown] {
  return [
    400,
    { error: { code, message: msg, details: null, data: null, validationErrors: null } },
  ]
}

export function registerOrgUnitsMock(mock: MockAdapter): void {
  mock.onGet('/api/base/organization-units/tree').reply(() => [200, structuredClone(tree)])

  mock.onPost('/api/base/organization-units').reply((config) => {
    const body = parseBody(config.data)
    const parentId = (body.parentId as string | null) ?? null
    const siblings = parentId ? findNode(tree, parentId)?.children : tree
    if (!siblings) return [404, null]
    const node: OrgUnit = {
      id: String(Date.now()),
      code: parentId
        ? `${findNode(tree, parentId)!.code}.${String(siblings.length + 1).padStart(4, '0')}`
        : String(siblings.length + 1).padStart(4, '0'),
      displayName: body.displayName as string,
      parentId,
      creationTime: new Date().toISOString(),
      children: [],
    }
    siblings.push(node)
    return [200, node]
  })

  mock.onPut(/\/api\/base\/organization-units\/[^/]+$/).reply((config) => {
    const id = extractId(config.url, /\/organization-units\/([^/]+)$/)
    if (!id) return [404, null]
    const node = findNode(tree, id)
    if (!node) return [404, null]
    const body = parseBody(config.data)
    if (body.displayName !== undefined) node.displayName = body.displayName as string
    // parentId 仅在显式传入且变化时移动子树（对齐后端 null=保持不变 语义）
    if (body.parentId !== undefined && body.parentId !== node.parentId) {
      const newParentId = body.parentId as string
      const newSiblings = findNode(tree, newParentId)?.children
      if (!newSiblings) return [404, null]
      const moved = detachNode(tree, id)!
      moved.parentId = newParentId
      newSiblings.push(moved)
    }
    return [200, null]
  })

  mock.onDelete(/\/api\/base\/organization-units\/[^/]+$/).reply((config) => {
    const id = extractId(config.url, /\/organization-units\/([^/]+)$/)
    if (!id) return [404, null]
    const node = findNode(tree, id)
    if (!node) return [404, null]
    if (node.children.length > 0) {
      return abpError('OrganizationUnit:CannotDeleteWithChildren', '存在子级组织，无法删除')
    }
    detachNode(tree, id)
    return [200, null]
  })
}
