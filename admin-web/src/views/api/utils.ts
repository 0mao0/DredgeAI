import type { MergedUserRecord } from './types'

export function formatConsumption(n: number): string {
  if (n >= 1e12) return `${(n / 1e12).toFixed(1)} 兆`
  if (n >= 1e8) return `${(n / 1e8).toFixed(1)} 亿`
  if (n >= 1e7) return `${(n / 1e7).toFixed(1)} 千万`
  if (n >= 1e4) return `${(n / 1e4).toFixed(1)} 万`
  return n.toLocaleString()
}

export function formatLimit(record: MergedUserRecord, field: 'calls' | 'tokens'): string {
  const total = record.modelLimits
    .filter((m) => m.enabled)
    .reduce((s, m) => s + (field === 'calls' ? m.callsLimit : m.tokensLimit), 0)
  if (total === 0) return '-'
  return `${formatConsumption(total).replace(/\s/g, '')}/周`
}
