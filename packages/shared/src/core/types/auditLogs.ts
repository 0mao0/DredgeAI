/** 审计日志类型（镜像后端 AuditLogController DTO，camelCase） */

export interface OperatorInfo {
  userId: string | null
  userName: string | null
  displayName: string | null
  roleNames: string[]
  organizationUnits: string[]
}

export interface AuditLogListItem {
  id: string
  applicationName: string | null
  operator: OperatorInfo
  /** UTC ISO 8601 */
  executionTime: string
  /** 毫秒 */
  executionDuration: number
  clientIpAddress: string | null
  clientName: string | null
  clientId: string | null
  correlationId: string | null
  browserInfo: string | null
  httpMethod: string | null
  url: string | null
  httpStatusCode: number | null
  exceptions: string | null
  comments: string | null
}

export interface AuditLogAction {
  id: string
  serviceName: string | null
  methodName: string | null
  /** JSON 字符串 */
  parameters: string | null
  executionTime: string
  executionDuration: number
}

/** ABP EntityChangeType: 0=Created 1=Updated 2=Deleted */
export type EntityChangeType = 0 | 1 | 2

export interface EntityPropertyChange {
  id: string
  propertyName: string | null
  originalValue: string | null
  newValue: string | null
  propertyTypeFullName: string | null
}

export interface EntityChange {
  id: string
  auditLogId: string
  changeTime: string
  changeType: EntityChangeType
  entityTenantId: string | null
  entityId: string | null
  entityTypeFullName: string | null
  propertyChanges: EntityPropertyChange[]
}

export interface AuditLogDetail extends AuditLogListItem {
  entityChanges: EntityChange[]
  actions: AuditLogAction[]
}

/** 对应后端 GetAuditLogListInput（camelCase 发送，ASP.NET 绑定大小写不敏感） */
export interface AuditLogListParams {
  skipCount: number
  maxResultCount: number
  sorting?: string
  /** ISO 8601 */
  startTime?: string
  endTime?: string
  userName?: string
  httpMethod?: string
  url?: string
  httpStatusCode?: number
  hasException?: boolean
}
