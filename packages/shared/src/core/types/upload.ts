/** 上传文件条目（共享 UploadFileRow 使用，比标/读标/后续模块复用）。 */
export interface UploadFileItem {
  key: string
  name: string
  size: number
  file: File
  role: 'bid' | 'tender'
  status: 'pending' | 'uploading' | 'done' | 'error'
  error?: string
  warning?: string
  docId?: string
  percent?: number
  startedAt?: number
}
