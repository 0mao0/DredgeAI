import type { ApiKey, ModelType, UsageByModel, UsageByKey, UsageTimeSeries } from '@shared/types'

/** admin 侧 Key：字段更全（含 app / expiredAt / lastUsed） */
export const adminApiKeys: ApiKey[] = [
  {
    id: '1',
    name: '生产环境-主入口',
    key: 'sk-dredge-9f2a****c1b4',
    fullKey: 'sk-dredge-9f2a3e7d8b90c1b4',
    modelType: 'GPT-4o',
    app: '智浚 AI 平台',
    status: '启用',
    createdAt: '2025-03-12',
    expiredAt: '2027-03-12',
    lastUsed: '2026-07-18 09:12',
    quota: 10000000,
    usage: 7250000,
    docUrl: 'https://docs.dredgeai.com/api/gpt4o',
  },
  {
    id: '2',
    name: '用量统计采集器',
    key: 'sk-dredge-3e7d****8a90',
    fullKey: 'sk-dredge-3e7dba4567c18a90',
    modelType: 'Claude 3 Haiku',
    app: '数据中台',
    status: '启用',
    createdAt: '2025-05-20',
    lastUsed: '2026-07-18 08:55',
    quota: 5000000,
    usage: 3120000,
    docUrl: 'https://docs.dredgeai.com/api/haiku',
  },
  {
    id: '3',
    name: 'BIM 分析专用',
    key: 'sk-dredge-61c2****4f3e',
    fullKey: 'sk-dredge-61c20e8c5b674f3e',
    modelType: 'GPT-4o',
    app: 'BIM 智能分析',
    status: '启用',
    createdAt: '2025-06-08',
    lastUsed: '2026-07-17 22:40',
    quota: 3000000,
    usage: 1840000,
    docUrl: 'https://docs.dredgeai.com/api/gpt4o',
  },
  {
    id: '4',
    name: '测试环境-内部',
    key: 'sk-dredge-ba45****2d18',
    fullKey: 'sk-dredge-ba45d4f19a2e2d18',
    modelType: 'Claude 3.5 Sonnet',
    app: '内部测试',
    status: '启用',
    createdAt: '2025-08-01',
    lastUsed: '2026-07-18 10:02',
    quota: 1000000,
    usage: 460000,
    docUrl: 'https://docs.dredgeai.com/api/claude',
  },
  {
    id: '5',
    name: '海外节点-新加坡',
    key: 'sk-dredge-77f9****e0c3',
    fullKey: 'sk-dredge-77f9m3n4e0c3q1r2',
    modelType: 'GPT-4o',
    app: '海外业务',
    status: '禁用',
    createdAt: '2025-09-15',
    lastUsed: '2026-06-30 14:20',
    quota: 2000000,
    usage: 920000,
    docUrl: 'https://docs.dredgeai.com/api/gpt4o',
  },
  {
    id: '6',
    name: '文档分析-合同审查',
    key: 'sk-dredge-0e8c****5b67',
    fullKey: 'sk-dredge-0e8cs3t45b67u5v6',
    modelType: 'Claude 3.5 Sonnet',
    app: '合同审查',
    status: '启用',
    createdAt: '2025-10-22',
    lastUsed: '2026-07-18 09:30',
    quota: 2000000,
    usage: 1230000,
    docUrl: 'https://docs.dredgeai.com/api/claude',
  },
  {
    id: '7',
    name: 'AI 审标专用',
    key: 'sk-dredge-d4f1****9a2e',
    fullKey: 'sk-dredge-d4f1o9p09a2eq1r2',
    modelType: 'GPT-4o',
    app: 'AI 审标',
    status: '启用',
    createdAt: '2025-11-03',
    lastUsed: '2026-07-18 08:40',
    quota: 1500000,
    usage: 890000,
    docUrl: 'https://docs.dredgeai.com/api/gpt4o',
  },
]

