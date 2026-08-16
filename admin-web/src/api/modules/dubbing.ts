import request from '@/api/request'
import { urls, fillUrl } from '@shared/core/api'
import type { DubbingTask, DubbingUsageSummary, DubbingUsageTimeSeries, VoiceItem } from '@/types'
import type { PagedResult } from '@shared/types'

export function getAdminDubbingTasks(params?: Record<string, string | number>): Promise<PagedResult<DubbingTask>> {
  return request.get<PagedResult<DubbingTask>>(urls.adminDubbingTasks, { params })
}

export function deleteAdminDubbingTask(id: string): Promise<void> {
  return request.delete(fillUrl(urls.adminDubbingTask, { id }))
}

export function getAdminDubbingUsageSummary(): Promise<DubbingUsageSummary> {
  return request.get<DubbingUsageSummary>(urls.adminDubbingUsageSummary)
}

export function getAdminDubbingUsageTimeseries(range: string): Promise<DubbingUsageTimeSeries> {
  return request.get<DubbingUsageTimeSeries>(urls.adminDubbingUsageTimeseries, { params: { range } })
}

export function getAdminVoices(params?: Record<string, string | number>): Promise<VoiceItem[]> {
  // 后端响应形状未固定（数组或 { data } 包裹），统一收窄为数组
  return request.get<VoiceItem[] | { data?: VoiceItem[] }>(urls.adminVoices, { params }).then(
    (r) => (Array.isArray(r) ? r : r?.data || []),
  )
}

export function createAdminVoice(data: FormData | { name: string, gender: string }): Promise<void> {
  return request.post(urls.adminVoices, data)
}

export function deleteAdminVoice(id: string): Promise<void> {
  return request.delete(`${urls.adminVoices}/${id}`)
}
