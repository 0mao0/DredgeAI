import katex from 'katex'
import 'katex/dist/katex.min.css'

function escapeHtml(s: string): string {
  return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
}

function render(tex: string, displayMode: boolean): string {
  try {
    return katex.renderToString(tex, {
      displayMode,
      throwOnError: false,
      strict: 'ignore',
      output: 'html',
    })
  } catch {
    return `<code class="katex-error">${escapeHtml(tex)}</code>`
  }
}

const token = (i: number) => `\x00${i}\x00`

/** 提取 $$...$$ 与 $...$ 公式为占位符，返回替换后的文本与渲染好的 HTML 片段表 */
export function extractMath(source: string): { text: string, segments: string[] } {
  const segments: string[] = []
  let text = source.replace(/\$\$([\s\S]+?)\$\$/g, (_m, tex: string) => {
    segments.push(render(tex, true))
    return token(segments.length - 1)
  })
  text = text.replace(/\$([^$\n]+)\$/g, (_m, tex: string) => {
    segments.push(render(tex, false))
    return token(segments.length - 1)
  })
  return { text, segments }
}

/** 将占位符还原为渲染后的公式 HTML */
export function restoreMath(html: string, segments: string[]): string {
  return html.replace(/\0(\d+)\0/g, (_m, i: string) => segments[Number(i)] ?? '')
}