/** user 侧 Key：无 app 字段 */
export const userApiKeys: ApiKey[] = [
  { id: 'k-1', name: '生产环境-主入口', key: 'sk-dg-****-a1b2', fullKey: 'sk-dg-prod-a1b2c3d4e5f6', modelType: 'GPT-4o', createdAt: '2026-06-01', status: '启用', usage: 12500, quota: 50000, docUrl: 'https://docs.dredgeai.com/api/gpt4o' },
  { id: 'k-2', name: '测试环境-内部', key: 'sk-dg-****-f6e5', fullKey: 'sk-dg-test-f6e5d4c3b2a1', modelType: 'Claude 3.5 Sonnet', createdAt: '2026-06-15', status: '启用', usage: 8300, quota: 20000, docUrl: 'https://docs.dredgeai.com/api/claude' },
  { id: 'k-3', name: '第三方集成-合作商A', key: 'sk-dg-****-x7y8', fullKey: 'sk-dg-integ-x7y8z9a0b1c2', modelType: 'DeepSeek-V3', createdAt: '2026-07-01', status: '禁用', usage: 0, quota: 10000, docUrl: 'https://docs.dredgeai.com/api/deepseek' },
  { id: 'k-4', name: 'AI 审标专用', key: 'sk-dg-****-m3n4', fullKey: 'sk-dg-review-m3n4o5p6q7r8', modelType: 'GPT-4o', createdAt: '2026-06-20', status: '启用', usage: 5600, quota: 30000, docUrl: 'https://docs.dredgeai.com/api/gpt4o' },
  { id: 'k-5', name: '本地部署-推理', key: 'sk-dg-****-p9q0', fullKey: 'sk-dg-local-p9q0r1s2t3u4', modelType: '本地模型', createdAt: '2026-05-10', status: '启用', usage: 4200, quota: 15000, docUrl: 'https://docs.dredgeai.com/api/local' },
  { id: 'k-6', name: '通义测试-千问', key: 'sk-dg-****-v5w6', fullKey: 'sk-dg-qwen-v5w6x7y8z9a0', modelType: '通义千问-Max', createdAt: '2026-07-05', status: '启用', usage: 1600, quota: 10000, docUrl: 'https://docs.dredgeai.com/api/qwen' },
  { id: 'k-7', name: 'BIM 分析专用', key: 'sk-dg-****-c7d8', fullKey: 'sk-dg-bim-c7d8e9f0g1h2', modelType: 'GPT-4o', createdAt: '2026-04-10', status: '启用', usage: 9800, quota: 40000, docUrl: 'https://docs.dredgeai.com/api/gpt4o' },
  { id: 'k-8', name: '文档分析-合同审查', key: 'sk-dg-****-e9f0', fullKey: 'sk-dg-contract-e9f0g1h2i3j4', modelType: 'Claude 3.5 Sonnet', createdAt: '2026-05-20', status: '启用', usage: 6200, quota: 25000, docUrl: 'https://docs.dredgeai.com/api/claude' },
  { id: 'k-9', name: '图片识别-现场巡检', key: 'sk-dg-****-g1h2', fullKey: 'sk-dg-vision-g1h2i3j4k5l6', modelType: 'GPT-4o Vision', createdAt: '2026-06-25', status: '启用', usage: 3400, quota: 20000, docUrl: 'https://docs.dredgeai.com/api/vision' },
  { id: 'k-10', name: '跨部门测试环境', key: 'sk-dg-****-i3j4', fullKey: 'sk-dg-cross-i3j4k5l6m7n8', modelType: 'DeepSeek-V3', createdAt: '2026-07-10', status: '启用', usage: 2100, quota: 8000, docUrl: 'https://docs.dredgeai.com/api/deepseek' },
  { id: 'k-11', name: '供应商-数据对接', key: 'sk-dg-****-k5l6', fullKey: 'sk-dg-vendor-k5l6m7n8o9p0', modelType: '通义千问-Max', createdAt: '2026-06-05', status: '禁用', usage: 450, quota: 5000, docUrl: 'https://docs.dredgeai.com/api/qwen' },
  { id: 'k-12', name: '用量统计采集器', key: 'sk-dg-****-m7n8', fullKey: 'sk-dg-metrics-m7n8o9p0q1r2', modelType: 'Claude 3 Haiku', createdAt: '2026-07-15', status: '启用', usage: 15000, quota: 60000, docUrl: 'https://docs.dredgeai.com/api/haiku' },
  { id: 'k-13', name: '安全审计专用', key: 'sk-dg-****-o9p0', fullKey: 'sk-dg-audit-o9p0q1r2s3t4', modelType: '本地模型', createdAt: '2026-03-01', status: '启用', usage: 800, quota: 5000, docUrl: 'https://docs.dredgeai.com/api/local' },
  { id: 'k-14', name: '海外节点-新加坡', key: 'sk-dg-****-q1r2', fullKey: 'sk-dg-sg-q1r2s3t4u5v6', modelType: 'GPT-4o', createdAt: '2026-07-20', status: '启用', usage: 7200, quota: 35000, docUrl: 'https://docs.dredgeai.com/api/gpt4o' },
  { id: 'k-15', name: '开发调试-临时', key: 'sk-dg-****-s3t4', fullKey: 'sk-dg-dev-s3t4u5v6w7x8', modelType: 'Claude 3.5 Sonnet', createdAt: '2026-07-22', status: '启用', usage: 2800, quota: 10000, docUrl: 'https://docs.dredgeai.com/api/claude' },
]

