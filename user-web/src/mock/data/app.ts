import type { AppCard } from '@/types'

/** 10 个应用模块，与 admin-web 共享同一套数据。测试用户已全部授权，默认勾选 3 个放入侧边栏 */
export const appCards: AppCard[] = [
  { id: '1', title: '智能审批助手', description: 'AI 辅助日常审批流程，自动识别关键节点', category: '设计', icon: 'AuditOutlined', status: '已授权', route: '/approval', version: 'v2.3.1', pinned: false },
  { id: '2', title: '知识检索系统', description: '基于企业知识库的智能问答与文档检索', category: '设计', icon: 'SearchOutlined', status: '已授权', route: '/knowledge', version: 'v1.8.0', pinned: true },
  { id: '3', title: '标书智能分析', description: '智能解析招标文件，辅助投标策略决策', category: '设计', icon: 'FileSearchOutlined', status: '已授权', route: '/bid-review', version: 'v3.0.2', pinned: true },
  { id: '4', title: '数据洞察助手', description: '项目数据可视化分析，挖掘业务洞察', category: '施工', icon: 'DashboardOutlined', status: '已授权', route: '/insight', version: 'v1.2.0', pinned: false },
  { id: '5', title: '合规检查工具', description: '自动比对施工方案与法规规范的符合性', category: '施工', icon: 'SafetyOutlined', status: '已授权', route: '/compliance', version: 'v0.9.0', pinned: false },
  { id: '6', title: '开发接口网关', description: '统一管理第三方 AI 服务接入与调用', category: '施工', icon: 'ApiOutlined', status: '已授权', route: '/api', version: 'v2.0.0', pinned: false },
  { id: '7', title: '图纸合规检查', description: '智能识别施工图纸与设计规范的偏差', category: '施工', icon: 'EditOutlined', status: '已授权', route: '/standards', version: 'v1.4.0', pinned: true },
  { id: '8', title: '安全风险监控', description: '施工现场安全隐患实时识别与报警', category: '经营', icon: 'WarningOutlined', status: '已授权', route: '/safety', version: 'v1.6.0', pinned: false },
  { id: '9', title: '成本动态分析', description: '项目成本实时归集与超支预警', category: '经营', icon: 'FundOutlined', status: '已授权', route: '/cost', version: 'v1.2.0', pinned: false },
  { id: '10', title: '知识问答', description: '基于企业知识库的智能问答与文档检索', category: '经营', icon: 'QuestionCircleOutlined', status: '已授权', route: '/qa', version: 'v2.0.0', pinned: false },
]
