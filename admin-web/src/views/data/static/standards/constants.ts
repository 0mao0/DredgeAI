/** 标准规范模块共享选项与工具，供页面与上传组件复用 */
export const industryOptions = ['水利', '建筑', '交通', '环保', '能源', '综合']
export const natureOptions = ['强制', '推荐', '指导']
export const levelOptions = ['国家标准', '行业标准', '地方标准', '团体标准', '企业标准', '国际标准', '法律法规']
export const statusOptions = ['现行', '作废', '即将实施']

export const currentYear = new Date().getFullYear()
export const yearOptions = Array.from({ length: currentYear - 1989 }, (_, i) => currentYear - i)

export const industrySelectOptions = industryOptions.map((value) => ({ value, label: value }))
export const natureSelectOptions = natureOptions.map((value) => ({ value, label: value }))
export const levelSelectOptions = levelOptions.map((value) => ({ value, label: value }))
export const statusSelectOptions = statusOptions.map((value) => ({ value, label: value }))
export const yearSelectOptions = yearOptions.map((value) => ({ value, label: String(value) }))

export function statusColor(status?: string): string {
  if (status === '现行') return 'green'
  if (status === '作废') return 'red'
  if (status === '即将实施') return 'blue'
  return 'default'
}
