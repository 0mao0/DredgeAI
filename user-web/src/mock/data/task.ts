import type { TaskItem } from '@/types'

export const taskItems: TaskItem[] = [
  { id: '1', title: 'XX 项目招标文件风险分析', status: '进行中', updatedAt: '2026-07-17 14:32', app: 'AI 审标', progress: 65 },
  { id: '2', title: 'GB/T 19001 条款匹配检查', status: '已完成', updatedAt: '2026-07-16 16:08', app: '标准查询', progress: 100 },
  { id: '3', title: '合同审查 - 供应商协议 v3', status: '已完成', updatedAt: '2026-07-15 11:24', app: '合同审查', progress: 100 },
  { id: '4', title: '技术规范文档比对', status: '进行中', updatedAt: '2026-07-14 09:50', app: '文档比对', progress: 40 },
  { id: '5', title: '本周工程报告撰写', status: '已暂停', updatedAt: '2026-07-13 17:15', app: '智能写作', progress: 30 },
  { id: '6', title: 'Q2 项目数据看板生成', status: '已完成', updatedAt: '2026-07-12 10:30', app: '数据看板', progress: 100 },
]

export const quickTasks = [
  { id: 'qt-1', title: '开始 AI 审标', tag: '专业业务', route: '/bid-review', icon: 'FileSearchOutlined' },
  { id: 'qt-2', title: '查询标准条款', tag: '知识查询', route: '/standards', icon: 'BookOutlined' },
  { id: 'qt-3', title: '撰写工程报告', tag: '日常办公', route: '/apps', icon: 'EditOutlined' },
] as const
