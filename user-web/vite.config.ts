import { defineConfig, loadEnv } from 'vite'
import vue from '@vitejs/plugin-vue'
import UnoCSS from 'unocss/vite'
import { fileURLToPath, URL } from 'node:url'
// pdf.js 6.x wasm/字体资源复制插件：直接复用 angineer-docs-ui submodule 自带实现
// （该子路径未在包 exports 中暴露，submodule 落地后经相对路径引入，避免维护内联拷贝）
import pdfWasmPlugin from '../vendor/angineer-docs-ui/vite-pdf-wasm.mjs'

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')
  return {
    plugins: [vue(), UnoCSS(), pdfWasmPlugin()],
    optimizeDeps: {
      exclude: ['@angineer/docs-ui'],
      // docs-ui 的 CJS 依赖必须显式预构建，否则开发模式下会以原始 UMD 形式下发，
      // 导致 `import JSZip from 'jszip'` 报 "does not provide an export named 'default'"
      include: ['@angineer/docs-ui > docx-preview', '@angineer/docs-ui > xlsx'],
    },
    resolve: {
      alias: {
        '@': fileURLToPath(new URL('src', import.meta.url)),
        '@shared': fileURLToPath(new URL('../packages/shared/src', import.meta.url)),
      },
      dedupe: ['vue', 'ant-design-vue', '@ant-design/icons-vue'],
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
        // OpenIddict 令牌端点（登录密码流，不在 /api 前缀下）
        '/connect/token': {
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
