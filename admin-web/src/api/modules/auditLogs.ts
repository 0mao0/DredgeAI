import request from '@/api/request'
import { urls } from '@shared/core/api'
import type { PagedResult, AuditLogDetail, AuditLogListItem, AuditLogListParams } from '@shared/core/types'

export function getAuditLogs(params: AuditLogListParams): Promise<PagedResult<AuditLogListItem>> {
  return request.get<PagedResult<AuditLogListItem>>(urls.auditLogs, { params })
}

export function getAuditLogDetail(id: string): Promise<AuditLogDetail> {
  return request.get<AuditLogDetail>(urls.auditLogDetail.replace(':id', id))
}
