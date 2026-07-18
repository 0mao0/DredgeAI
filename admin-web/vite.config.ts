import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import UnoCSS from 'unocss/vite'
import { fileURLToPath, URL } from 'url'

export default defineConfig({
  plugins: [vue(), UnoCSS()],
  resolve: {
    alias: { '@': fileURLToPath(new URL('src', import.meta.url)) },
  },
  server: { port: 5374, host: true, open: false },
  css: {
    preprocessorOptions: {
      less: {
        javascriptEnabled: true,
        modifyVars: {
          'primary-color': '#0EA5E9',
          'link-color': '#0EA5E9',
          'border-radius-base': '8px',
          'font-size-base': '14px',
        },
      },
    },
  },
})
