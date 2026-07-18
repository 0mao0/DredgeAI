import type { Notification } from '@/types'

export const notifications: Notification[] = [
  { id: 'n-1', type: 'business', title: 'AI 审标任务完成', content: 'XX 项目招标文件已完成风险分析，共识别 5 项风险', time: '2026-07-17 14:35', read: false },
  { id: 'n-2', type: 'system', title: '系统维护通知', content: '今晚 23:00-次日 02:00 系统例行维护，期间服务可能中断', time: '2026-07-17 10:00', read: false },
  { id: 'n-3', type: 'audit', title: 'API Key 创建', content: '您于 2026-07-01 创建了 "第三方集成" Key', time: '2026-07-01 09:30', read: true },
  { id: 'n-4', type: 'business', title: '标准查询结果', content: '您查询的 "施工质量验收标准" 已命中 5 条标准', time: '2026-07-16 14:40', read: true },
  { id: 'n-5', type: 'system', title: '权限更新', content: '管理员已为您开通 "数据看板" 应用权限', time: '2026-07-15 16:20', read: true },
  { id: 'n-6', type: 'audit', title: '登录提醒', content: '您的账号于 2026-07-15 08:50 在新设备登录', time: '2026-07-15 08:50', read: true },
  { id: 'n-7', type: 'business', title: '合同审查报告生成', content: '供应商协议 v3 审查报告已生成，共 3 处风险', time: '2026-07-15 11:25', read: true },
  { id: 'n-8', type: 'system', title: '版本更新', content: 'AI 审标应用已升级到 v2.3.1', time: '2026-07-14 18:00', read: true },
]
