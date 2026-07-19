import request from '@/api/request'
import { urls } from '@shared/core/api'
import type { FileItem } from '@/types'

export function getRecentFiles(): Promise<FileItem[]> {
  return request.get<FileItem[]>(urls.fileRecent)
}
