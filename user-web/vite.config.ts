import { defineConfig, loadEnv } from 'vite'
import vue from '@vitejs/plugin-vue'
import UnoCSS from 'unocss/vite'
import { fileURLToPath, URL } from 'node:url'
import { copyFileSync, existsSync, mkdirSync, readdirSync } from 'node:fs'
import { createRequire } from 'node:module'
import { dirname, join } from 'node:path'

/**
 * pdf.js 6.x 运行资源复制插件（镜像 @angineer/docs-ui/vite-pdf-wasm：
 * 该子路径暂未在包 exports 中暴露，这里内联等价逻辑）。
 * 把 pdfjs-dist 的 cmaps / standard_fonts / wasm 复制到应用 public 目录，
 * 配合 PDF_Viewer 默认 `${BASE_URL}` 即可开箱即用。
 */
function copyDir(sourceDir: string, targetDir: string): void {
  if (!existsSync(sourceDir)) return
  mkdirSync(targetDir, { recursive: true })
  for (const file of readdirSync(sourceDir)) {
    const source = join(sourceDir, file)
    if (!existsSync(source)) continue
    copyFileSync(source, join(targetDir, file))
  }
}

function copyPdfAssets(publicDir: string): void {
  if (!publicDir) return
  const require = createRequire(import.meta.url)
  let pdfjsRoot = ''
  try {
    pdfjsRoot = dirname(require.resolve('pdfjs-dist/package.json'))
  } catch {
    console.warn('[pdf-wasm] 找不到 pdfjs-dist，跳过 cmaps/fonts/wasm 资源复制。')
    return
  }
  copyDir(join(pdfjsRoot, 'cmaps'), join(publicDir, 'cmaps'))
  copyDir(join(pdfjsRoot, 'standard_fonts'), join(publicDir, 'standard_fonts'))
  copyDir(join(pdfjsRoot, 'wasm'), join(publicDir, 'wasm'))
}

function pdfWasmPlugin(): { name: string, enforce: 'pre', configResolved: (config: { publicDir: string }) => void, buildStart: () => void, configureServer: () => void } {
  let publicDir = ''
  return {
    name: 'user-web:pdf-wasm',
    enforce: 'pre',
    configResolved(config) {
      publicDir = config.publicDir
    },
    buildStart() {
      copyPdfAssets(publicDir)
    },
    configureServer() {
      copyPdfAssets(publicDir)
    },
  }
}

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')
  return {
    plugins: [vue(), UnoCSS(), pdfWasmPlugin()],
    optimizeDeps: {
      exclude: ['@angineer/docs-ui'],
    },
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
