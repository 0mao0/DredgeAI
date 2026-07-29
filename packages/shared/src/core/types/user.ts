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

/** 组织用户（4A 系统同步） */
export interface OrgUser {
  id: string
  name: string
  phone: string
  departments: string[]
  status: 'active' | 'disabled'
  roleIds: string[]
  createdAt: string
}

/** 角色 */
export interface Role {
  id: string
  name: string
  description: string
  menuKeys: string[]
  appIds: string[]
  userCount: number
  createdAt: string
}
