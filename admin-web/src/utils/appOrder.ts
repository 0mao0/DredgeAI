import type { ApplicationItem } from '@/types'

/** 按 admin 全局默认顺序（应用 id 列表）排序应用；不在顺序中的应用排末尾。 */
export function sortAppsByOrder(apps: ApplicationItem[], orderIds: string[]): ApplicationItem[] {
  const position = new Map(orderIds.map((id, index) => [id, index]))
  return [...apps].sort((a, b) => {
    const ai = position.get(a.id) ?? Number.MAX_SAFE_INTEGER
    const bi = position.get(b.id) ?? Number.MAX_SAFE_INTEGER
    return ai - bi
  })
}
