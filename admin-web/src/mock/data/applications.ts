import type { ApplicationItem } from '@/types'

/** 10 个应用模块，与 user-web 共享同一套数据 */
export const mockApplications: ApplicationItem[] = [
  { id: '1', name: '智能审批助手', category: '日常办公', manager: '张三', version: 'v2.3.1', status: '运营中', userCount: 856, apiCalls: 128000, createdAt: '2026-01-15' },
  { id: '2', name: '知识检索系统', category: '知识查询', manager: '李四', version: 'v1.8.0', status: '运营中', userCount: 1240, apiCalls: 256000, createdAt: '2026-02-20' },
  { id: '3', name: '标书智能分析', category: '专业业务', manager: '王五', version: 'v3.0.2', status: '运营中', userCount: 312, apiCalls: 89000, createdAt: '2026-03-10' },
  { id: '4', name: '数据洞察助手', category: '专业业务', manager: '赵六', version: 'v1.2.0', status: '开发中', userCount: 56, apiCalls: 12000, createdAt: '2026-05-01' },
  { id: '5', name: '合规检查工具', category: '专业业务', manager: '孙七', version: 'v0.9.0', status: '开发中', userCount: 28, apiCalls: 4500, createdAt: '2026-06-15' },
  { id: '6', name: '开发接口网关', category: '开发接口', manager: '周八', version: 'v2.0.0', status: '运营中', userCount: 89, apiCalls: 520000, createdAt: '2025-12-01' },
  { id: '7', name: '图纸合规检查', category: '专业业务', manager: '吴九', version: 'v1.4.0', status: '运营中', userCount: 520, apiCalls: 76000, createdAt: '2026-02-01' },
  { id: '8', name: '安全风险监控', category: '专业业务', manager: '郑十', version: 'v1.6.0', status: '运营中', userCount: 430, apiCalls: 65000, createdAt: '2026-03-01' },
  { id: '9', name: '成本动态分析', category: '专业业务', manager: '冯十一', version: 'v1.2.0', status: '运营中', userCount: 280, apiCalls: 42000, createdAt: '2026-04-15' },
  { id: '10', name: '知识问答', category: '知识查询', manager: '陈十二', version: 'v2.0.0', status: '运营中', userCount: 1890, apiCalls: 320000, createdAt: '2026-01-01' },
]
