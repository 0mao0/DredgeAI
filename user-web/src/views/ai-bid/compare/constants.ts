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
