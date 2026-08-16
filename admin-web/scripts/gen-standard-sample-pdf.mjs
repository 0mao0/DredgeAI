/**
 * 生成知识库-标准规范模块演示 PDF（无第三方依赖，手写最小 PDF 结构）。
 * 中文使用预定义 CMap 的 Type0 字体（STSong-Light + UniGB-UCS2-H），
 * pdf.js 会用系统中文字体回退渲染。
 * 用法：node admin-web/scripts/gen-standard-sample-pdf.mjs
 */
import { mkdirSync, writeFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { dirname, join } from 'node:path'
import { Buffer } from 'node:buffer'

const OUT_DIR = join(dirname(fileURLToPath(import.meta.url)), '../public/mock/standards')
mkdirSync(OUT_DIR, { recursive: true })

const PAGE_W = 595
const PAGE_H = 842

/** 文本 → UTF-16BE（带 BOM）hex 字符串（UniGB-UCS2-H 编码要求） */
function hexText(str) {
  const le = Buffer.from(`\uFEFF${str}`, 'utf16le')
  const be = Buffer.alloc(le.length)
  for (let i = 0; i < le.length; i += 2) {
    be[i] = le[i + 1]
    be[i + 1] = le[i]
  }
  return `<${be.toString('hex').toUpperCase()}>`
}

/** 按最大字符数硬换行（中文按单字符估算宽度） */
function wrap(text, maxChars) {
  const lines = []
  let rest = text
  while (rest.length > maxChars) {
    lines.push(rest.slice(0, maxChars))
    rest = rest.slice(maxChars)
  }
  if (rest) lines.push(rest)
  return lines
}

/** 组装最小 PDF。pages: { x, y, size, text }[][]（y 为行顶坐标） */
function buildPdf(pages) {
  const objects = new Map()
  objects.set(3, '<< /Type /Font /Subtype /Type0 /BaseFont /STSong-Light /Encoding /UniGB-UCS2-H /DescendantFonts [ << /Type /Font /Subtype /CIDFontType0 /BaseFont /STSong-Light /CIDSystemInfo << /Registry (Adobe) /Ordering (GB1) /Supplement 5 >> /DW 1000 >> ] >>')
  const kids = []
  pages.forEach((lines, i) => {
    const pageNum = 4 + i * 2
    const contentNum = pageNum + 1
    kids.push(`${pageNum} 0 R`)
    const stream = lines
      .map((l) => `BT /F1 ${l.size} Tf 1 0 0 1 ${l.x} ${PAGE_H - l.y - l.size} Tm ${hexText(l.text)} Tj ET`)
      .join('\n')
    objects.set(pageNum, `<< /Type /Page /Parent 2 0 R /MediaBox [0 0 ${PAGE_W} ${PAGE_H}] /Resources << /Font << /F1 3 0 R >> >> /Contents ${contentNum} 0 R >>`)
    objects.set(contentNum, `<< /Length ${stream.length} >>\nstream\n${stream}\nendstream`)
  })
  objects.set(1, '<< /Type /Catalog /Pages 2 0 R >>')
  objects.set(2, `<< /Type /Pages /Kids [ ${kids.join(' ')} ] /Count ${pages.length} >>`)

  let out = '%PDF-1.4\n'
  const offsets = new Map()
  const max = Math.max(...objects.keys())
  for (let n = 1; n <= max; n++) {
    offsets.set(n, out.length)
    out += `${n} 0 obj\n${objects.get(n)}\nendobj\n`
  }
  const xrefPos = out.length
  out += `xref\n0 ${max + 1}\n0000000000 65535 f \n`
  for (let n = 1; n <= max; n++) {
    out += `${String(offsets.get(n)).padStart(10, '0')} 00000 n \n`
  }
  out += `trailer\n<< /Size ${max + 1} /Root 1 0 R >>\nstartxref\n${xrefPos}\n%%EOF\n`
  return out
}

const STANDARDS = [
  {
    id: 'std-1',
    name: '中华人民共和国河道管理条例',
    code: '国务院令第698号',
    industry: '水利',
    nature: '强制',
    level: '国家标准',
    status: '现行',
    issuer: '国务院',
    publishYear: 2018,
    description: '《中华人民共和国河道管理条例》旨在加强河道管理，保障防洪安全，发挥江河湖泊的综合效益。本条例适用于中华人民共和国领域内的河道，包括湖泊、人工水道、行洪区、蓄洪区、滞洪区。',
    articles: [
      '第一条 为加强河道管理，保障防洪安全，发挥江河湖泊的综合效益，根据《中华人民共和国水法》，制定本条例。',
      '第二条 本条例适用于中华人民共和国领域内的河道，包括湖泊、人工水道、行洪区、蓄洪区、滞洪区。',
      '第十条 河道的整治与建设，应当服从流域综合规划，符合国家规定的防洪标准、通航标准和其他有关技术要求，维护堤防安全，保持河势稳定和行洪、航运通畅。',
    ],
  },
  {
    id: 'std-2',
    name: '中华人民共和国防洪条例',
    code: '国务院令第48号',
    industry: '水利',
    nature: '强制',
    level: '国家标准',
    status: '现行',
    issuer: '国务院',
    publishYear: 2011,
    description: '《中华人民共和国防洪条例》旨在做好防汛抗洪工作，保障人民生命财产安全和经济建设的顺利进行。',
    articles: [
      '第一条 为了做好防汛抗洪工作，保障人民生命财产安全和经济建设的顺利进行，根据《中华人民共和国水法》，制定本条例。',
      '第二条 防汛工作实行“安全第一，常备不懈，以防为主，全力抢险”的方针。',
    ],
  },
  {
    id: 'std-3',
    name: '建设工程质量管理条例',
    code: '建设工程质量管理条例',
    industry: '建筑',
    nature: '强制',
    level: '国家标准',
    status: '现行',
    issuer: '国务院',
    publishYear: 2019,
    description: '《建设工程质量管理条例》旨在加强对建设工程质量的管理，保证建设工程质量，保护人民生命和财产安全。',
  },
  {
    id: 'std-4',
    name: '中华人民共和国水土保持法',
    code: '主席令第39号',
    industry: '水利',
    nature: '强制',
    level: '国家标准',
    status: '现行',
    issuer: '全国人大常委会',
    publishYear: 2010,
    description: '《中华人民共和国水土保持法》旨在预防和治理水土流失，保护和合理利用水土资源，减轻水、旱、风沙灾害。',
  },
  {
    id: 'std-5',
    name: '水利工程建设程序管理暂行规定',
    code: '水建[1998]16号',
    industry: '水利',
    nature: '推荐',
    level: '行业标准',
    status: '现行',
    issuer: '水利部',
    publishYear: 1998,
    description: '本规定适用于水利工程建设项目的前期工作、可行性研究、初步设计、施工准备、建设实施、生产准备、竣工验收、后评价等阶段的管理。',
  },
  {
    id: 'std-6',
    name: '河道管理范围内建设项目管理的有关规定',
    code: '水政[1992]7号',
    industry: '水利',
    nature: '推荐',
    level: '行业标准',
    status: '现行',
    issuer: '水利部',
    publishYear: 1992,
    description: '本规定适用于在河道管理范围内新建、扩建、改建的建设项目，包括开发水利（水电）、防治水害、整治河道的各类工程。',
  },
  {
    id: 'std-7',
    name: '取水许可和水资源费征收管理条例',
    code: '国务院令第460号',
    industry: '水利',
    nature: '强制',
    level: '国家标准',
    status: '现行',
    issuer: '国务院',
    publishYear: 2006,
    description: '本条例旨在加强水资源管理和保护，促进水资源的节约与合理开发利用。',
  },
  {
    id: 'std-8',
    name: '中华人民共和国防洪法',
    code: '主席令第88号',
    industry: '水利',
    nature: '强制',
    level: '国家标准',
    status: '现行',
    issuer: '全国人大常委会',
    publishYear: 2016,
    description: '《中华人民共和国防洪法》旨在防治洪水，防御、减轻洪涝灾害，维护人民的生命和财产安全。',
  },
  {
    id: 'std-9',
    name: '疏浚工程施工技术规范',
    code: 'JTS 207-2012',
    industry: '水利',
    nature: '推荐',
    level: '行业标准',
    status: '现行',
    issuer: '交通运输部',
    publishYear: 2012,
    description: '规定了疏浚工程施工的技术要求、施工准备、质量控制与验收等内容，适用于疏浚工程施工。',
  },
  {
    id: 'std-10',
    name: '水运工程混凝土施工规范',
    code: 'JTS 202-2011',
    industry: '交通',
    nature: '指导',
    level: '行业标准',
    status: '现行',
    issuer: '交通运输部',
    publishYear: 2011,
    description: '用于指导水运工程混凝土施工的原材料、配合比、浇筑与养护等环节。',
  },
  {
    id: 'std-11',
    name: '天津市河道管理技术标准',
    code: 'DB12/T 986-2021',
    industry: '水利',
    nature: '推荐',
    level: '地方标准',
    status: '现行',
    issuer: '天津市水务局',
    publishYear: 2021,
    description: '结合天津市河道管理实际制定，明确河道巡查、维护与治理的技术要求。',
  },
  {
    id: 'std-12',
    name: '疏浚工程环保技术团体标准',
    code: 'T/CWEA 12-2022',
    industry: '环保',
    nature: '推荐',
    level: '团体标准',
    status: '即将实施',
    issuer: '中国水利企业协会',
    publishYear: 2022,
    description: '针对疏浚工程环境影响控制制定，涵盖泥浆处理、噪声防治与生态保护要求。',
  },
  {
    id: 'std-13',
    name: '企业安全文明施工管理规范',
    code: 'Q/SDGC 001-2020',
    industry: '建筑',
    nature: '指导',
    level: '企业标准',
    status: '现行',
    issuer: '山东工程集团',
    publishYear: 2020,
    description: '结合企业项目管理经验制定，指导施工现场安全与文明施工管理。',
  },
  {
    id: 'std-14',
    name: 'ISO 9001:2015 质量管理体系',
    code: 'ISO 9001:2015',
    industry: '综合',
    nature: '推荐',
    level: '国际标准',
    status: '现行',
    issuer: '国际标准化组织',
    publishYear: 2015,
    description: '国际通用的质量管理体系标准，规定了组织建立、实施、保持和改进质量管理体系的要求。',
  },
  {
    id: 'std-15',
    name: '中华人民共和国水法',
    code: '主席令第74号',
    industry: '水利',
    nature: '强制',
    level: '法律法规',
    status: '现行',
    issuer: '全国人大常委会',
    publishYear: 2016,
    description: '规范水资源开发、利用、节约、保护与管理工作，是水利行业的基础性法律。',
  },
  {
    id: 'std-16',
    name: '建筑工程施工质量验收统一标准（旧版）',
    code: 'GB 50300-2001',
    industry: '建筑',
    nature: '强制',
    level: '国家标准',
    status: '作废',
    issuer: '住房和城乡建设部',
    publishYear: 2001,
    description: '已被 GB 50300-2013 替代，原标准规定建筑工程施工质量验收的基本要求。',
  },
]

function buildPages(standard) {
  const metaLine1 = `行业：${standard.industry}    性质：${standard.nature}    级别：${standard.level}`
  const metaLine2 = `状态：${standard.status}    发布部门：${standard.issuer}    发布年份：${standard.publishYear}`
  const page1 = [
    { x: 60, y: 72, size: 18, text: standard.name },
    { x: 60, y: 112, size: 13, text: `编号：${standard.code}` },
    { x: 60, y: 150, size: 12, text: metaLine1 },
    { x: 60, y: 178, size: 12, text: metaLine2 },
    { x: 60, y: 220, size: 12, text: '简介：' },
    ...wrap(standard.description, 38).map((line, i) => ({ x: 72, y: 248 + i * 22, size: 12, text: line })),
  ]
  const articles = standard.articles ?? [
    `第一条 为规范${standard.industry}领域的标准实施与监督管理，制定本文件。`,
    `第二条 本文件适用于${standard.name}（${standard.code}）所涉及的管理与技术要求。`,
    `第三条 ${standard.description}`,
  ]
  const page2 = [
    { x: 60, y: 72, size: 16, text: '条款摘录' },
    ...articles.flatMap((article, i) =>
      wrap(article, 40).map((line, j) => ({ x: 60, y: 120 + (i * 2 + j) * 22, size: 12, text: line }))),
  ]
  return [page1, page2]
}

for (const standard of STANDARDS) {
  const pages = buildPages(standard)
  writeFileSync(join(OUT_DIR, `${standard.id}.pdf`), buildPdf(pages), 'latin1')
  console.log(`generated public/mock/standards/${standard.id}.pdf (${pages.length} page(s))`)
}
