import { test } from 'node:test'
import assert from 'node:assert/strict'
import { isPdfFileName } from '../../packages/shared/src/core/utils/compare.ts'

test('isPdfFileName 只放行 .pdf 文件（不区分大小写）', () => {
  assert.equal(isPdfFileName('标书A.pdf'), true)
  assert.equal(isPdfFileName('BID.PDF'), true)
  assert.equal(isPdfFileName('C.docx'), false)
  assert.equal(isPdfFileName('D.doc'), false)
  assert.equal(isPdfFileName('无扩展名'), false)
  assert.equal(isPdfFileName(undefined), false)
})
