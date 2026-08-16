const CATEGORY_COLORS: Record<string, string> = {
  通用: 'var(--color-info)',
  经营: 'var(--color-success)',
  设计: 'var(--color-accent)',
  施工: 'var(--color-warning)',
}

const CATEGORY_COLOR_FALLBACK = 'var(--color-text-tertiary)'

export function getCategoryColor(category: string): string {
  return CATEGORY_COLORS[category] ?? CATEGORY_COLOR_FALLBACK
}

export function getCategoryAlphaBg(category: string): string {
  return `color-mix(in srgb, ${getCategoryColor(category)} 13%, transparent)`
}
