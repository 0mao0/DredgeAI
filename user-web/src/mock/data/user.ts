import type { UserInfo } from '@/types'

export const currentUser: UserInfo = {
  id: 'u-001',
  name: '张明',
  department: '工程技术部',
  position: '高级工程师',
  email: 'zhangming@dredgeai.com',
  phone: '138****6688',
  authorizedScopes: ['智能审批助手', '知识检索系统', '标书智能分析', '数据洞察助手', '合规检查工具', '开发接口网关', '图纸合规检查', '安全风险监控', '成本动态分析', '知识问答'],
  preferences: { theme: 'light', language: 'zh-CN' },
}
