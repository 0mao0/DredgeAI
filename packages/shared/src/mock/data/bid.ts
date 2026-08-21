import type { BidReviewSession } from '@shared/types'

export const bidReviewSessions: BidReviewSession[] = [
  { id: 's-1', document: 'XX_项目_招标文件.pdf', date: '2026-07-17 14:32', riskCount: 5, status: '进行中', snippets: [
    { role: 'user', content: '重点检查第 3 章和第 8 章' },
    { role: 'assistant', content: '已识别 5 项风险，其中高风险 1 项、中风险 2 项、低风险 2 项。详见右侧风险面板。' },
  ] },
  { id: 's-2', document: 'YY_工程_投标文件.pdf', date: '2026-07-15 09:18', riskCount: 5, status: '已完成' },
  { id: 's-3', document: 'ZZ_项目_招标文件_v2.pdf', date: '2026-07-12 16:40', riskCount: 2, status: '已完成' },
  { id: 's-4', document: 'WW_改造_招标文件.pdf', date: '2026-07-08 11:05', riskCount: 0, status: '已完成' },
  { id: 's-5', document: 'AA_咨询_招标文件.pdf', date: '2026-07-03 14:22', riskCount: 3, status: '已完成' },
]
