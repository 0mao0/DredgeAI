import type { EvidenceItem } from '@/types'

const FIELD_LABELS: Record<string, string> = {
  author: '作者',
  createdAt: '创建时间',
  creatorTool: '创建工具',
}

/** 把后端证据 metrics 转成一行行可读的关键指标，用于卡片/表格的细节展示 */
export function evidenceMetricLines(ev: EvidenceItem): string[] {
  const m = ev.metrics
  if (!m) return []

  const lines: string[] = []

  if (typeof m.similarity === 'number') {
    lines.push(`相似度 ${(m.similarity * 100).toFixed(1)}%`)
  }
  if (typeof m.matchedBlockCount === 'number') {
    lines.push(`匹配块 ${m.matchedBlockCount}`)
  }
  if (m.ocrSuspect) {
    lines.push('OCR 低置信，已降权')
  }
  if (m.cluster) {
    const avg = typeof m.avgSimilarity === 'number' ? `${(m.avgSimilarity * 100).toFixed(1)}%` : ''
    lines.push(`共同雷同 ${m.memberCount} 份${avg ? ` · 平均 ${avg}` : ''}`)
  }

  if (m.pattern === 'arithmetic' && typeof m.commonDiff === 'number') {
    lines.push(`报价等差 · 公差约 ${Math.round(m.commonDiff).toLocaleString()} 元`)
  }
  if (m.pattern === 'tail' && typeof m.tail === 'string') {
    lines.push(`报价尾数相同 · 末两位 ${m.tail}`)
  }
  if (m.pattern === 'closeness' && typeof m.spreadRatio === 'number') {
    lines.push(`报价贴近 · 最大偏差 ${(m.spreadRatio * 100).toFixed(2)}%`)
  }

  if (m.field && typeof m.value === 'string') {
    const field = typeof m.field === 'string' ? m.field : ''
    lines.push(`${FIELD_LABELS[field] ?? field}一致 · ${m.value}`)
  }
  if (m.pattern === 'shared-typo' && typeof m.sharedNgramCount === 'number') {
    lines.push(`相同错别字/异常字串 ${m.sharedNgramCount} 处`)
  }

  if (m.status === 'partial') lines.push('部分响应')
  if (m.status === 'none') lines.push('未响应')

  return lines
}

export const EVIDENCE_TYPE_META: Record<EvidenceItem['type'], { label: string, color: string }> = {
  similarity: { label: '雷同', color: 'red' },
  price: { label: '报价', color: 'orange' },
  metadata: { label: '属性信息', color: 'blue' },
  clause: { label: '条款', color: 'green' },
  indicator: { label: '指标', color: 'purple' },
}

export const SEVERITY_META: Record<EvidenceItem['severity'], { label: string, color: string }> = {
  high: { label: '高风险', color: '#EF4444' },
  mid: { label: '中风险', color: '#F59E0B' },
  low: { label: '低风险', color: '#3B82F6' },
}
