import { describe, expect, it } from 'vitest'

import { matchTableCell, stripTrailingSourceNumber } from '../src/views/ai-bid/read/tableLocator'

describe('stripTrailingSourceNumber', () => {
  it('去掉末尾溯源页码，保留正文', () => {
    expect(stripTrailingSourceNumber('投标文件应加盖章印。10')).toBe('投标文件应加盖章印。')
    expect(stripTrailingSourceNumber('投标人不得修改或撤回投标文件。57')).toBe('投标人不得修改或撤回投标文件。')
  })

  it('无页码后缀时原样返回', () => {
    expect(stripTrailingSourceNumber('投标文件应加盖章印。')).toBe('投标文件应加盖章印。')
    expect(stripTrailingSourceNumber('增值税税率 6%')).toBe('增值税税率 6%')
  })

  it('纯数字内容剥离后为空', () => {
    expect(stripTrailingSourceNumber('10')).toBe('')
  })
})

describe('matchTableCell', () => {
  const cells = [
    { row: 0, col: 0, rowspan: 1, colspan: 1, pageIdx: 1, bbox: [0.1, 0.2, 0.5, 0.3], text: '条款号' },
    { row: 0, col: 1, rowspan: 1, colspan: 1, pageIdx: 1, bbox: [0.5, 0.2, 0.9, 0.3], text: '投标文件应加盖章印。' },
    { row: 1, col: 0, rowspan: 1, colspan: 2, pageIdx: 2, bbox: [0.1, 0.4, 0.9, 0.55], text: '凡二代身份证在开标时不能出示的，招标人当场退回其投标文件。' },
  ]

  it('整段条款命中包含它的单元格，返回该格 pageIdx + bbox', () => {
    const hit = matchTableCell(cells, '凡二代身份证在开标时不能出示的，招标人当场退回其投标文件。11')
    expect(hit).toEqual({ pageIdx: 2, bbox: [0.1, 0.4, 0.9, 0.55] })
  })

  it('跨页表格按单元格 page_idx 归属，不沿用表格块主页', () => {
    const hit = matchTableCell(cells, '投标文件应加盖章印。10')
    expect(hit?.pageIdx).toBe(1)
  })

  it('标点/空白归一化后仍可命中', () => {
    const hit = matchTableCell(cells, '投标文件 应加盖章印…10')
    expect(hit?.bbox).toEqual([0.5, 0.2, 0.9, 0.3])
  })

  it('条款跨多格时取最长的命中片段', () => {
    const frag = [
      { pageIdx: 1, bbox: [0.1, 0.1, 0.4, 0.2], text: '修改或撤回' },
      { pageIdx: 1, bbox: [0.1, 0.3, 0.4, 0.4], text: '但所递交的修改或撤回通知必须按招标文件的规定进行编制、密封' },
      { pageIdx: 1, bbox: [0.1, 0.5, 0.4, 0.6], text: '和递交。投标截止时间之后，投标人不得修改或撤回投标文件。' },
    ]
    const hit = matchTableCell(frag, '投标人可对所递交的投标文件进行修改或撤回，但所递交的修改或撤回通知必须按招标文件的规定进行编制、密封、标志和递交')
    expect(hit?.bbox).toEqual([0.1, 0.3, 0.4, 0.4])
  })

  it('单元格文本包含整段时优先于仅含片段的单元格', () => {
    const frag = [
      { pageIdx: 1, bbox: [0.1, 0.3, 0.4, 0.4], text: '但所递交的修改或撤回通知必须按招标文件的规定进行编制、密封' },
      { pageIdx: 1, bbox: [0.1, 0.6, 0.9, 0.8], text: '投标人可对所递交的投标文件进行修改或撤回，但所递交的修改或撤回通知必须按招标文件的规定进行编制、密封、标志和递交。' },
    ]
    const hit = matchTableCell(frag, '投标人可对所递交的投标文件进行修改或撤回，但所递交的修改或撤回通知必须按招标文件的规定进行编制、密封、标志和递交')
    expect(hit?.bbox).toEqual([0.1, 0.6, 0.9, 0.8])
  })

  it('无命中返回 null', () => {
    expect(matchTableCell(cells, '完全不存在的条款内容')).toBeNull()
  })

  it('bbox 非法时返回 null', () => {
    expect(matchTableCell([{ pageIdx: 0, bbox: [0, 0, 0, 0], text: 'x' }], 'x')).toBeNull()
  })
})
