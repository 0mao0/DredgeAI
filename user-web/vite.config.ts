import { defineConfig, loadEnv } from 'vite'
import vue from '@vitejs/plugin-vue'
import UnoCSS from 'unocss/vite'
import { fileURLToPath, URL } from 'node:url'

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')
  return {
    plugins: [vue(), UnoCSS()],
    resolve: {
      alias: {
        '@': fileURLToPath(new URL('src', import.meta.url)),
        '@shared': fileURLToPath(new URL('../packages/shared/src', import.meta.url)),
      },
      dedupe: ['vue', 'ant-design-vue', '@ant-design/icons-vue', 'vue-echarts'],
    },
    server: {
      port: 5373,
      host: true,
      open: false,
      proxy: {
        // ABP 后端（比标等真实接口，开发联调用；mock 关闭后生效）
        '/api': {
          target: env.VITE_API_TARGET || 'https://localhost:44361',
          changeOrigin: true,
          secure: false,
        },
      // 本地 CosyVoice TTS 服务（开发联调用，生产由 VITE_TTS_TARGET 指向正式服务）
        '/tts': {
          target: env.VITE_TTS_TARGET || 'http://localhost:8000',
          changeOrigin: true,
          rewrite: (p) => p.replace(/^\/tts/, '/api'),
        },
      },
    },
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
  }
})
