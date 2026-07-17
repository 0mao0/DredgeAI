import { defineConfig, presetUno, presetAttributify, presetIcons } from 'unocss'

export default defineConfig({
  presets: [
    presetUno(),
    presetAttributify(),
    presetIcons({ scale: 1.2, warn: true }),
  ],
  theme: {
    colors: {
      brand: { DEFAULT: '#0EA5E9', hover: '#0284C7' },
      accent: '#06B6D4',
      success: '#10B981',
      warning: '#F59E0B',
      danger: '#EF4444',
      info: '#3B82F6',
      sidebar: { DEFAULT: '#0F172A', 2: '#1E293B' },
      content: '#F8FAFC',
      card: '#FFFFFF',
      text: { primary: '#0F172A', secondary: '#475569', tertiary: '#94A3B8' },
      border: '#E2E8F0',
      divider: '#F1F5F9',
    },
    boxShadow: {
      sm: '0 1px 2px rgb(0 0 0 / 0.05)',
      md: '0 4px 12px rgb(15 23 42 / 0.08)',
      lg: '0 12px 32px rgb(15 23 42 / 0.12)',
      brand: '0 8px 24px rgb(14 165 233 / 0.25)',
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
