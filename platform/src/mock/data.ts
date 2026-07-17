import type { MetricCard, AppCard, TaskItem, FileItem, PermissionItem, UserInfo, ApiKey, KeyUsageStat, KeyUsageByKey } from '@/types'

export const userInfo: UserInfo = {
  name: '张明',
  department: '工程技术部',
  position: '高级工程师',
}

export const metricCards: MetricCard[] = [
  { title: '总调用量', value: '12,846', trend: '+12.5%', trendUp: true },
  { title: '活跃用户', value: '1,234', trend: '+8.3%', trendUp: true },
  { title: '异常告警', value: '3', trend: '-2', trendUp: false },
  { title: '待审核数据', value: '47', trend: '+12', trendUp: true },
]

export const appCards: AppCard[] = [
  { id: '1', title: 'AI 审标', description: '智能识别招标文件中的风险条款与偏差', category: '专业业务', icon: 'FileSearch', status: '已授权' },
  { id: '2', title: '标准查询', description: '自然语言检索行业标准与规范条款', category: '知识查询', icon: 'Book', status: '已授权' },
  { id: '3', title: '智能写作', description: 'AI 辅助撰写工程报告与文档', category: '日常办公', icon: 'Edit', status: '已授权' },
  { id: '4', title: '合同审查', description: '自动识别合同风险条款与合规问题', category: '专业业务', icon: 'Safety', status: '已授权' },
  { id: '5', title: '数据看板', description: '可视化展示项目关键指标与趋势', category: '日常办公', icon: 'Dashboard', status: '已授权' },
  { id: '6', title: 'API 网关', description: '统一管理第三方 AI 服务接入与调用', category: '开发接口', icon: 'Api', status: '待申请' },
  { id: '7', title: '知识问答', description: '基于企业知识库的智能问答系统', category: '知识查询', icon: 'QuestionCircle', status: '已授权' },
  { id: '8', title: '文档比对', description: '智能比对多个文档版本差异', category: '专业业务', icon: 'Swap', status: '待申请' },
]

export const taskItems: TaskItem[] = [
  { id: '1', title: 'XX 项目招标文件风险分析', status: '进行中', updatedAt: '2026-07-17' },
  { id: '2', title: 'GB/T 19001 条款匹配检查', status: '已完成', updatedAt: '2026-07-16' },
  { id: '3', title: '合同审查 - 供应商协议 v3', status: '已完成', updatedAt: '2026-07-15' },
  { id: '4', title: '技术规范文档比对', status: '进行中', updatedAt: '2026-07-14' },
]

export const fileItems: FileItem[] = [
  { id: '1', name: 'XX_项目_招标文件.pdf', type: 'pdf', updatedAt: '2026-07-17' },
  { id: '2', name: '合同审查报告_v3.docx', type: 'docx', updatedAt: '2026-07-16' },
  { id: '3', name: '标准查询结果_20260715.xlsx', type: 'xlsx', updatedAt: '2026-07-15' },
  { id: '4', name: '项目需求文档_v2.pdf', type: 'pdf', updatedAt: '2026-07-14' },
]

export const categories: { key: string; label: string }[] = [
  { key: 'all', label: '全部' },
  { key: '日常办公', label: '日常办公' },
  { key: '专业业务', label: '专业业务' },
  { key: '知识查询', label: '知识查询' },
  { key: '开发接口', label: '开发接口' },
]

export const permissions: PermissionItem[] = [
  { id: '1', role: '超级管理员', menu: '全部菜单', app: '全部应用', data: '全部数据' },
  { id: '2', role: '平台管理员', menu: '管理端全部', app: '全部应用', data: '全部数据' },
  { id: '3', role: '部门主管', menu: 'userWeb + 管理台', app: '本部门应用', data: '本部门数据' },
  { id: '4', role: '高级工程师', menu: 'userWeb', app: '专业业务应用', data: '本人数据' },
  { id: '5', role: '普通员工', menu: 'userWeb（受限）', app: '基础应用', data: '本人数据' },
]

export const bidReviewSteps = [
  { title: '上传文档', description: '支持 PDF/Word 格式' },
  { title: '智能识别', description: 'AI 识别关键条款' },
  { title: '风险分析', description: '风险分级与建议' },
  { title: '输出报告', description: '导出分析报告' },
]

export const riskItems = [
  { level: '高风险', content: '投标截止时间与法定节假日冲突，可能导致无效投标', source: '第3章 2.1节' },
  { level: '中风险', content: '资质要求中"近三年"定义不明确，存在歧义', source: '第5章 1.3节' },
  { level: '低风险', content: '付款条款中质保金比例高于行业惯例', source: '第8章 4.2节' },
]

export const bidReviewHistory = [
  { id: '1', document: 'XX_项目_招标文件.pdf', date: '2026-07-17', riskCount: 3, status: '已完成' },
  { id: '2', document: 'YY_工程_投标文件.pdf', date: '2026-07-15', riskCount: 5, status: '已完成' },
  { id: '3', document: 'ZZ_项目_招标文件_v2.pdf', date: '2026-07-12', riskCount: 2, status: '已完成' },
  { id: '4', document: 'WW_改造_招标文件.pdf', date: '2026-07-08', riskCount: 0, status: '已完成' },
]

export const standardsSearchHistory = [
  { id: '1', query: 'GB/T 19001 质量管理体系', date: '2026-07-17', resultCount: 3 },
  { id: '2', query: '施工质量验收标准', date: '2026-07-16', resultCount: 5 },
  { id: '3', query: '合同审查相关规范', date: '2026-07-14', resultCount: 2 },
  { id: '4', query: '安全生产标准化', date: '2026-07-10', resultCount: 4 },
]

