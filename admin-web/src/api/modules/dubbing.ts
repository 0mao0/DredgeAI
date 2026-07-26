import request from '@/api/request'
import { urls } from '@shared/core/api'
import type { DubbingTask, DubbingUsageSummary, DubbingUsageTimeSeries, VoiceItem } from '@/types'
import type { PagedResult } from '@shared/types'

function buildUrl(tpl: string, id: string): string {
  return tpl.replace(':id', id)
}

export function getAdminDubbingTasks(params?: Record<string, string | number>): Promise<PagedResult<DubbingTask>> {
  return request.get<PagedResult<DubbingTask>>(urls.adminDubbingTasks, { params })
}

export function deleteAdminDubbingTask(id: string): Promise<void> {
  return request.delete(buildUrl(urls.adminDubbingTask, id))
}

export function getAdminDubbingUsageSummary(): Promise<DubbingUsageSummary> {
  return request.get<DubbingUsageSummary>(urls.adminDubbingUsageSummary)
}

export function getAdminDubbingUsageTimeseries(range: string): Promise<DubbingUsageTimeSeries> {
  return request.get<DubbingUsageTimeSeries>(urls.adminDubbingUsageTimeseries, { params: { range } })
}

export function getAdminVoices(params?: Record<string, string | number>): Promise<VoiceItem[]> {
  return request.get(urls.adminVoices, { params }).then(
    (r: unknown) => (Array.isArray(r) ? r : (r as Record<string, unknown>)?.data || r) as VoiceItem[],
  )
}

export function createAdminVoice(data: FormData | { name: string; gender: string }): Promise<void> {
  return request.post(urls.adminVoices, data)
}

export function deleteAdminVoice(id: string): Promise<void> {
  return request.delete(`${urls.adminVoices}/${id}`)
}