/** 兼容别名：admin/user 路由各自按需引用 */
export const apiKeys = adminApiKeys

export const modelTypes: ModelType[] = [
  { id: 'gpt4o', name: 'GPT-4o', provider: 'OpenAI', description: '通用旗舰模型，适合复杂推理与多模态' },
  { id: 'gpt4o-vision', name: 'GPT-4o Vision', provider: 'OpenAI', description: '图像理解与分析，支持施工图纸识别' },
  { id: 'claude35', name: 'Claude 3.5 Sonnet', provider: 'Anthropic', description: '长文本与代码能力突出，适合合同审查' },
  { id: 'claude-haiku', name: 'Claude 3 Haiku', provider: 'Anthropic', description: '轻量快速，高并发低延迟场景' },
  { id: 'deepseek', name: 'DeepSeek-V3', provider: 'DeepSeek', description: '国产高性价比模型，适合通用对话' },
  { id: 'qwen', name: '通义千问-Max', provider: '阿里云', description: '中文场景深度优化，行业知识增强' },
  { id: 'qwen-code', name: '通义千问-Code', provider: '阿里云', description: '代码生成与审查专用模型' },
  { id: 'glm', name: 'GLM-4-Plus', provider: '智谱AI', description: '中英双语能力均衡，适合信息抽取' },
  { id: 'local', name: '本地模型', provider: '自部署', description: '数据不出域的私有部署方案' },
  { id: 'embedding', name: 'Embedding-v3', provider: '自部署', description: '文本向量化，用于知识库语义检索' },
]

export const usageByModel: UsageByModel[] = [
  { modelName: 'GPT-4o', calls: 24700, share: 34 },
  { modelName: 'Claude 3.5 Sonnet', calls: 14500, share: 20 },
  { modelName: '本地模型', calls: 5000, share: 7 },
  { modelName: 'GPT-4o Vision', calls: 3400, share: 5 },
  { modelName: '通义千问-Max', calls: 2050, share: 3 },
  { modelName: 'DeepSeek-V3', calls: 2100, share: 3 },
  { modelName: 'Claude 3 Haiku', calls: 15000, share: 21 },
  { modelName: 'Embedding-v3', calls: 3800, share: 5 },
  { modelName: '通义千问-Code', calls: 900, share: 1 },
  { modelName: 'GLM-4-Plus', calls: 750, share: 1 },
]

