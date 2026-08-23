import type { StandardResult, StandardSearchHistory, StandardCategory, StandardListItem, StandardProperty, StandardDocument, StandardAIAnalysis, StandardRecord, StandardFile } from '@shared/types'

export const standardsSearchHistory: StandardSearchHistory[] = [
  { id: 'h-1', query: 'GB/T 19001 质量管理体系', date: '2026-07-17 10:15', resultCount: 3 },
  { id: 'h-2', query: '施工质量验收标准', date: '2026-07-16 14:38', resultCount: 5 },
  { id: 'h-3', query: '合同审查相关规范', date: '2026-07-14 09:22', resultCount: 2 },
  { id: 'h-4', query: '安全生产标准化', date: '2026-07-10 16:50', resultCount: 4 },
]

export const standardsResult: StandardResult[] = [
  { id: 'std-1', code: 'GB/T 19001-2016', title: '质量管理体系 要求', match: '条款 7.1.4 — 过程运行环境', excerpt: '组织应确定、提供并维护所需的过程运行环境，以获得合格产品和服务。', source: '国家标准全文公开系统' },
  { id: 'std-2', code: 'GB/T 50430-2017', title: '工程建设施工企业质量管理规范', match: '条款 3.2 — 质量管理体系策划', excerpt: '施工企业应建立并实施质量管理体系，并持续改进其有效性。', source: '国家标准全文公开系统' },
  { id: 'std-3', code: 'GB 50300-2013', title: '建筑工程施工质量验收统一标准', match: '条款 4.0 — 验收基本规定', excerpt: '建筑工程施工质量应按下列要求进行验收：参与验收各方人员应具备规定的资格。', source: '国家标准全文公开系统' },
  { id: 'std-4', code: 'GB/T 28001-2011', title: '职业健康安全管理体系 要求', match: '条款 4.4.6 — 运行控制', excerpt: '组织应确定与所认定的风险相关的、需要采取控制措施的运行和活动。', source: '国家标准全文公开系统' },
  { id: 'std-5', code: 'JGJ 59-2011', title: '建筑施工安全检查标准', match: '条款 3 — 检查评分', excerpt: '建筑施工安全检查评定中保证项目应全数检查，保证项目得分必须为合格。', source: '行业标准全文公开系统' },
]

export const standardCategories: StandardCategory[] = [
  { id: 'c-1', name: '国家标准（GB）', count: 1250, children: [
    { id: 'c-1-1', name: '工程建设', count: 320 },
    { id: 'c-1-2', name: '质量管理', count: 180 },
    { id: 'c-1-3', name: '安全环保', count: 210 },
  ] },
  { id: 'c-2', name: '行业标准（JGJ）', count: 680 },
  { id: 'c-3', name: '地方标准（DB）', count: 420 },
  { id: 'c-4', name: '团体标准（T）', count: 280 },
]

export const recommendedQuestions = [
  '质量管理体系运行环境有哪些要求？',
  '施工质量验收的基本规定是什么？',
  '职业健康安全管理体系如何运行控制？',
  '建筑施工安全检查如何评分？',
]

export const standardList: StandardListItem[] = [
  { id: 'std-1', name: '中华人民共和国河道管理条例', code: '国务院令第698号' },
  { id: 'std-2', name: '中华人民共和国防汛条例', code: '国务院令第148号' },
  { id: 'std-3', name: '建设工程质量管理条例', code: '国务院令第279号' },
  { id: 'std-4', name: '中华人民共和国水土保持法', code: '主席令第39号' },
  { id: 'std-5', name: '水利工程建设程序管理暂行规定', code: '水建[1998]16号' },
  { id: 'std-6', name: '河道管理范围内建设项目管理的有关规定', code: '水政[1992]7号' },
  { id: 'std-7', name: '取水许可和水资源费征收管理条例', code: '国务院令第460号' },
  { id: 'std-8', name: '中华人民共和国防洪法', code: '主席令第88号' },
]