export const standardsResult = [
  { code: 'GB/T 19001-2016', title: '质量管理体系 要求', match: '条款 7.1.4 — 过程运行环境', excerpt: '组织应确定、提供并维护所需的过程运行环境...' },
  { code: 'GB/T 50430-2017', title: '工程建设施工企业质量管理规范', match: '条款 3.2 — 质量管理体系策划', excerpt: '施工企业应建立并实施质量管理体系...' },
  { code: 'GB 50300-2013', title: '建筑工程施工质量验收统一标准', match: '条款 4.0 — 验收基本规定', excerpt: '建筑工程施工质量应按下列要求进行验收...' },
]

export const appManagementList = [
  { id: '1', name: 'AI 审标', group: '专业业务', version: 'v2.3.1', status: '已上架', scope: '全部' },
  { id: '2', name: '标准查询', group: '知识查询', version: 'v1.5.0', status: '已上架', scope: '全部' },
  { id: '3', name: '智能写作', group: '日常办公', version: 'v3.0.2', status: '已上架', scope: '全部' },
  { id: '4', name: '合同审查', group: '专业业务', version: 'v1.2.0', status: '已上架', scope: '部门' },
  { id: '5', name: '知识问答', group: '知识查询', version: 'v2.0.0', status: '已下架', scope: '' },
  { id: '6', name: '文档比对', group: '专业业务', version: 'v0.9.0', status: '待审核', scope: '' },
]

export const dataItems = [
  { id: '1', name: 'XX项目招标文件.pdf', uploader: '张明', date: '2026-07-17', category: '招标文件', status: '待审核' },
  { id: '2', name: '合同模板_v3.docx', uploader: '李华', date: '2026-07-16', category: '合同', status: '已通过' },
  { id: '3', name: '技术规范.docx', uploader: '王芳', date: '2026-07-15', category: '技术文档', status: '已拒绝' },
  { id: '4', name: '会议纪要_0714.pdf', uploader: '赵磊', date: '2026-07-14', category: '会议记录', status: '待审核' },
  { id: '5', name: '项目计划书.xlsx', uploader: '陈静', date: '2026-07-13', category: '计划报告', status: '已通过' },
]

export const analyticsTrend = [
  { month: '2月', calls: 2850, users: 420 },
  { month: '3月', calls: 3200, users: 560 },
  { month: '4月', calls: 4100, users: 680 },
  { month: '5月', calls: 3800, users: 720 },
  { month: '6月', calls: 5200, users: 890 },
  { month: '7月', calls: 4800, users: 850 },
]

export const appRanking = [
  { name: 'AI 审标', calls: 12500, share: 35 },
  { name: '标准查询', calls: 8900, share: 25 },
  { name: '智能写作', calls: 6500, share: 18 },
  { name: '合同审查', calls: 4800, share: 13 },
  { name: '知识问答', calls: 3200, share: 9 },
]

export const modelCost = [
  { model: 'GPT-4o', cost: 12800, share: 45 },
  { model: 'Claude 3.5 Sonnet', cost: 8500, share: 30 },
  { model: '本地模型', cost: 4200, share: 15 },
  { model: '其他', cost: 2800, share: 10 },
]

export const modelTypes: { id: string; name: string }[] = [
  { id: 'gpt4o', name: 'GPT-4o' },
  { id: 'claude35', name: 'Claude 3.5 Sonnet' },
  { id: 'deepseek', name: 'DeepSeek-V3' },
  { id: 'qwen', name: '通义千问-Max' },
  { id: 'local', name: '本地模型' },
]

export const apiKeys: ApiKey[] = [
  { id: '1', name: '生产环境', key: 'sk-dg-****-a1b2', fullKey: 'sk-dg-prod-a1b2c3d4e5f6', modelType: 'GPT-4o', createdAt: '2026-06-01', status: '启用', usage: 12500, docUrl: 'https://docs.dredgeai.com/api/gpt4o' },
  { id: '2', name: '测试环境', key: 'sk-dg-****-f6e5', fullKey: 'sk-dg-test-f6e5d4c3b2a1', modelType: 'Claude 3.5 Sonnet', createdAt: '2026-06-15', status: '启用', usage: 8300, docUrl: 'https://docs.dredgeai.com/api/claude' },
  { id: '3', name: '第三方集成', key: 'sk-dg-****-x7y8', fullKey: 'sk-dg-integ-x7y8z9a0b1c2', modelType: 'DeepSeek-V3', createdAt: '2026-07-01', status: '禁用', usage: 0, docUrl: 'https://docs.dredgeai.com/api/deepseek' },
  { id: '4', name: 'AI 审标专用', key: 'sk-dg-****-m3n4', fullKey: 'sk-dg-review-m3n4o5p6q7r8', modelType: 'GPT-4o', createdAt: '2026-06-20', status: '启用', usage: 5600, docUrl: 'https://docs.dredgeai.com/api/gpt4o' },
]

export const usageByModel: KeyUsageStat[] = [
  { modelName: 'GPT-4o', calls: 18100, share: 45 },
  { modelName: 'Claude 3.5 Sonnet', calls: 8300, share: 30 },
  { modelName: 'DeepSeek-V3', calls: 2800, share: 10 },
  { modelName: '通义千问-Max', calls: 1600, share: 8 },
  { modelName: '本地模型', calls: 4200, share: 15 },
]

export const usageByKey: KeyUsageByKey[] = [
  { keyName: '生产环境', calls: 12500, share: 38 },
  { keyName: '测试环境', calls: 8300, share: 25 },
  { keyName: 'AI 审标专用', calls: 5600, share: 17 },
]
