import type { AttendanceItemDto } from '@/types'

/** 从 18 位身份证号取出生日期（YYYYMMDD），例如 320482198704085913 → 19870408；非身份证号返回空。 */
export function birthdayFromIdCard(employeeNo?: string): string {
  if (!employeeNo) return ''
  const digits = employeeNo.trim()
  if (!/^\d{17}[\dX]$/i.test(digits)) return ''
  return digits.slice(6, 14)
}

/** 身份证生日后四位（MMDD），例如 320482198704085913 → 0408；非身份证号返回空。 */
export function birthdaySuffix(employeeNo?: string): string {
  const birthday = birthdayFromIdCard(employeeNo)
  if (!birthday) return ''
  return birthday.slice(4)
}

/**
 * 列表内出现同名不同人（不同 workerId）时，显示“姓名-生日后四位”以区分，
 * 例如 王飞-0408；没有同名冲突时只显示姓名。
 */
export function displayAttendanceName(item: AttendanceItemDto, list: AttendanceItemDto[]): string {
  const hasSameNamePeer = list.some(
    (other) => other.name === item.name && other.workerId !== item.workerId,
  )
  if (!hasSameNamePeer) return item.name
  const suffix = birthdaySuffix(item.employeeNo)
  return suffix ? `${item.name}-${suffix}` : item.name
}
