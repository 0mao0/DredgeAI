/** 项目简称：超过 max 个字符时截断（下拉展示用），完整名称用 title 提示。 */
export function shortProjectName(name: string, max = 6): string {
  const trimmed = name.trim()
  if (!trimmed) return '未命名'
  return trimmed.length > max ? trimmed.slice(0, max) : trimmed
}
