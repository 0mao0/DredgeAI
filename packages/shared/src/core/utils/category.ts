import type { AppCategory, AppMainStatus, SubAppStatus } from '@shared/core/types/application'

const CATEGORY_COLORS: Record<string, string> = {
  general: 'var(--color-info)',
  operation: 'var(--color-success)',
  design: 'var(--color-accent)',
  construction: 'var(--color-warning)',
}

const CATEGORY_COLOR_FALLBACK = 'var(--color-text-tertiary)'

export function getCategoryColor(category: string): string {
  return CATEGORY_COLORS[category] ?? CATEGORY_COLOR_FALLBACK
}

export function getCategoryAlphaBg(category: string): string {
  return `color-mix(in srgb, ${getCategoryColor(category)} 13%, transparent)`
}

export const APP_CATEGORY_LABELS: Record<AppCategory, string> = {
  general: '通用',
  operation: '经营',
  design: '设计',
  construction: '施工',
}

export const APP_MAIN_STATUS_LABELS: Record<AppMainStatus, string> = {
  online: '运营中',
  offline: '已下架',
}

export const SUB_APP_STATUS_LABELS: Record<SubAppStatus, string> = {
  published: '已发布',
  unpublished: '已下架',
}
