import request from '@/api/request'
import type { FileItem } from '@/types'

export function getRecentFiles(): Promise<FileItem[]> {
  return request.get<FileItem[]>('/file/recent')
}
