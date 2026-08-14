import { it } from 'vitest'
import assert from 'node:assert/strict'
import {
  MAX_BID_DOCUMENTS,
  buildDocLabels,
  docLabel,
  overviewDocLabels,
} from '../../packages/shared/src/core/utils/compare.ts'

const doc = (id: string, fileName: string, role?: string) => ({ id, fileName, role })

it('文件名自带字母时别名对齐（C.docx / D.docx）', () => {
  const docs = [doc('d1', 'C.docx'), doc('d2', 'D.docx')]
  assert.deepEqual(buildDocLabels(docs), { d1: 'C', d2: 'D' })
})

it('中文前缀与括号中的字母也能识别', () => {
  assert.equal(docLabel([doc('d1', '标书C.docx')], 'd1'), 'C')
  assert.equal(docLabel([doc('d1', '技术标（C）.pdf')], 'd1'), 'C')
})

it('文件名含多个独立字母时取第一个', () => {
  assert.equal(docLabel([doc('d1', '标书C-D.docx')], 'd1'), 'C')
})

it('嵌入英文单词/数字串的字母不识别，按顺序补位', () => {
  const docs = [doc('d1', 'CIF条款.pdf'), doc('d2', 'B2B报价.docx')]
  assert.deepEqual(buildDocLabels(docs), { d1: 'A', d2: 'B' })
})

it('a-H 之外的字母不参与自报', () => {
  assert.equal(docLabel([doc('d1', 'Z.docx')], 'd1'), 'A')
})

it('重复字母并列编号 C / C1', () => {
  const docs = [doc('d1', 'C.docx'), doc('d2', '标书C.docx')]
  assert.deepEqual(buildDocLabels(docs), { d1: 'C', d2: 'C1' })
})

it('无字母文件从 A 补位并跳过已占用字母', () => {
  const docs = [doc('d1', 'C.docx'), doc('d2', 'D.docx'), doc('d3', '无字母.docx'), doc('d4', '报价.pdf')]
  assert.deepEqual(buildDocLabels(docs), { d1: 'C', d2: 'D', d3: 'A', d4: 'B' })

  const three = [doc('d1', 'C.docx'), doc('d2', 'D.docx'), doc('d3', '无字母1.pdf'), doc('d4', '无字母2.pdf'), doc('d5', '无字母3.pdf')]
  assert.deepEqual(buildDocLabels(three), { d1: 'C', d2: 'D', d3: 'A', d4: 'B', d5: 'E' })
})

it('招标文件固定显示「招标」', () => {
  const docs = [doc('d1', 'C.docx'), doc('t1', '招标文件.pdf', 'tender')]
  assert.equal(docLabel(docs, 't1'), '招标')
  assert.equal(docLabel(docs, 'd1'), 'C')
})

it('小写字母统一转大写', () => {
  assert.equal(docLabel([doc('d1', 'c.docx')], 'd1'), 'C')
})

it('overviewDocLabels 按文档生成热力图标签', () => {
  const docs = [doc('d1', 'C.docx'), doc('d2', '无字母.pdf')]
  assert.deepEqual(overviewDocLabels(['d1', 'd2'], docs), ['C', 'A'])
})

it('标书上限为 8 份', () => {
  assert.equal(MAX_BID_DOCUMENTS, 8)
})