export const standardProperties: StandardProperty[] = [
  {
    id: 'std-1',
    name: '中华人民共和国河道管理条例',
    code: '中华人民共和国河道管理条例（2018修订）',
    industry: '水利',
    nature: '强制性标准',
    level: '国家标准',
    status: '现行',
    issuer: '国务院',
    publishYear: 2018,
    parentId: '国家标准',
    description: '《中华人民共和国河道管理条例》旨在加强河道管理，保障防洪安全，发挥江河湖泊的综合效益。本条例适用于中华人民共和国领域内的河道，包括湖泊、人工水道、行洪区、蓄洪区、滞洪区。',
  },
  {
    id: 'std-2',
    name: '中华人民共和国防汛条例',
    code: '中华人民共和国防汛条例',
    industry: '水利',
    nature: '强制性标准',
    level: '国家标准',
    status: '现行',
    issuer: '国务院',
    publishYear: 2011,
    parentId: '国家标准',
    description: '《中华人民共和国防汛条例》旨在做好防汛抗洪工作，保障人民生命财产安全和经济建设的顺利进行。',
  },
  {
    id: 'std-3',
    name: '建设工程质量管理条例',
    code: '建设工程质量管理条例',
    industry: '建筑',
    nature: '强制性标准',
    level: '国家标准',
    status: '现行',
    issuer: '国务院',
    publishYear: 2019,
    parentId: '国家标准',
    description: '《建设工程质量管理条例》旨在加强对建设工程质量的管理，保证建设工程质量，保护人民生命和财产安全。',
  },
  {
    id: 'std-4',
    name: '中华人民共和国水土保持法',
    code: '中华人民共和国水土保持法',
    industry: '水利',
    nature: '强制性标准',
    level: '国家标准',
    status: '现行',
    issuer: '全国人大常委会',
    publishYear: 2010,
    parentId: '国家标准',
    description: '《中华人民共和国水土保持法》旨在预防和治理水土流失，保护和合理利用水土资源，减轻水、旱、风沙灾害。',
  },
  {
    id: 'std-5',
    name: '水利工程建设程序管理暂行规定',
    code: '水建[1998]16号',
    industry: '水利',
    nature: '推荐性标准',
    level: '行业标准',
    status: '现行',
    issuer: '水利部',
    publishYear: 1998,
    parentId: '行业标准',
    description: '本规定适用于水利工程建设项目的立项、可行性研究、初步设计、施工准备、建设实施、生产准备、竣工验收、后评价等阶段的管理。',
  },
  {
    id: 'std-6',
    name: '河道管理范围内建设项目管理的有关规定',
    code: '水政[1992]7号',
    industry: '水利',
    nature: '推荐性标准',
    level: '行业标准',
    status: '现行',
    issuer: '水利部',
    publishYear: 1992,
    parentId: '行业标准',
    description: '本规定适用于在河道管理范围内新建、扩建、改建的建设项目，包括开发水利（水电）、防治水害、整治河道的各类工程。',
  },
  {
    id: 'std-7',
    name: '取水许可和水资源费征收管理条例',
    code: '国务院令第460号',
    industry: '水利',
    nature: '强制性标准',
    level: '国家标准',
    status: '现行',
    issuer: '国务院',
    publishYear: 2006,
    parentId: '国家标准',
    description: '本条例旨在加强水资源管理和保护，促进水资源的节约与合理开发利用。',
  },
  {
    id: 'std-8',
    name: '中华人民共和国防洪法',
    code: '主席令第88号',
    industry: '水利',
    nature: '强制性标准',
    level: '国家标准',
    status: '现行',
    issuer: '全国人大常委会',
    publishYear: 2016,
    parentId: '国家标准',
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
    parentId: '行业标准',
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
    parentId: '行业标准',
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
    parentId: '地方标准',
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
    parentId: '团体标准',
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
    parentId: '企业标准',
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
    parentId: '国际标准',
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
    parentId: '法律法规',
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
    parentId: '国家标准',
    description: '已被 GB 50300-2013 替代，原标准规定建筑工程施工质量验收的基本要求。',
  },
]

