import { AbpError } from '@shared/core/http'

/** 与后端 UploadFileSignature 一致的魔数（防止改扩展名绕过白名单） */
const PDF_MAGIC = [0x25, 0x50, 0x44, 0x46] // %PDF
const ZIP_MAGIC = [0x50, 0x4B, 0x03, 0x04] // PK\x03\x04，.docx 的 ZIP 容器
const OLE2_MAGIC = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1] // Word 97-2003 .doc

const EXT_LABELS: Record<string, string> = {
  '.pdf': 'PDF',
  '.doc': 'Word 97-2003（.doc）',
  '.docx': 'Word（.docx）',
}

function headerStartsWith(header: Uint8Array, magic: number[]): boolean {
  if (header.length < magic.length) return false
  return magic.every((byte, i) => header[i] === byte)
}

/** 上传前本地识别文件真实格式，返回「提示 + 建议」（不拦截上传）；格式没问题返回 null */
export async function detectFileTypeWarning(file: File): Promise<string | null> {
  const match = file.name.match(/\.[^.]+$/)
  const extension = (match?.[0] ?? '').toLowerCase()
  if (!(extension in EXT_LABELS)) return null

  const header = new Uint8Array(await file.slice(0, 8).arrayBuffer())
  const isPdf = headerStartsWith(header, PDF_MAGIC)
  const isDocx = headerStartsWith(header, ZIP_MAGIC)
  const isDoc = headerStartsWith(header, OLE2_MAGIC)

  if ((extension === '.pdf' && isPdf) || (extension === '.docx' && isDocx) || (extension === '.doc' && isDoc)) {
    return null
  }

  if (extension === '.docx' && isDoc) {
    return '文件实际是 Word 97-2003（.doc）格式，但扩展名是 .docx。已按 Word 文档继续处理；如需规范格式，请另存为 .docx'
  }
  if (extension === '.doc' && isDocx) {
    return '文件实际是 Word（.docx）格式，但扩展名是 .doc。已按 Word 文档继续处理；如需规范格式，请另存为 .doc'
  }
  if (extension === '.pdf') {
    return '文件扩展名是 .pdf，但内容不是 PDF，解析会直接交给 PDF 解析器，可能失败。建议用 Word/WPS 另存为 PDF 后重传'
  }
  const actual = isPdf ? 'PDF' : isDocx ? 'Word（.docx）' : isDoc ? 'Word 97-2003（.doc）' : null
  if (actual) {
    return `文件内容与扩展名不符（检测到${actual}格式）。已按内容继续处理；如解析异常，请另存为${EXT_LABELS[extension]}后重传`
  }
  return `无法识别文件内容（不是有效的 PDF / Word 文档）。已继续处理；如解析异常，请用 Word 打开后另存为${EXT_LABELS[extension]}再重传`
}

/** 把上传失败原因翻译成具体提示；拿不到细节时回退到通用文案 */
export function describeUploadError(err: unknown): string {
  if (err instanceof AbpError) {
    if (err.code === 'BidCompare:UnsupportedFileType') {
      const reason = typeof err.data?.reason === 'string' ? err.data.reason : null
      if (reason) {
        return `不支持的文件类型：${reason}。请检查扩展名是否与文件内容一致（仅支持 PDF / .doc / .docx）`
      }
      return '不支持的文件类型，仅支持 PDF / Word（.doc / .docx）'
    }
    if (err.code === 'BidCompare:DocumentCountOutOfRange') {
      return `投标文件数量超出上限（最多 ${String(err.data?.max ?? 5)} 份）`
    }
    return err.message || '上传失败，请重试'
  }
  if (err instanceof Error) {
    if (/timeout|timed ?out/i.test(err.message)) return '上传超时，请检查网络后重试'
    return err.message || '上传失败，请重试'
  }
  return '上传失败，请重试'
}
