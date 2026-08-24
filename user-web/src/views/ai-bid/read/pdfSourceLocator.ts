import * as pdfjsLib from 'pdfjs-dist'
import pdfWorkerUrl from 'pdfjs-dist/build/pdf.worker.min.mjs?url'
import { normalizeText } from './tableLocator'
import type { PreciseRect } from './tableLocator'

// 纯函数/类型拆到 tableLocator.ts（无 pdfjs 依赖，便于单测与复用），此处透传保持旧导入兼容
export type { PreciseRect, TableCellLocator } from './tableLocator'
export { estimateTableRowBbox, matchTableCell, normalizeText, parseTableRows, stripTrailingSourceNumber } from './tableLocator'

let workerReady = false

function ensureWorker(): void {
  if (!workerReady) {
    pdfjsLib.GlobalWorkerOptions.workerSrc = pdfWorkerUrl
    workerReady = true
  }
}

const docCache = new Map<string, Promise<pdfjsLib.PDFDocumentProxy>>()
const pageTextCache = new Map<string, Promise<PageTextItem[]>>()

interface PageTextItem {
  text: string
  left: number
  top: number
  width: number
  height: number
}

interface RawTextItem {
  str?: string
  transform?: number[]
  width?: number
}

function getDocument(url: string): Promise<pdfjsLib.PDFDocumentProxy> {
  ensureWorker()
  let promise = docCache.get(url)
  if (!promise) {
    promise = pdfjsLib.getDocument({ url }).promise
    docCache.set(url, promise)
  }
  return promise
}

/** 读取指定页的文本项（带缓存），bbox 算法与 docs-ui 的 loadPageTextItems 一致。 */
async function getPageTextItems(url: string, pageIdx: number): Promise<PageTextItem[]> {
  const key = `${url}#${pageIdx}`
  let promise = pageTextCache.get(key)
  if (!promise) {
    promise = (async () => {
      const doc = await getDocument(url)
      const pdfPage = await doc.getPage(pageIdx + 1)
      const viewport = pdfPage.getViewport({ scale: 1 })
      const vw = viewport.width || 1
      const vh = viewport.height || 1
      const textContent = await pdfPage.getTextContent()
      const items: PageTextItem[] = []
      for (const raw of (textContent.items || []) as RawTextItem[]) {
        const str = raw.str || ''
        if (!str || !raw.transform) continue
        // 合成 viewport 变换得到渲染坐标（y 已翻转为顶部基准）
        const tx = pdfjsLib.Util.transform(viewport.transform, raw.transform)
        const fontHeight = Math.hypot(tx[2], tx[3]) || 1
        const angle = Math.atan2(tx[1], tx[0])
        const cosA = Math.cos(angle)
        const sinA = Math.sin(angle)
        const width = Math.max(0, Number(raw.width) || 0) * viewport.scale
        const bx = tx[4]
        const by = tx[5]
        const ex = bx + width * cosA
        const ey = by + width * sinA
        const ax = bx + fontHeight * sinA
        const ay = by - fontHeight * cosA
        const dx = ex - fontHeight * 0.15 * sinA
        const dy = ey + fontHeight * 0.15 * cosA
        const xs = [bx, ex, ax, dx]
        const ys = [by, ey, ay, dy]
        const left = Math.min(...xs)
        const top = Math.min(...ys)
        const right = Math.max(...xs)
        const bottom = Math.max(...ys)
        items.push({
          text: str,
          left: Math.max(0, Math.min(1, left / vw)),
          top: Math.max(0, Math.min(1, top / vh)),
          width: Math.max(0, Math.min(1, (right - left) / vw)),
          height: Math.max(0, Math.min(1, (bottom - top) / vh)),
        })
      }
      return items
    })()
    pageTextCache.set(key, promise)
  }
  return promise
}

/**
 * 在 PDF 原生文本层中精确定位某段文字（跨文本项匹配），返回归一化 bbox。
 * 扫描件（无文本层）返回 null，交由调用方降级处理。
 */
export async function findTextBbox(
  url: string,
  pageIdx: number,
  needle: string,
): Promise<PreciseRect | null> {
  const target = normalizeText(needle)
  if (!target) return null
  const items = await getPageTextItems(url, pageIdx)
  if (items.length === 0) return null

  const chars: Array<{ ch: string, item: PageTextItem }> = []
  for (const item of items) {
    for (const ch of item.text) {
      const n = normalizeText(ch)
      if (n) chars.push({ ch: n, item })
    }
  }
  if (chars.length === 0) return null

  const joined = chars.map((c) => c.ch).join('')
  const idx = joined.indexOf(target)
  if (idx < 0) return null

  const hitItems = new Set<PageTextItem>()
  for (let i = idx; i < idx + target.length; i++) hitItems.add(chars[i].item)

  let left = 1
  let top = 1
  let right = 0
  let bottom = 0
  for (const item of hitItems) {
    left = Math.min(left, item.left)
    top = Math.min(top, item.top)
    right = Math.max(right, item.left + item.width)
    bottom = Math.max(bottom, item.top + item.height)
  }
  if (right <= left || bottom <= top) return null
  return { left, top, width: right - left, height: bottom - top }
}
