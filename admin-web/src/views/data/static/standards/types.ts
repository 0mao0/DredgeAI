export interface StandardParseBatchItem {
  id: string
  name: string
  status: 'parsing' | 'success' | 'failed'
  error?: string
}
