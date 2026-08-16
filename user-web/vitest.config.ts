import { defineConfig } from 'vitest/config'
import { fileURLToPath, URL } from 'node:url'

export default defineConfig({
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('src', import.meta.url)),
      '@shared': fileURLToPath(new URL('../packages/shared/src', import.meta.url)),
    },
  },
  test: {
    include: ['__tests__/**/*.test.ts'],
  },
})
