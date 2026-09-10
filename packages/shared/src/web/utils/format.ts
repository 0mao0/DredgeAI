import dayjs from 'dayjs'
import utcPlugin from 'dayjs/plugin/utc'
import timezonePlugin from 'dayjs/plugin/timezone'
import type { ApplicationConfiguration } from '../../core/types/appConfig'

dayjs.extend(utcPlugin)
dayjs.extend(timezonePlugin)

/** 显示时区缺省值（应用配置缺失/未加载时回退） */
export const DEFAULT_TIME_ZONE = 'Asia/Shanghai'

/** 从 ABP 应用配置提取 IANA 时区名（timing.timeZone，ABP 时钟为 UTC） */
export function resolveAppConfigTimeZone(config: ApplicationConfiguration | null | undefined): string {
  return config?.timing?.timeZone?.iana?.timeZoneName ?? DEFAULT_TIME_ZONE
}

/**
 * 后端时间（UTC ISO 串，可能无 Z 后缀，按 UTC 解释）按指定时区格式化为 YYYY-MM-DD HH:mm:ss。
 * 非法输入回退原串。
 */
export function formatDateTime(iso: string, timeZone: string = DEFAULT_TIME_ZONE): string {
  const d = dayjs.utc(/(?:z|[+-]\d{2}:?\d{2})$/i.test(iso) ? iso : `${iso}Z`)
  return d.isValid() ? d.tz(timeZone).format('YYYY-MM-DD HH:mm:ss') : iso
}

/** 文件大小格式化（MB/KB/B）。 */
export function formatFileSize(size: number): string {
  if (size >= 1024 * 1024) return `${(size / 1024 / 1024).toFixed(1)} MB`
  if (size >= 1024) return `${(size / 1024).toFixed(0)} KB`
  return `${size} B`
}
