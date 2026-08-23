import type MockAdapter from 'axios-mock-adapter'
import { standardsResult, standardsSearchHistory, standardCategories, recommendedQuestions, standardList, standardProperties, standardDocuments, standardAIAnalyses, standardRecords, standardFilesByRecord } from '@shared/mock/data/standard'

function normNature(n?: string | null): string {
  if (n === '强制性标准' || n === '强制') return '强制'
  if (n === '推荐性标准' || n === '推荐') return '推荐'
  return n || '指导'
}

export function registerStandardMock(
  mock: MockAdapter,
  wrap: (h: () => unknown) => () => Promise<[number, unknown]>,
): void {
  mock.onGet('/api/standard/result').reply(wrap(() => standardsResult))
  mock.onGet('/api/standard/history').reply(wrap(() => standardsSearchHistory))
  mock.onGet('/api/standard/categories').reply(wrap(() => standardCategories))
  mock.onGet('/api/standard/recommended').reply(wrap(() => recommendedQuestions))
  mock.onGet('/api/standard/list').reply(wrap(() => standardList))
  mock.onGet('/api/standard/property').reply((config) => {
    const id = (config.params as Record<string, string>)?.id
    return [200, standardProperties.find((p) => p.id === id) || null]
  })
  mock.onGet('/api/standard/property/list').reply(wrap(() => standardProperties))
  mock.onGet('/api/standard/document').reply((config) => {
    const id = (config.params as Record<string, string>)?.id
    return [200, standardDocuments.find((d) => d.id === id) || null]
  })
  mock.onGet('/api/standard/ai-analysis').reply((config) => {
    const id = (config.params as Record<string, string>)?.id
    return [200, standardAIAnalyses.find((a) => a.id === id) || null]
  })
  mock.onPut('/api/standard/property').reply(wrap(() => [200, null]))

  // —— 标准记录（新规范模型）——
  mock.onGet('/api/standard/records/tree').reply(wrap(() => standardCategories))
  mock.onGet('/api/standard/records').reply((config) => {
    const params = (config.params ?? {}) as Record<string, unknown>
    const keyword = String(params.keyword ?? '').trim().toLowerCase()
    const level = String(params.level ?? '').trim()
    const industry = String(params.industry ?? '').trim()
    const nature = String(params.nature ?? '').trim()
    const status = String(params.status ?? '').trim()
    const year = Number(params.year ?? 0)
    const filtered = standardRecords.filter((r) => {
      if (keyword) {
        const hay = [r.name, r.code, r.level, r.industry, r.department, r.nature, r.status, String(r.year ?? '')].join(' ').toLowerCase()
        if (!hay.includes(keyword)) return false
      }
      if (level && r.level !== level) return false
      if (industry && r.industry !== industry) return false
      if (nature && normNature(r.nature) !== nature) return false
      if (status && r.status !== status) return false
      if (year && r.year !== year) return false
      return true
    })
    const skip = Number(params.skipCount ?? 0)
    const max = Number(params.maxResultCount ?? 15)
    return [200, { items: filtered.slice(skip, skip + max), totalCount: filtered.length }]
  })
  mock.onGet(/\/api\/standard\/records\/[^/]+\/files$/).reply((config) => {
    const id = config.url?.split('/').slice(-2)[0]
    return [200, standardFilesByRecord[id ?? ''] ?? []]
  })
  mock.onGet(/\/api\/standard\/records\/[^/]+$/).reply((config) => {
    const id = config.url?.split('/').pop()
    return [200, standardRecords.find((r) => r.id === id) || null]
  })
  mock.onPost('/api/standard/qa/ask').reply(wrap(() => ({
    answer: '根据《中华人民共和国河道管理条例》第二十条，有堤防的河道，其管理范围为两岸堤防之间的水域、沙洲、滩地（包括可耕地）、行洪区，两岸堤防及护堤地。',
    citations: [
      { standardId: 'std-1', name: '中华人民共和国河道管理条例', code: '国务院令第698号', snippet: '第二十条 有堤防的河道，其管理范围为两岸堤防之间的水域、沙洲、滩地（包括可耕地）、行洪区，两岸堤防及护堤地。', page: 3 },
    ],
  })))
}
