/**
 * URL 契约：所有 API 资源路径在此声明，双端共用同一 key。
 * 路径不含 baseURL 前缀（前缀由各端的 createRequest 注入）。
 * 修复历史问题：原 user-web 用 /key、admin-web 用 /apikey，命名不统一。
 */
export const urls = {
  // user-web
  userCurrent: '/user/current',
  appList: '/app/list',
  taskRecent: '/task/recent',
  taskQuick: '/task/quick',
  fileRecent: '/file/recent',
  bidSteps: '/bid/steps',
  bidRisks: '/bid/risks',
  bidSessions: '/bid/sessions',
  bidDocument: '/bid/document',
  standardResult: '/standard/result',
  standardHistory: '/standard/history',
  standardCategories: '/standard/categories',
  standardRecommended: '/standard/recommended',
  standardList: '/standard/list',
  standardProperty: '/standard/property',
  standardPropertyList: '/standard/property/list',
  standardDocument: '/standard/document',
  standardAIAnalysis: '/standard/ai-analysis',
  chartEfficiencyTrend: '/chart/efficiency-trend',

  // 共享（双端均使用 /apikey 命名，统一规范）
  apiKeyList: '/apikey/list',
  apiKeyModels: '/apikey/models',
  apiKeyUsageByModel: '/apikey/usage-by-model',
  apiKeyUsageByKey: '/apikey/usage-by-key',
  apiKeyUsageStats: '/apikey/usage-stats',
  apiKeyUsageTimeSeries: '/apikey/usage-timeseries',

  // AI 配音（直连 CosyVoice TTS 服务，经 Vite /tts 代理转发到 localhost:8000）
  dubbingVoices: '/tts/voices',
  dubbingGenerate: '/tts/tts',
  dubbingRegister: '/tts/voices/upload',
  adminVoices: '/dubbing/admin/voices',
  dubbingTasks: '/dubbing/tasks',
  dubbingTask: '/dubbing/tasks/:id',
  dubbingTaskDownload: '/dubbing/tasks/:id/download',
  adminDubbingTasks: '/dubbing/admin/tasks',
  adminDubbingTask: '/dubbing/admin/tasks/:id',
  adminDubbingUsageSummary: '/dubbing/admin/usage/summary',
  adminDubbingUsageTimeseries: '/dubbing/admin/usage/timeseries',

  // AI 比标
  compareTasks: '/compare/tasks',
  compareTask: '/compare/tasks/:id',
  compareTaskDocuments: '/compare/tasks/:id/documents',
  compareTaskStartParse: '/compare/tasks/:id/documents/parse',
  compareTaskDocumentFile: '/compare/tasks/:id/documents/:docId/file',
  compareTaskReparse: '/compare/tasks/:id/documents/reparse',
  compareTaskCompareRetry: '/compare/tasks/:id/compare/retry',
  compareTaskName: '/compare/tasks/:id/name',
  compareTaskIr: '/compare/tasks/:id/ir/:docId',
  compareTaskEvidences: '/compare/tasks/:id/evidences',
  compareTaskMatrix: '/compare/tasks/:id/matrix',
  compareTaskClauses: '/compare/tasks/:id/clauses',
  compareTaskClauseExtract: '/compare/tasks/:id/clauses/extract',
  compareTaskReport: '/compare/tasks/:id/report',
  compareTaskExport: '/compare/tasks/:id/export',
  compareTaskExportStatus: '/compare/tasks/:id/exports/:exportId',
  compareClauseTemplates: '/compare/clause-templates',
  compareClauseTemplate: '/compare/clause-templates/:id',

  // admin-web
  adminStats: '/dashboard/stats',
  dashboardMetrics: '/dashboard/metrics',
  dashboardApiCallsTrend: '/dashboard/api-calls-trend',
  dashboardAppDistribution: '/dashboard/app-distribution',
  dashboardActiveUsersTrend: '/dashboard/active-users-trend',
  dashboardRecentLogs: '/dashboard/recent-logs',
  analyticsDailyApiCalls: '/analytics/daily-api-calls',
  analyticsModelUsage: '/analytics/model-usage',
  analyticsUserGrowth: '/analytics/user-growth',
  analyticsErrorRate: '/analytics/error-rate',
  applications: '/applications',
  permissions: '/permissions',
  orgUsers: '/org-users',
  orgUserStatus: '/org-users/:id/status',
  orgUserRoles: '/org-users/:id/roles',
  roles: '/roles',
  roleDetail: '/roles/:id',
  roleUsers: '/roles/:id/users',
  rolePermissions: '/roles/:id/permissions',
  datasources: '/datasources',
  adminProfile: '/profile',
} as const

export type UrlKey = keyof typeof urls
