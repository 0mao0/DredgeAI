import type { CompareTaskStatus } from '@/types'

/** 任务状态 → 展示颜色/文案（index / 分析页 / 历史抽屉共用） */
export const COMPARE_STATUS_MAP: Record<CompareTaskStatus, { color: string, text: string }> = {
  uploading: { color: 'default', text: '上传中' },
  parsing: { color: 'blue', text: '解析中' },
  comparing: { color: 'blue', text: '比对中' },
  ai_analyzing: { color: 'blue', text: 'AI 分析中' },
  completed: { color: 'green', text: '已完成' },
  partial: { color: 'orange', text: '部分完成' },
  failed: { color: 'red', text: '失败' },
}

/** 比标文档别名（A~H）标签色板，与现有风险/状态色系保持一致。 */
const DOC_BADGE_COLORS: Record<string, string> = {
  A: '#2563EB',
  B: '#0EA5E9',
  C: '#F59E0B',
  D: '#8B5CF6',
  E: '#10B981',
  F: '#EF4444',
  G: '#D97706',
  H: '#3B82F6',
}

/** 取别名标签底色：A~H 取专属色，其余（招标/未知）回落品牌色。 */
export function docBadgeColor(label: string): string {
  const key = (label || '').trim().charAt(0).toUpperCase()
  return DOC_BADGE_COLORS[key] ?? '#0EA5E9'
}

export function isTerminalStatus(status: CompareTaskStatus): boolean {
  return status === 'completed' || status === 'partial' || status === 'failed'
}

/** AnGIneer 解析管线阶段顺序（DredgeAI 以 stages=all 调用），用于把 stage 映射成“第几步/百分比”。 */
export const AN_GINEER_PARSE_STAGES = [
  'source_prep',
  'convert',
  'raw_parse',
  'popo',
  'structure',
  'fts',
  'vectors',
  'graph',
] as const

/**
 * 由 AnGIneer stage 推导单文档解析进度：
 * step = 当前是第几步（1 起）；percent = 已完成阶段占比（0~100，当前阶段尚未计入）。
 * 无法识别的 stage（queued 等）返回 null，由调用方回退到 AnGIneer 原始 progress。
 */
export function anGineerStepInfo(stage: string | null | undefined): { step: number, total: number, percent: number } | null {
  if (!stage) return null
  const index = (AN_GINEER_PARSE_STAGES as readonly string[]).indexOf(stage)
  if (index < 0) return null
  const total = AN_GINEER_PARSE_STAGES.length
  return {
    step: index + 1,
    total,
    percent: Math.round((index / total) * 100),
  }
}
export { buildDocLabels, docLabel, isPdfFileName, MAX_BID_DOCUMENTS, overviewDocLabels } from '@shared/core/utils/compare'

export function formatFileSize(size: number): string {
  if (size >= 1024 * 1024) return `${(size / 1024 / 1024).toFixed(1)} MB`
  if (size >= 1024) return `${(size / 1024).toFixed(0)} KB`
  return `${size} B`
}

/** 客户端候选名：取文件名公共前缀，去除「投标文件 / 报价文件 / 标书」后缀，不超过 20 字。 */
export function deriveProjectName(fileNames: string[]): string {
  const stems = fileNames.map((n) => n.replace(/\.[^.]+$/, ''))
  if (!stems.length) return ''
  let prefix = stems[0]
  for (const s of stems.slice(1)) {
    while (prefix && !s.startsWith(prefix)) prefix = prefix.slice(0, -1)
  }
  const cleaned = prefix.replace(/(投标文件|报价文件|标书)+[\s_（(]?[A-E甲丁乙丙]?[)）]?$/g, '').trim()
  const base = cleaned.length >= 2 ? cleaned : stems[0]
  return base.slice(0, 20)
}

/** 解析完成后生成项目名：xxx项目-比标-N本（N = 投标文件份数）。 */
export function formatProjectName(suggestedName: string, bidCount: number): string {
  const base = suggestedName
    .trim()
    .replace(/项目-比标-\d+本$/, '')
    .replace(/项目$/, '')
    .trim()
    .slice(0, 80)
  return `${base || '比标项目'}项目-比标-${bidCount}本`
}
