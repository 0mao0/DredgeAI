export interface FileItem {
  id: string
  name: string
  type: 'pdf' | 'docx' | 'xlsx' | 'pptx' | 'image' | 'other'
  size: string
  updatedAt: string
  url?: string
}
