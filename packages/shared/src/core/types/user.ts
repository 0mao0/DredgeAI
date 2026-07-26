export interface AdminUserInfo {
  id: string
  username: string
  name: string
  email: string
  phone: string
  role: 'super_admin' | 'admin' | 'operator'
  department: string
  avatar?: string
  status: '启用' | '禁用'
  createdAt: string
  lastLogin?: string
}

export interface UserUserInfo {
  id: string
  name: string
  department: string
  position: string
  email: string
  phone: string
  avatar?: string
  authorizedScopes: string[]
  preferences: { theme: 'light' | 'dark', language: 'zh-CN' | 'en-US' }
}
