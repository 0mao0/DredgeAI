import type { AppCard } from '@/types'

export const categories = [
  { key: 'all', label: '全部' },
  { key: '日常办公', label: '日常办公' },
  { key: '专业业务', label: '专业业务' },
  { key: '知识查询', label: '知识查询' },
  { key: '开发接口', label: '开发接口' },
] as const

export const appCards: AppCard[] = [
  { id: '1', title: 'AI 审标', description: '智能识别招标文件中的风险条款与偏差', category: '专业业务', icon: 'FileSearchOutlined', status: '已授权', route: '/bid-review', version: 'v2.3.1', pinned: true },
  { id: '2', title: '标准查询', description: '自然语言检索行业标准与规范条款', category: '知识查询', icon: 'BookOutlined', status: '已授权', route: '/standards', version: 'v1.5.0', pinned: true },
  { id: '3', title: '智能写作', description: 'AI 辅助撰写工程报告与文档', category: '日常办公', icon: 'EditOutlined', status: '已授权', version: 'v3.0.2' },
  { id: '4', title: '合同审查', description: '自动识别合同风险条款与合规问题', category: '专业业务', icon: 'SafetyOutlined', status: '已授权', version: 'v1.2.0' },
  { id: '5', title: '数据看板', description: '可视化展示项目关键指标与趋势', category: '日常办公', icon: 'DashboardOutlined', status: '已授权', version: 'v1.8.0' },
  { id: '6', title: 'API 网关', description: '统一管理第三方 AI 服务接入与调用', category: '开发接口', icon: 'ApiOutlined', status: '待申请', route: '/api' },
  { id: '7', title: '知识问答', description: '基于企业知识库的智能问答系统', category: '知识查询', icon: 'QuestionCircleOutlined', status: '已授权', version: 'v2.0.0' },
  { id: '8', title: '文档比对', description: '智能比对多个文档版本差异', category: '专业业务', icon: 'SwapOutlined', status: '待申请' },
  { id: '9', title: '代码助手', description: '面向研发团队的智能代码补全与审查', category: '开发接口', icon: 'CodeOutlined', status: '待申请' },
  { id: '10', title: '会议纪要', description: '自动生成会议摘要与待办事项', category: '日常办公', icon: 'TeamOutlined', status: '已授权', version: 'v1.1.0' },
]
