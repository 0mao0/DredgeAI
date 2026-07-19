import type { StandardResult, StandardSearchHistory, StandardCategory } from '@shared/types'

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
  ]},
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
