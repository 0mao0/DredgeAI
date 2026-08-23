import type MockAdapter from 'axios-mock-adapter'
import { standardAIAnalyses, standardDocuments, standardProperties } from '@shared/mock/data/standard'
import type {
  StandardAIAnalysis,
  StandardHighlight,
  StandardParseBatchResult,
  StandardProperty,
  StandardPropertyInput,
} from '@/types'

function normalizeNature(nature?: string): string {
  if (nature === '强制性标准') return '强制'
  if (nature === '推荐性标准') return '推荐'
  return nature || '推荐'
}

// 管理端标准规范列表：复用共享标准数据并统一性质文案
const adminStandards: StandardProperty[] = standardProperties.map((p) => ({
  ...p,
  nature: normalizeNature(p.nature),
  source: p.level === '企业标准' ? 'manual' : 'remote',
  syncedAt: p.level === '企业标准' ? null : '2026-08-20T03:00:00Z',
  isEnabled: p.status !== '作废',
}))

const PAGE_W = 595
const PAGE_H = 842

// 演示数据：已解析的标准附带原文 bbox 高亮（坐标与 gen-standard-sample-pdf.mjs 的排版一致）
const demoHighlights: Record<string, StandardHighlight[]> = {
  'std-1': [
    { id: 'std-1-title', itemId: 'std-1', page: 1, left: 60 / PAGE_W, top: 72 / PAGE_H, width: 216 / PAGE_W, height: 18 / PAGE_H },
    { id: 'std-1-a1', itemId: 'std-1', page: 2, left: 60 / PAGE_W, top: 120 / PAGE_H, width: 480 / PAGE_W, height: 12 / PAGE_H },
    { id: 'std-1-a1-2', itemId: 'std-1', page: 2, left: 60 / PAGE_W, top: 142 / PAGE_H, width: 132 / PAGE_W, height: 12 / PAGE_H },
    { id: 'std-1-a2', itemId: 'std-1', page: 2, left: 60 / PAGE_W, top: 164 / PAGE_H, width: 480 / PAGE_W, height: 12 / PAGE_H },
    { id: 'std-1-a2-2', itemId: 'std-1', page: 2, left: 60 / PAGE_W, top: 186 / PAGE_H, width: 72 / PAGE_W, height: 12 / PAGE_H },
    { id: 'std-1-a3', itemId: 'std-1', page: 2, left: 60 / PAGE_W, top: 208 / PAGE_H, width: 480 / PAGE_W, height: 12 / PAGE_H },
    { id: 'std-1-a3-2', itemId: 'std-1', page: 2, left: 60 / PAGE_W, top: 230 / PAGE_H, width: 396 / PAGE_W, height: 12 / PAGE_H },
  ],
  'std-2': [
    { id: 'std-2-title', itemId: 'std-2', page: 1, left: 60 / PAGE_W, top: 72 / PAGE_H, width: 198 / PAGE_W, height: 18 / PAGE_H },
    { id: 'std-2-a1', itemId: 'std-2', page: 2, left: 60 / PAGE_W, top: 120 / PAGE_H, width: 480 / PAGE_W, height: 12 / PAGE_H },
    { id: 'std-2-a1-2', itemId: 'std-2', page: 2, left: 60 / PAGE_W, top: 142 / PAGE_H, width: 120 / PAGE_W, height: 12 / PAGE_H },
    { id: 'std-2-a2', itemId: 'std-2', page: 2, left: 60 / PAGE_W, top: 164 / PAGE_H, width: 408 / PAGE_W, height: 12 / PAGE_H },
  ],
}

function attachParsedInfo(item: StandardProperty): StandardProperty {
  const highlights = demoHighlights[item.id]
  return highlights ? { ...item, parsed: true, highlights } : item
}

function matchPattern(url: string | undefined, prefix: string): string | null {
  if (!url) return null
  const escaped = prefix.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
  const match = url.match(new RegExp(`^${escaped}/(.+)$`))
  return match ? match[1] : null
}

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms))
}

