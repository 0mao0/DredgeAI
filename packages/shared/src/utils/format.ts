import dayjs from 'dayjs'
import relativeTime from 'dayjs/plugin/relativeTime'
import 'dayjs/locale/zh-cn'

dayjs.extend(relativeTime)
dayjs.locale('zh-cn')

export function formatDate(date: string | Date, fmt = 'YYYY-MM-DD HH:mm'): string {
  return dayjs(date).format(fmt)
}

export function fromNow(date: string | Date): string {
  return dayjs(date).fromNow()
}

export function formatNumber(n: number): string {
  if (n >= 1e8) return `${(n / 1e8).toFixed(2)} 亿`
  if (n >= 1e4) return `${(n / 1e4).toFixed(1)} 万`
  return n.toLocaleString()
}

export function formatPercent(n: number): string {
  return `${(n * 100).toFixed(1)}%`
}