export const standardDocuments: StandardDocument[] = [
  {
    id: 'std-1',
    title: '中华人民共和国河道管理条例',
    content: `# 中华人民共和国河道管理条例

## 第一章 总则

**第一条** 为加强河道管理，保障防洪安全，发挥江河湖泊的综合效益，根据《中华人民共和国水法》，制定本条例。

**第二条** 本条例适用于中华人民共和国领域内的河道（包括湖泊、人工水道、行洪区、蓄洪区、滞洪区）。

**第三条** 开发利用江河湖泊水资源和防治水害，应当全面规划、统筹兼顾、综合利用、讲求效益，服从防洪的总体安排。

## 第二章 河道整治与建设

**第十条** 河道的整治与建设，应当服从流域综合规划，符合国家规定的防洪标准、通航标准和其他有关技术要求，维护堤防安全，保持河势稳定和行洪、航运通畅。

**第十一条** 修建开发水利、防治水害、整治河道的各类工程和跨河、穿河、穿堤、临河的桥梁、码头、道路、渡口、管道、缆线等建筑物及设施，建设单位必须按照河道管理权限，将工程建设方案报送河道主管机关审查同意后，方可按照基本建设程序履行审批手续。

## 第三章 河道保护

**第二十条** 有堤防的河道，其管理范围为两岸堤防之间的水域、沙洲、滩地（包括可耕地）、行洪区，两岸堤防及护堤地。

**第二十一条** 在河道管理范围内，水域和土地的利用应当符合江河行洪、输水和航运的要求；滩地的利用应当由河道主管机关会同土地管理等有关部门制定规划，报县级以上地方人民政府批准后实施。`,
  },
  {
    id: 'std-2',
    title: '中华人民共和国防汛条例',
    content: `# 中华人民共和国防汛条例

## 第一章 总则

**第一条** 为了做好防汛抗洪工作，保障人民生命财产安全和经济建设的顺利进行，根据《中华人民共和国水法》，制定本条例。

**第二条** 防汛工作实行"安全第一，常备不懈，以防为主，全力抢险"的方针。
`,
  },
]

/** 现有 StandardProperty 映射为规范 StandardRecord（字段对齐：issuer→department / publishYear→year / description→content） */
export const standardRecords: StandardRecord[] = standardProperties.map((p) => ({
  id: p.id,
  externalId: `ext-${p.id}`,
  parentId: p.parentId,
  status: p.status,
  nature: p.nature,
  level: p.level,
  department: p.issuer,
  industry: p.industry,
  year: p.publishYear,
  name: p.name,
  code: p.code,
  content: p.description,
  isEnabled: p.status !== '作废',
  source: 'remote',
  syncedAt: '2026-08-20T03:00:00Z',
  externalUpdatedAt: '2026-08-19T10:00:00Z',
}))

/** 附件 mock：按标准 id 挂 1~2 份附件 */
export const standardFilesByRecord: Record<string, StandardFile[]> = {
  'std-1': [
    { id: 'f-1-1', fileName: '河道管理条例（2018修订）全文.pdf', fileExtension: '.pdf', fileSize: 1523400, mimeType: 'application/pdf', parseStatus: 'parsed' },
  ],
  'std-9': [
    { id: 'f-9-1', fileName: '疏浚工程施工技术规范.pdf', fileExtension: '.pdf', fileSize: 2845000, mimeType: 'application/pdf', parseStatus: 'parsed' },
    { id: 'f-9-2', fileName: '疏浚工程施工技术规范（条文说明）.pdf', fileExtension: '.pdf', fileSize: 1120000, mimeType: 'application/pdf', parseStatus: 'parsing' },
  ],
  'std-16': [
    { id: 'f-16-1', fileName: 'GB 50300-2001 旧版.pdf', fileExtension: '.pdf', fileSize: 960000, mimeType: 'application/pdf', parseStatus: 'failed', parseError: '文档扫描件模糊，OCR 置信度过低' },
  ],
}

export const standardAIAnalyses: StandardAIAnalysis[] = [
  {
    id: 'std-1',
    summary: '该条例是河道管理的核心法规，明确了河道整治、建设、保护及处罚的各项规定。2018年修订版强化了河道管理范围内的执法力度和处罚标准。',
    keyPoints: [
      '明确河道管理范围：有堤防的河道以两岸堤防为界',
      '河道整治建设须经河道主管机关审查同意',
      '禁止在河道管理范围内建设妨碍行洪的建筑物',
    ],
    relatedStandards: [
      { code: 'GB 50201-2014', title: '防洪标准' },
      { code: 'SL 252-2017', title: '水利水电工程等级划分及洪水标准' },
    ],
    riskWarnings: [
      '2018年修订版提高了违法建设处罚力度，罚款上限提高至10万元',
      '跨河、穿河工程须同时提交防洪评价报告',
    ],
  },
]
