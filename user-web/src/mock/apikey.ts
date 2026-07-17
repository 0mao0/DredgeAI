import type { ApiKey, ModelType, UsageByModel, UsageByKey } from '@/types'

export const apiKeys: ApiKey[] = [
  { id: 'k-1', name: '生产环境', key: 'sk-dg-****-a1b2', fullKey: 'sk-dg-prod-a1b2c3d4e5f6', modelType: 'GPT-4o', createdAt: '2026-06-01', status: '启用', usage: 12500, quota: 50000, docUrl: 'https://docs.dredgeai.com/api/gpt4o' },
  { id: 'k-2', name: '测试环境', key: 'sk-dg-****-f6e5', fullKey: 'sk-dg-test-f6e5d4c3b2a1', modelType: 'Claude 3.5 Sonnet', createdAt: '2026-06-15', status: '启用', usage: 8300, quota: 20000, docUrl: 'https://docs.dredgeai.com/api/claude' },
  { id: 'k-3', name: '第三方集成', key: 'sk-dg-****-x7y8', fullKey: 'sk-dg-integ-x7y8z9a0b1c2', modelType: 'DeepSeek-V3', createdAt: '2026-07-01', status: '禁用', usage: 0, quota: 10000, docUrl: 'https://docs.dredgeai.com/api/deepseek' },
  { id: 'k-4', name: 'AI 审标专用', key: 'sk-dg-****-m3n4', fullKey: 'sk-dg-review-m3n4o5p6q7r8', modelType: 'GPT-4o', createdAt: '2026-06-20', status: '启用', usage: 5600, quota: 30000, docUrl: 'https://docs.dredgeai.com/api/gpt4o' },
]

export const modelTypes: ModelType[] = [
  { id: 'gpt4o', name: 'GPT-4o', provider: 'OpenAI', description: '通用旗舰模型，适合复杂推理' },
  { id: 'claude35', name: 'Claude 3.5 Sonnet', provider: 'Anthropic', description: '长文本与代码能力突出' },
  { id: 'deepseek', name: 'DeepSeek-V3', provider: 'DeepSeek', description: '国产高性价比模型' },
  { id: 'qwen', name: '通义千问-Max', provider: '阿里云', description: '中文场景优化' },
  { id: 'local', name: '本地模型', provider: '自部署', description: '数据不出域的私有部署' },
]

export const usageByModel: UsageByModel[] = [
  { modelName: 'GPT-4o', calls: 18100, share: 45 },
  { modelName: 'Claude 3.5 Sonnet', calls: 8300, share: 30 },
  { modelName: '本地模型', calls: 4200, share: 15 },
  { modelName: '通义千问-Max', calls: 1600, share: 6 },
  { modelName: 'DeepSeek-V3', calls: 800, share: 4 },
]

export const usageByKey: UsageByKey[] = [
  { keyName: '生产环境', calls: 12500, share: 38 },
  { keyName: '测试环境', calls: 8300, share: 25 },
  { keyName: 'AI 审标专用', calls: 5600, share: 17 },
]
