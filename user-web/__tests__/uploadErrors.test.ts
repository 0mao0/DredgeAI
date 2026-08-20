import { AbpError } from '@shared/core/http'
import { describeUploadError, detectFileTypeWarning } from '@/views/ai-bid/compare/uploadErrors'
import { describe, expect, it } from 'vitest'

function fileWithHeader(name: string, header: number[]): File {
  return new File([new Uint8Array(header)], name, { type: 'application/octet-stream' })
}

const OLE2_HEADER = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1] // Word 97-2003 .doc
const ZIP_HEADER = [0x50, 0x4B, 0x03, 0x04, 0x00, 0x00, 0x00, 0x00] // .docx ZIP 容器
const PDF_HEADER = [0x25, 0x50, 0x44, 0x46, 0x00, 0x00, 0x00, 0x00] // %PDF

describe('detectFileTypeWarning', () => {
  it('docx 后缀 + OLE2（.doc）内容时返回可操作提示而不是 null', async () => {
    const warning = await detectFileTypeWarning(fileWithHeader('投标文件港口院.docx', OLE2_HEADER))

    expect(warning).not.toBeNull()
    expect(warning).toContain('.doc')
    expect(warning).toContain('.docx')
  })

  it('扩展名与内容一致时返回 null', async () => {
    expect(await detectFileTypeWarning(fileWithHeader('标书.docx', ZIP_HEADER))).toBeNull()
    expect(await detectFileTypeWarning(fileWithHeader('标书.doc', OLE2_HEADER))).toBeNull()
    expect(await detectFileTypeWarning(fileWithHeader('规范.pdf', PDF_HEADER))).toBeNull()
  })
})

describe('describeUploadError', () => {
  it('保留后端具体原因', () => {
    const err = new AbpError({
      code: 'BidCompare:UnsupportedFileType',
      message: '不支持的文件类型',
      details: null,
      data: { extension: '.docx', reason: '文件内容与扩展名不符（魔数校验失败）' },
      validationErrors: null,
    })

    const text = describeUploadError(err)
    expect(text).toContain('文件内容与扩展名不符')
    expect(text).toContain('.docx')
  })
})
