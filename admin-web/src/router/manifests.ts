import type { AppManifest } from '@shared/core/types/application'
import type { MenuGroupMeta } from '@shared/web/router/manifest'

/** 无 manifest 定义者的菜单分组元数据（dev/users 仅在 parentKeys 中被引用） */
export const adminMenuGroups: Record<string, MenuGroupMeta> = {
  dev: { title: '开发管理', icon: 'CodeOutlined' },
  users: { title: '用户权限', icon: 'TeamOutlined' },
}

/**
 * admin-web 应用清单：声明所有管理端路由/组件/权限元数据。
 * 路由表由 manifest 数组动态生成，与 user-web 保持一致模式。
 */
export const adminAppManifests: AppManifest[] = [
  // ─── 仪表盘 ───────────────────────────────────────────
  {
    id: 'dashboard',
    route: '/dashboard',
    name: 'Dashboard',
    title: '仪表盘',
    icon: 'DashboardOutlined',
    component: () => import('@/views/dashboard/index.vue'),
    defaultVisible: true,
  },
  // ─── API 管理（底栏） ──────────────────────────────────
  {
    id: 'api',
    route: '/api',
    name: 'ApiManage',
    title: 'API 管理',
    icon: 'ApiOutlined',
    component: () => import('@/views/api/index.vue'),
    defaultVisible: true,
    menuPlacement: 'bottom',
  },
  // ─── 应用管理（分组） ──────────────────────────────────
  {
    id: 'applications',
    route: '/applications',
    name: 'Applications',
    title: '应用管理',
    icon: 'AppstoreOutlined',
    parentKeys: ['apps'],
    redirect: '/applications/analysis',
    children: [
      {
        id: 'app-analysis',
        route: '/applications/analysis',
        name: 'AppAnalysis',
        title: '数据分析',
        icon: 'BarChartOutlined',
        component: () => import('@/views/applications/analysis.vue'),
        parentKeys: ['apps'],
      },
      {
        id: 'app-control',
        route: '/applications/control',
        name: 'AppControl',
        title: '发布管理',
        icon: 'SendOutlined',
        component: () => import('@/views/applications/control.vue'),
        parentKeys: ['apps'],
      },
      {
        id: 'app-detail',
        route: '/applications/:id',
        name: 'AppDetail',
        title: '应用详情',
        component: () => import('@/views/applications/detail.vue'),
        parentKeys: ['apps'],
        menuPlacement: 'hidden',
      },
    ],
  },
  // ─── AI 配音（独立页） ─────────────────────────────────
  // 菜单由动态应用列表自动渲染（带分类 tag），route 保持独立注册
  {
    id: 'dubbing',
    route: '/applications/dubbing',
    name: 'AiDubbing',
    title: 'AI 配音',
    icon: 'CustomerServiceOutlined',
    component: () => import('@/views/dubbing/index.vue'),
    defaultVisible: true,
    parentKeys: ['apps'],
    menuPlacement: 'hidden',
  },
  // ─── 数据管理（分组） ──────────────────────────────────
  {
    id: 'data',
    route: '/data',
    name: 'Data',
    title: '数据管理',
    icon: 'DatabaseOutlined',
    parentKeys: ['data'],
    redirect: '/data/statistics',
    children: [
      {
        id: 'data-statistics',
        route: '/data/statistics',
        name: 'DataStatistics',
        title: '数据统计',
        icon: 'FundOutlined',
        component: () => import('@/views/data/statistics.vue'),
        parentKeys: ['data'],
      },
      // 动态数据分组
      {
        id: 'data-dynamic',
        route: '/data/dynamic',
        name: 'DataDynamic',
        title: '动态数据',
        icon: 'DotChartOutlined',
        parentKeys: ['data', 'data-dynamic'],
        redirect: '/data/dynamic/monitoring',
        children: [
          {
            id: 'monitoring',
            route: '/data/dynamic/monitoring',
            name: 'Monitoring',
            title: '监控',
            icon: 'MonitorOutlined',
            component: () => import('@/views/data/dynamic/monitoring.vue'),
            parentKeys: ['data', 'data-dynamic'],
          },
          {
            id: 'tide-level',
            route: '/data/dynamic/tide-level',
            name: 'TideLevel',
            title: '潮位',
            icon: 'FieldNumberOutlined',
            component: () => import('@/views/data/dynamic/tide-level.vue'),
            parentKeys: ['data', 'data-dynamic'],
          },
        ],
      },
      // 静态数据分组
      {
        id: 'data-static',
        route: '/data/static',
        name: 'DataStatic',
        title: '静态数据',
        icon: 'FolderOutlined',
        parentKeys: ['data', 'data-static'],
        redirect: '/data/static/enterprise',
        children: [
          {
            id: 'enterprise',
            route: '/data/static/enterprise',
            name: 'Enterprise',
            title: '企业库',
            icon: 'BankOutlined',
            component: () => import('@/views/data/static/enterprise.vue'),
            parentKeys: ['data', 'data-static'],
          },
          {
            id: 'standards',
            route: '/data/static/standards',
            name: 'Standards',
            title: '标准库',
            icon: 'AuditOutlined',
            component: () => import('@/views/data/static/standards.vue'),
            parentKeys: ['data', 'data-static'],
          },
          {
            id: 'reports',
            route: '/data/static/reports',
            name: 'Reports',
            title: '报告库',
            icon: 'SolutionOutlined',
            component: () => import('@/views/data/static/reports.vue'),
            parentKeys: ['data', 'data-static'],
          },
          {
            id: 'experience',
            route: '/data/static/experience',
            name: 'Experience',
            title: '经验库',
            icon: 'ReadOutlined',
            component: () => import('@/views/data/static/experience.vue'),
            parentKeys: ['data', 'data-static'],
          },
        ],
      },
    ],
  },
  // ─── 权限管理 ─────────────────────────────────────────
  {
    id: 'permissions',
    route: '/permissions',
    name: 'Permissions',
    title: '权限管理',
    icon: 'SafetyOutlined',
    component: () => import('@/views/permissions/index.vue'),
    defaultVisible: true,
    parentKeys: ['users'],
  },
  // ─── 组织用户 ─────────────────────────────────────────
  {
    id: 'org-users',
    route: '/org-users',
    name: 'OrgUsers',
    title: '组织用户',
    icon: 'TeamOutlined',
    component: () => import('@/views/org-users/index.vue'),
    defaultVisible: true,
    parentKeys: ['users'],
  },
  // ─── 预警管理 ─────────────────────────────────────────
  {
    id: 'alerts',
    route: '/alerts',
    name: 'Alerts',
    title: '预警管理',
    icon: 'AlertOutlined',
    component: () => import('@/views/alerts/index.vue'),
    defaultVisible: true,
  },
  // ─── 个人中心 ─────────────────────────────────────────
  {
    id: 'profile',
    route: '/profile',
    name: 'Profile',
    title: '个人中心',
    icon: 'UserOutlined',
    component: () => import('@/views/profile/index.vue'),
    menuPlacement: 'bottom',
  },
  // ─── 开发者工具（分组） ────────────────────────────────
  {
    id: 'menu-config',
    route: '/menu-config',
    name: 'MenuConfig',
    title: '菜单配置',
    icon: 'MenuOutlined',
    component: () => import('@/views/dev/menu-config.vue'),
    parentKeys: ['dev'],
    requiredPermission: 'dev',
  },
  {
    id: 'task-scheduler',
    route: '/task-scheduler',
    name: 'TaskScheduler',
    title: '任务调度',
    icon: 'ScheduleOutlined',
    component: () => import('@/views/dev/task-scheduler.vue'),
    parentKeys: ['dev'],
    requiredPermission: 'dev',
  },
  {
    id: 'logs',
    route: '/logs',
    name: 'Logs',
    title: '日志管理',
    icon: 'FileTextOutlined',
    component: () => import('@/views/dev/logs.vue'),
    parentKeys: ['dev'],
    requiredPermission: 'dev',
  },
  {
    id: 'platform',
    route: '/platform',
    name: 'Platform',
    title: '平台信息',
    icon: 'InfoCircleOutlined',
    component: () => import('@/views/dev/platform.vue'),
    parentKeys: ['dev'],
    requiredPermission: 'dev',
  },
]
