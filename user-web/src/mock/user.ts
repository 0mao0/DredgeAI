import type { UserInfo } from '@/types'

export const currentUser: UserInfo = {
  id: 'u-001',
  name: '张明',
  department: '工程技术部',
  position: '高级工程师',
  email: 'zhangming@dredgeai.com',
  phone: '138****6688',
  authorizedScopes: ['AI 审标', '标准查询', '智能写作', '合同审查', '数据看板', '知识问答'],
  preferences: { theme: 'light', language: 'zh-CN' },
}
