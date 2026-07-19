// ============================================================
// 跨端共享类型定义（admin-web / user-web 共用）
// 所有 mock 数据与 API 模块均引用此处的类型，作为单一数据源
// ============================================================

/** 分页参数 */
export interface Pagination {
  page: number
  pageSize: number
  total: number
}

// ---------- 通用图表类型 ----------

export interface ChartSeries {
  name: string
  data: number[]
}

export interface LineChartData {
  categories: string[]
  series: ChartSeries[]
}

export interface PieChartData {
  name: string
  data: { name: string; value: number }[]
}

export interface ApiUsageStats {
  totalTokens: number
  totalCalls: number
}

export interface UsageByModel {
  modelName: string
  calls: number
  share: number
}

export interface UsageByKey {
  keyName: string
  calls: number
  share: number
}

export interface UsageTimeSeries {
  categories: string[]
  byModel: { modelName: string; data: number[] }[]
  byKey: { keyName: string; data: number[] }[]
  byName: { name: string; data: number[] }[]
}

export interface ModelType {
  id: string
  name: string
  provider: string
  description?: string
}

// ---------- API Key（统一结构，admin 侧字段更全） ----------

export interface ApiKey {
  id: string
  name: string
  key: string
  fullKey: string
  modelType: string
  /** 该 Key 归属的应用（admin 侧用于管理“哪些人可以用哪些 API”） */
  app?: string
  status: '启用' | '禁用'
  createdAt: string
  expiredAt?: string
  lastUsed?: string
  quota: number
  usage: number
  docUrl: string
}

// ============================================================
//  admin-web 专用类型
// ============================================================

export interface AdminStats {
  totalUsers: number
  totalApps: number
  totalApiCalls: number
  activeUsers: number
  userTrend: number
  appTrend: number
  apiTrend: number
  activeUserTrend: number
}

export interface DashboardMetric {
  id: string
  title: string
  value: string | number
  suffix?: string
  trend?: number
  trendUp?: boolean
  icon?: string
  color?: string
}

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

export interface PermissionItem {
  id: string
  name: string
  code: string
  type: 'menu' | 'button' | 'api' | 'data'
  parentId?: string
  description?: string
  status: '启用' | '禁用'
  sort: number
}

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

export interface SystemLog {
  id: string
  type: '操作日志' | '登录日志' | '系统错误' | '安全告警'
  operator: string
  content: string
  ip?: string
  createdAt: string
  level?: 'info' | 'warning' | 'error'
}

export interface DataSource {
  id: string
  name: string
  type: 'mysql' | 'postgresql' | 'api'
  status: '已连接' | '连接失败' | '未配置'
  lastSync?: string
  description?: string
}

// ============================================================
//  user-web 专用类型
// ============================================================

export interface UserUserInfo {
  id: string
  name: string
  department: string
  position: string
  email: string
  phone: string
  avatar?: string
  authorizedScopes: string[]
  preferences: { theme: 'light' | 'dark'; language: 'zh-CN' | 'en-US' }
}

export interface MetricCard {
  id: string
  title: string
  value: string | number
  trend?: string
  trendUp?: boolean
  sparkline?: number[]
}

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

export interface TaskItem {
  id: string
  title: string
  status: '进行中' | '已完成' | '已暂停' | '已失败'
  updatedAt: string
  app?: string
  progress?: number
}

export interface FileItem {
  id: string
  name: string
  type: 'pdf' | 'docx' | 'xlsx' | 'pptx' | 'image' | 'other'
  size: string
  updatedAt: string
  url?: string
}

export interface BidReviewStep {
  title: string
  description: string
  status: 'wait' | 'process' | 'finish' | 'error'
}

export interface RiskItem {
  id: string
  level: '高风险' | '中风险' | '低风险'
  content: string
  source: string
  suggestion?: string
}

export interface BidReviewSession {
  id: string
  document: string
  date: string
  riskCount: number
  status: '已完成' | '进行中'
  snippets?: { role: 'user' | 'assistant'; content: string }[]
}

export interface StandardResult {
  id: string
  code: string
  title: string
  match: string
  excerpt: string
  source?: string
}

export interface StandardSearchHistory {
  id: string
  query: string
  date: string
  resultCount: number
}

export interface StandardCategory {
  id: string
  name: string
  count: number
  children?: StandardCategory[]
}
