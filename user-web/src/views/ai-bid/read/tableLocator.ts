/** 精确矩形：0~1 归一化坐标，与 IR bbox 同体系。 */
export interface PreciseRect {
  left: number
  top: number
  width: number
  height: number
}

/** 与后端一致的归一化：去掉空白、标点、符号，统一小写。 */
export function normalizeText(input: string): string {
  let out = ''
  for (const ch of input) {
    if (/\s/u.test(ch)) continue
    if (/[\p{P}\p{S}]/u.test(ch)) continue
    out += ch.toLocaleLowerCase()
  }
  return out
}

/**
 * 去掉末尾的溯源页码后缀（如「投标文件应加盖章印。10」→「投标文件应加盖章印。」），
 *  用于 pdf.js 文本层 / 行估算的整段精确搜索，避免页码干扰匹配。
 */
export function stripTrailingSourceNumber(input: string): string {
  return input.replace(/\s*\d+\s*$/, '').trim()
}

/** docs-api 单元格级坐标（read/index.vue 的 IrTableCell 结构兼容，取交集字段）。 */
export interface TableCellLocator {
  pageIdx: number
  bbox: number[]
  text: string
}

/**
 * 在 docs-api 单元格级坐标中匹配条款原文：
 * 优先找「单元格文本包含整个条款」（取 bbox 最小的单元格）；
 * 否则找「条款包含的最长单元格片段」（条款跨多格时取最具体的命中格）。
 * 命中直接返回 cell.pageIdx + cell.bbox，天然解决扫描件与跨页归属。
 */
export function matchTableCell(
  cells: TableCellLocator[],
  needle: string,
): { pageIdx: number, bbox: number[] } | null {
  const target = normalizeText(needle)
  if (!target) return null
  let exact: TableCellLocator | null = null
  let fragment: TableCellLocator | null = null
  for (const cell of cells) {
    const cellText = normalizeText(cell.text)
    if (!cellText) continue
    if (cellText.includes(target)) {
      if (!exact || cellText.length < normalizeText(exact.text).length) exact = cell
    } else if (target.includes(cellText)) {
      if (!fragment || cellText.length > normalizeText(fragment.text).length) fragment = cell
    }
  }
  const cell = exact ?? fragment
  if (!cell || !Array.isArray(cell.bbox) || cell.bbox.length !== 4) return null
  const [x0, y0, x1, y1] = cell.bbox
  if (!(x1 > x0 && y1 > y0)) return null
  return { pageIdx: cell.pageIdx, bbox: [x0, y0, x1, y1] }
}

/** 解析 table.html 的行文本（去标签、合并单元格）。 */
export function parseTableRows(tableHtml: string): string[] {
  const rows: string[] = []
  const trRe = /<tr[^>]*>([\s\S]*?)<\/tr>/gi
  let m: RegExpExecArray | null
  m = trRe.exec(tableHtml)
  while (m) {
    const cells: string[] = []
    const tdRe = /<t[dh][^>]*>([\s\S]*?)<\/t[dh]>/gi
    let c: RegExpExecArray | null
    c = tdRe.exec(m[1])
    while (c) {
      cells.push(c[1].replace(/<[^>]+>/g, '').replace(/&nbsp;/gi, ' ').trim())
      c = tdRe.exec(m[1])
    }
    rows.push(cells.join(' '))
    m = trRe.exec(tableHtml)
  }
  return rows
}

/**
 * 扫描表格降级方案：docs-api 未提供单元格坐标，按「行文本长度加权」估算命中行在表格
 * bbox 内的纵向区域（长文本行更高），让溯源至少落在具体行而不是整张表。
 */
export function estimateTableRowBbox(
  tableHtml: string,
  blockBbox: number[],
  needle: string,
): PreciseRect | null {
  const target = normalizeText(needle)
  if (!target || blockBbox.length !== 4) return null
  const rows = parseTableRows(tableHtml)
  if (rows.length === 0) return null

  const idx = rows.findIndex((row) => normalizeText(row).includes(target))
  if (idx < 0) return null

  const weights = rows.map((row) => Math.max(2, row.replace(/\s/g, '').length))
  const total = weights.reduce((sum, w) => sum + w, 0)
  if (total <= 0) return null

  const [x0, y0, x1, y1] = blockBbox
  let acc = 0
  for (let i = 0; i < idx; i++) acc += weights[i]
  const rowTop = y0 + (acc / total) * (y1 - y0)
  const rowBottom = y0 + ((acc + weights[idx]) / total) * (y1 - y0)
  return {
    left: x0,
    top: rowTop,
    width: Math.max(x1 - x0, 0.001),
    height: Math.max(rowBottom - rowTop, 0.004),
  }
}
