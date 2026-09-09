export interface AdminUserInfo {
  username: string
  name: string
  email: string
  phone?: string
  departments: string[]
  roles: string[]
  createdAt?: string
  lastLogin?: string
  concurrencyStamp?: string
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
