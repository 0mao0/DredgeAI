/** ABP 错误响应结构 */
export interface AbpErrorInfo {
  code: string | null
  message: string | null
  details: string | null
  data: Record<string, unknown> | null
  validationErrors: Array<{ message: string | null; members: string[] | null }> | null
}

export interface AbpErrorResponse {
  error: AbpErrorInfo
}

/** 分页查询响应 */
export interface PagedResult<T> {
  items: T[]
  totalCount: number
}
