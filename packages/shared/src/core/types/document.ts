/** 文档解析块（契约对齐 AnGIneer MinerU blocks bbox 数据） */
export interface DocBlock {
  id: string
  page: number
  type: 'text' | 'table' | 'figure' | 'formula'
  bbox: { x: number, y: number, w: number, h: number }
  title?: string
}
