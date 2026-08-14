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

export function isTerminalStatus(status: CompareTaskStatus): boolean {
  return status === 'completed' || status === 'partial' || status === 'failed'
}

/** 文档编号：标书按创建时间 A~N；招标文件不参与编号（返回「招标」）。 */
export function docLabel(documents: { id: string, role?: string }[], docId: string): string {
  const bidDocs = documents.filter((d) => d.role !== 'tender')
  const idx = bidDocs.findIndex((d) => d.id === docId)
  return idx >= 0 ? String.fromCharCode(65 + idx) : '招标'
}

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
