/**
 * 子应用：由 admin 模块按采集/业务分类发布后，面向 user-web 的具体可订阅单元。
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
  /** 前端路由 slug，如 '/applications/dubbing'；缺省时回退到 /applications/:id */
  route?: string
  /** 按分类发布出的子应用；为空时模块直接作为用户端应用 */
  subApps?: SubApp[]
  /** 授权范围：所有用户可见，或按角色指定部分人 */
  scope?: '所有' | '部分'
}

/** user-web 侧应用卡片（由 ApplicationItem/SubApp 推导） */
export interface AppCard {
  id: string
  /** 子应用所属主应用 id（用于映射 admin 全局默认顺序）；主应用缺省时用 id */
  parentAppId?: string
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
 * 应用清单：每个应用/页面对应一份 manifest，描述路由/组件/权限等元数据。
 * 支持嵌套 children 用于分组路由（如 apps/data 分组），
 * 分组节点只需 path/name/title 和 children，无需 component。
 */
export interface AppManifest {
  /** 唯一 id */
  id: string
  /** 路由路径（必须以 / 开头；分组节点可无 component） */
  route: string
  /** 路由 name */
  name: string
  /** 菜单标题 */
  title: string
  /** antd 图标名（与 SubApp.icon 同一命名空间） */
  icon?: string
  /** 视图组件的动态 import 函数（分组节点不设） */
  component?: () => Promise<unknown>
  /** 默认是否在侧边栏可见 */
  defaultVisible?: boolean
  /** 所需权限码（可选，路由守卫消费） */
  requiredPermission?: string
  /** 分类标签（用于侧边栏分组） */
  category?: '通用' | '经营' | '设计' | '施工'
  /** 可选：用于侧边栏菜单展开的父级 key 列表 */
  parentKeys?: string[]
  /** 菜单归属：main=主菜单（默认）/ bottom=底部菜单 / hidden=不出现在菜单（如参数化详情页） */
  menuPlacement?: 'main' | 'bottom' | 'hidden'
  /** 子路由（支持嵌套分组） */
  children?: AppManifest[]
  /** 是否重定向（redirect 路径） */
  redirect?: string
}
