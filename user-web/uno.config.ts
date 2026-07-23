import { defineConfig, presetUno, presetAttributify, presetIcons } from 'unocss'

export default defineConfig({
  presets: [
    presetUno(),
    presetAttributify(),
    presetIcons({ scale: 1.2, warn: true }),
  ],
  theme: {
    colors: {
      brand: { DEFAULT: 'var(--color-brand)', hover: 'var(--color-brand-hover)' },
      accent: 'var(--color-accent)',
      success: 'var(--color-success)',
      warning: 'var(--color-warning)',
      danger: 'var(--color-danger)',
      info: 'var(--color-info)',
      content: 'var(--color-content-bg)',
      card: 'var(--color-card-bg)',
      text: { primary: 'var(--color-text-primary)', secondary: 'var(--color-text-secondary)', tertiary: 'var(--color-text-tertiary)' },
      border: 'var(--color-border)',
      divider: 'var(--color-divider)',
    },
    boxShadow: {
      sm: 'var(--shadow-sm)',
      md: 'var(--shadow-md)',
      lg: 'var(--shadow-lg)',
      brand: 'var(--shadow-brand)',
    },
    borderRadius: {
      sm: '6px',
      base: '8px',
      lg: '12px',
      xl: '16px',
    },
  },
  shortcuts: {
    'card-hover': 'transition-all duration-200 hover:-translate-y-0.5 hover:shadow-md',
    'flex-center': 'flex items-center justify-center',
    'flex-between': 'flex items-center justify-between',
  },
})
