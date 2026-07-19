import type { BidReviewStep, RiskItem, BidReviewSession } from '@shared/types'

export const bidReviewSteps: BidReviewStep[] = [
  { title: '上传文档', description: '支持 PDF/Word 格式，单文件 ≤ 50MB', status: 'finish' },
  { title: '智能识别', description: 'AI 识别关键条款与结构化信息', status: 'finish' },
  { title: '风险分析', description: '风险分级与处置建议', status: 'process' },
  { title: '输出报告', description: '导出分析报告与原文标注', status: 'wait' },
]

export const riskItems: RiskItem[] = [
  { id: 'r-1', level: '高风险', content: '投标截止时间与法定节假日冲突，可能导致无效投标', source: '第3章 2.1节', suggestion: '建议核实节假日安排并申请延期' },
  { id: 'r-2', level: '中风险', content: '资质要求中"近三年"定义不明确，存在歧义', source: '第5章 1.3节', suggestion: '建议在投标澄清阶段提出书面询问' },
  { id: 'r-3', level: '中风险', content: '技术评分项权重过高，可能影响商务竞争力', source: '第6章 4.1节', suggestion: '建议加强技术方案论证' },
  { id: 'r-4', level: '低风险', content: '付款条款中质保金比例高于行业惯例', source: '第8章 4.2节', suggestion: '可在商务谈判中协商调整' },
  { id: 'r-5', level: '低风险', content: '履约保证金缴纳期限偏短', source: '第9章 2.3节', suggestion: '建议提前做好资金安排' },
]

export const bidReviewSessions: BidReviewSession[] = [
  { id: 's-1', document: 'XX_项目_招标文件.pdf', date: '2026-07-17 14:32', riskCount: 5, status: '进行中', snippets: [
    { role: 'user', content: '重点检查第 3 章和第 8 章' },
    { role: 'assistant', content: '已识别 5 项风险，其中高风险 1 项、中风险 2 项、低风险 2 项。详见右侧风险面板。' },
  ]},
  { id: 's-2', document: 'YY_工程_投标文件.pdf', date: '2026-07-15 09:18', riskCount: 5, status: '已完成' },
  { id: 's-3', document: 'ZZ_项目_招标文件_v2.pdf', date: '2026-07-12 16:40', riskCount: 2, status: '已完成' },
  { id: 's-4', document: 'WW_改造_招标文件.pdf', date: '2026-07-08 11:05', riskCount: 0, status: '已完成' },
  { id: 's-5', document: 'AA_咨询_招标文件.pdf', date: '2026-07-03 14:22', riskCount: 3, status: '已完成' },
]

export const bidDocumentExcerpt = `第三章 投标文件编制

2.1 投标截止时间
投标文件应于 2026 年 8 月 15 日 17:00 前送达指定地点，逾期不予受理。

第五章 资格审查

1.3 资质要求
投标人须具备近三年内类似项目业绩不少于 3 项，且具有相应资质等级。

第六章 评标办法

4.1 评分权重
技术评分 60%，商务评分 30%，价格评分 10%。

第八章 合同条款

4.2 质保金
质保金为合同总价的 5%，质保期满后无息返还。`
