/** 子应用：由 admin 模块按采集/业务分类发布后，面向 user-web 的具体可订阅单元。
 * 仅当模块存在多个面向用户的形态时才定义（如「情报采集」发布为疏浚情报/科技情报）。
 * 普通模块无 subApps，则模块本身即直接作为 user 端应用。
 */
export interface SubApp {
  id: string
  name: string
  category: '通用' | '经营' | '设计' | '施工'
  parentAppId: string
  parentAppName: string
  route: string
  icon: string
  version: string
  status: '已发布' | '已下架'
  description?: string
  /** 授权范围：所有用户可见，或按角色指定部分人 */
  scope?: '所有' | '部分'
}

/** 应用目录（admin 模块，作为发布来源；user 端实际可见的是其发布的子应用或模块本身） */
export interface ApplicationItem {
  id: string
  name: string
  category: '通用' | '经营' | '设计' | '施工'
  manager: string
  version: string
  status: '运营中' | '已下架' | '开发中'
  userCount: number
  apiCalls: number
  createdAt: string
  /** antd 图标名，如 'BookOutlined' */
  icon: string
  /** 按分类发布出的子应用；为空时模块直接作为用户端应用 */
  subApps?: SubApp[]
  /** 授权范围：所有用户可见，或按角色指定部分人 */
  scope?: '所有' | '部分'
}

/** user-web 侧应用卡片（由 ApplicationItem/SubApp 推导） */
export interface AppCard {
  id: string
  title: string
  description: string
  category: '通用' | '设计' | '施工' | '经营'
  icon: string
  status: '已授权' | '待申请' | '已下架'
  route?: string
  version?: string
  pinned?: boolean
}

/**
 * 应用清单：每个 user-web 应用对应一份 manifest，描述路由/组件/权限等元数据。
 * 路由表不再硬编码，而是由 manifest 数组动态生成。
 */
export interface AppManifest {
  /** 唯一 id，与 ApplicationItem.id 或 SubApp.id 对应 */
  id: string
  /** 路由路径（必须以 / 开头） */
  route: string
  /** 路由 name */
  name: string
  /** 菜单标题 */
  title: string
  /** antd 图标名（与 SubApp.icon 同一命名空间） */
  icon: string
  /** 视图组件的动态 import 函数 */
  component: () => Promise<unknown>
  /** 默认是否在侧边栏可见（用户可在 profile 页勾选） */
  defaultVisible?: boolean
  /** 所需权限码（可选，路由守卫消费） */
  requiredPermission?: string
  /** 分类标签（用于侧边栏分组） */
  category?: '通用' | '经营' | '设计' | '施工'
}
