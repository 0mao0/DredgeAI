export function formatNumber(n: number): string {
  if (n >= 1e8) return `${(n / 1e8).toFixed(2)} 亿`
  if (n >= 1e7) return `${(n / 1e7).toFixed(1)} 千万`
  if (n >= 1e4) return `${(n / 1e4).toFixed(1)} 万`
  return n.toLocaleString()
}

export function formatPercent(n: number): string {
  return `${(n * 100).toFixed(1)}%`
}
