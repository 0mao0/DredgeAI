import type { UserUserInfo } from '@shared/types'

export const currentUser: UserUserInfo = {
  id: 'u-001',
  name: '张明',
  department: '工程技术部',
  position: '高级工程师',
  email: 'zhangming@dredgeai.com',
  phone: '138****6688',
  authorizedScopes: ['标准查询', 'AI视频', 'AI配音', '设计经验', '施工经验', '施组审核', '耙吸效率', '疏浚情报', '科技情报', 'AI投标'],
  preferences: { theme: 'light', language: 'zh-CN' },
}
