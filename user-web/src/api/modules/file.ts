import request from '@/api/request'
import type { FileItem } from '@/types'

export function getRecentFiles(): Promise<FileItem[]> {
  return request.get('/file/recent') as unknown as Promise<FileItem[]>
}