export const usageByKey: UsageByKey[] = [
  { keyName: '生产环境-主入口', calls: 12500, share: 18 },
  { keyName: '用量统计采集器', calls: 15000, share: 22 },
  { keyName: '测试环境-内部', calls: 8300, share: 12 },
  { keyName: 'BIM 分析专用', calls: 9800, share: 14 },
  { keyName: 'AI 审标专用', calls: 5600, share: 8 },
  { keyName: '文档分析-合同审查', calls: 6200, share: 9 },
  { keyName: '海外节点-新加坡', calls: 7200, share: 10 },
  { keyName: '图片识别-现场巡检', calls: 3400, share: 5 },
  { keyName: '本地部署-推理', calls: 4200, share: 6 },
  { keyName: '开发调试-临时', calls: 2800, share: 4 },
  { keyName: '跨部门测试环境', calls: 2100, share: 3 },
  { keyName: '通义测试-千问', calls: 1600, share: 2 },
  { keyName: '安全审计专用', calls: 800, share: 1 },
  { keyName: '供应商-数据对接', calls: 450, share: 1 },
]

function randomBetween(min: number, max: number): number {
  return Math.floor(Math.random() * (max - min + 1)) + min
}

const now = new Date()
const dayNames: string[] = []
for (let i = 6; i >= 0; i--) {
  const d = new Date(now)
  d.setDate(d.getDate() - i)
  dayNames.push(`${d.getMonth() + 1}/${d.getDate()}`)
}

export const usageTimeSeries: UsageTimeSeries = {
  categories: dayNames,
  byModel: [
    { modelName: 'GPT-4o', data: dayNames.map(() => randomBetween(1200, 3500)) },
    { modelName: 'Claude 3.5 Sonnet', data: dayNames.map(() => randomBetween(600, 2000)) },
    { modelName: 'Claude 3 Haiku', data: dayNames.map(() => randomBetween(800, 2500)) },
    { modelName: '本地模型', data: dayNames.map(() => randomBetween(200, 800)) },
    { modelName: '通义千问-Max', data: dayNames.map(() => randomBetween(50, 400)) },
    { modelName: 'DeepSeek-V3', data: dayNames.map(() => randomBetween(50, 300)) },
  ],
  byKey: [
    { keyName: '生产环境-主入口', data: dayNames.map(() => randomBetween(800, 2200)) },
    { keyName: '用量统计采集器', data: dayNames.map(() => randomBetween(600, 2500)) },
    { keyName: 'BIM 分析专用', data: dayNames.map(() => randomBetween(500, 1800)) },
    { keyName: '测试环境-内部', data: dayNames.map(() => randomBetween(400, 1500)) },
    { keyName: '海外节点-新加坡', data: dayNames.map(() => randomBetween(300, 1200)) },
    { keyName: '文档分析-合同审查', data: dayNames.map(() => randomBetween(200, 1000)) },
    { keyName: 'AI 审标专用', data: dayNames.map(() => randomBetween(200, 900)) },
    { keyName: '本地部署-推理', data: dayNames.map(() => randomBetween(100, 600)) },
    { keyName: '图片识别-现场巡检', data: dayNames.map(() => randomBetween(100, 500)) },
    { keyName: '开发调试-临时', data: dayNames.map(() => randomBetween(50, 400)) },
  ],
  byName: [
    { name: '生产环境-主入口', data: dayNames.map(() => randomBetween(800, 2200)) },
    { name: '用量统计采集器', data: dayNames.map(() => randomBetween(600, 2500)) },
    { name: 'BIM 分析专用', data: dayNames.map(() => randomBetween(500, 1800)) },
    { name: '测试环境-内部', data: dayNames.map(() => randomBetween(400, 1500)) },
    { name: '海外节点-新加坡', data: dayNames.map(() => randomBetween(300, 1200)) },
    { name: '文档分析-合同审查', data: dayNames.map(() => randomBetween(200, 1000)) },
  ],
}