function hashString(input: string): number {
  let hash = 0
  for (let i = 0; i < input.length; i += 1) {
    hash = (hash * 31 + input.charCodeAt(i)) >>> 0
  }
  return hash
}

function formDataFile(data: unknown): File | null {
  if (data instanceof FormData) {
    const file = data.get('file')
    return file instanceof File ? file : null
  }
  return null
}

function formDataMetadata(data: unknown): StandardPropertyInput | null {
  if (data instanceof FormData) {
    const raw = data.get('metadata')
    if (typeof raw === 'string') {
      try {
        return JSON.parse(raw) as StandardPropertyInput
      } catch {
        return null
      }
    }
  }
  return null
}

function parseJsonBody(data: unknown): Record<string, unknown> {
  if (typeof data === 'string') {
    try {
      return JSON.parse(data) as Record<string, unknown>
    } catch {
      return {}
    }
  }
  return (data && typeof data === 'object' ? data : {}) as Record<string, unknown>
}

function buildAnalysis(standard: StandardProperty): StandardAIAnalysis {
  const existing = standardAIAnalyses.find((a) => a.id === standard.id)
  if (existing) return existing
  return {
    id: standard.id,
    summary: `已解析《${standard.name}》（${standard.code}）：${standard.level}，${standard.nature}性标准，当前状态为「${standard.status}」。`,
    keyPoints: [
      `发布部门：${standard.issuer}，发布年份：${standard.publishYear}。`,
      `所属行业：${standard.industry}，适用于${standard.industry}相关工程与管理场景。`,
      '建议结合项目实际需求对照原文条款执行，并关注后续修订版本。',
    ],
    relatedStandards: [],
    riskWarnings: standard.status === '作废'
      ? ['该标准已作废，使用前请确认现行有效版本。']
      : standard.status === '即将实施'
        ? ['该标准尚未正式实施，请留意正式实施日期及过渡安排。']
        : [],
  }
}

