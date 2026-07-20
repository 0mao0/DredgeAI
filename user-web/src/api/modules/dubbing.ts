import request from '@/api/request'
import { urls } from '@shared/core/api'
import type { VoiceItem, DubbingTask, PagedResult } from '@/types'

function buildUrl(urlTemplate: string, id: string): string {
  return urlTemplate.replace(':id', id)
}

export function getVoices(): Promise<VoiceItem[]> {
  return request.get<VoiceItem[]>(urls.dubbingVoices)
}

export function generateDubbing(text: string, voiceId: string, speed: number): Promise<DubbingTask> {
  return request.post<DubbingTask>(urls.dubbingGenerate, { text, voiceId, speed })
}

export function getDubbingTasks(params: { skip?: number; max?: number }): Promise<PagedResult<DubbingTask>> {
  return request.get<PagedResult<DubbingTask>>(urls.dubbingTasks, { params })
}

export function getDubbingTask(id: string): Promise<DubbingTask> {
  return request.get<DubbingTask>(buildUrl(urls.dubbingTask, id))
}

export function deleteDubbingTask(id: string): Promise<void> {
  return request.delete<void>(buildUrl(urls.dubbingTask, id))
}

export function downloadDubbing(id: string): Promise<{ url: string }> {
  return request.get<{ url: string }>(buildUrl(urls.dubbingTaskDownload, id))
}
