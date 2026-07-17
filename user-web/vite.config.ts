import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import UnoCSS from 'unocss/vite'
import { resolve } from 'path'

export default defineConfig({
  plugins: [vue(), UnoCSS()],
  resolve: {
    alias: { '@': resolve(__dirname, 'src') },
  },
  server: { port: 5373, host: true, open: false },
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