export function registerStandardsMock(
  mock: MockAdapter,
): void {
  mock.onGet('/api/admin/standard/document').reply((config) => {
    const id = (config.params as Record<string, string>)?.id
    return [200, standardDocuments.find((d) => d.id === id) || null]
  })

  mock.onGet('/api/admin/standards').reply((config) => {
    const params = (config.params || {}) as Record<string, unknown>
    let items = [...adminStandards]
    const keyword = String(params.keyword || '').trim().toLowerCase()
    if (keyword) {
      items = items.filter((s) => s.name.toLowerCase().includes(keyword) || s.code.toLowerCase().includes(keyword))
    }
    if (params.industry) items = items.filter((s) => s.industry === params.industry)
    if (params.nature) items = items.filter((s) => s.nature === params.nature)
    if (params.level) items = items.filter((s) => s.level === params.level)
    if (params.status) items = items.filter((s) => s.status === params.status)
    if (params.publishYear) items = items.filter((s) => s.publishYear === Number(params.publishYear))
    const skipCount = Number(params.skipCount || 0)
    const maxResultCount = Number(params.maxResultCount || 15)
    return [200, {
      items: items.slice(skipCount, skipCount + maxResultCount).map(attachParsedInfo),
      totalCount: items.length,
    }]
  })

  mock.onPost('/api/admin/standards/preview').reply(async (config) => {
    const file = formDataFile(config.data)
    if (!file) return [400, {}]
    const baseName = file.name.replace(/\.pdf$/i, '').trim() || '未命名标准'
    const hash = hashString(file.name)
    const industries = ['水利', '建筑', '交通', '环保', '能源', '综合']
    const natures = ['强制', '推荐', '指导']
    const levels = ['国家标准', '行业标准', '地方标准', '团体标准', '企业标准', '国际标准', '法律法规']
    const issuers = ['国务院', '水利部', '住房和城乡建设部', '交通运输部', '生态环境部', '全国人大常委会']
    const statuses = ['现行', '即将实施', '作废']
    const industry = industries[hash % industries.length]
    const nature = natures[hash % natures.length]
    const level = levels[hash % levels.length]
    const issuer = issuers[hash % issuers.length]
    const status = statuses[hash % statuses.length]
    const publishYear = 2000 + (hash % 26)
    const code = `GB/T ${10000 + (hash % 9000)}-${publishYear}`
    const description = `《${baseName}》由 ${issuer} 于 ${publishYear} 年发布，属于${level}、${nature}性标准，适用于${industry}相关工程与管理场景。`
    await delay(1200)
    return [200, { name: baseName, code, uploader: '管理员', industry, nature, level, status, issuer, publishYear, description }]
  })

  mock.onPost('/api/admin/standards').reply(async (config) => {
    const file = formDataFile(config.data)
    const metadata = formDataMetadata(config.data)
    if (!file || !metadata) return [400, {}]
    await delay(1500)
    const record: StandardProperty = {
      id: `std-upload-${Date.now()}`,
      ...metadata,
      parentId: metadata.level ?? undefined,
    }
    adminStandards.unshift(record)
    return [200, record]
  })

  mock.onPost('/api/admin/standards/batch-delete').reply((config) => {
    const body = parseJsonBody(config.data)
    const ids: string[] = Array.isArray(body.ids) ? (body.ids as string[]) : []
    let deletedCount = 0
    for (const id of ids) {
      const idx = adminStandards.findIndex((s) => s.id === id)
      if (idx !== -1) {
        adminStandards.splice(idx, 1)
        deletedCount += 1
      }
    }
    return [200, deletedCount]
  })

  mock.onPost('/api/admin/standards/batch-parse').reply((config) => {
    const body = parseJsonBody(config.data)
    const ids: string[] = Array.isArray(body.ids) ? (body.ids as string[]) : []
    const results: StandardParseBatchResult[] = ids.map((id) => {
      const standard = adminStandards.find((s) => s.id === id)
      if (!standard) return { id, success: false, error: '标准不存在' }
      return { id, success: true, analysis: buildAnalysis(standard) }
    })
    return [200, results]
  })

  mock.onDelete(/\/api\/admin\/standards\/.+$/).reply((config) => {
    const id = matchPattern(config.url, '/api/admin/standards')
    if (!id) return [404, {}]
    const idx = adminStandards.findIndex((s) => s.id === id)
    if (idx === -1) return [404, {}]
    adminStandards.splice(idx, 1)
    return [204]
  })

  mock.onPut(/\/api\/admin\/standards\/.+$/).reply((config) => {
    const id = matchPattern(config.url, '/api/admin/standards')
    if (!id) return [404, {}]
    const idx = adminStandards.findIndex((s) => s.id === id)
    if (idx === -1) return [404, {}]
    const body = parseJsonBody(config.data)
    adminStandards[idx] = { ...adminStandards[idx], ...body, id }
    return [200, adminStandards[idx]]
  })

  mock.onPost(/\/api\/admin\/standards\/.+\/parse$/).reply((config) => {
    const id = matchPattern(config.url, '/api/admin/standards')
    if (!id) return [404, {}]
    const standard = adminStandards.find((s) => s.id === id)
    if (!standard) return [404, {}]
    return [200, buildAnalysis(standard)]
  })

  mock.onPut(/\/api\/admin\/standards\/.+\/enabled$/).reply((config) => {
    const id = matchPattern(config.url, '/api/admin/standards')?.split('/')[0]
    if (!id) return [404, {}]
    const idx = adminStandards.findIndex((s) => s.id === id)
    if (idx === -1) return [404, {}]
    const body = parseJsonBody(config.data)
    adminStandards[idx] = { ...adminStandards[idx], isEnabled: body.isEnabled === true }
    return [200, adminStandards[idx]]
  })
}
