import type MockAdapter from 'axios-mock-adapter'
import { mockClauseLibrary, mockDocMetaInfos, mockEvidence, mockOverview, mockTasks } from '@shared/mock/data/compare'
import type { ClauseItem, CompareTask, EvidenceItem, TaskOverview } from '@/types'
import sampleUrl from '@shared/assets/dubbing-sample.mp3'

let nextTaskSeq = 3
let clauseSeq = 100

/** 内置示例任务（task-1）的文档 id 与证据引用一致，无需重映射 */
function isCanonical(task: CompareTask): boolean {
  return task.documents[0]?.id === 'doc-a'
}

/** 新建任务：把示例证据重映射到任务真实文档 id 上，保证证据标签/溯源一致 */
function remapEvidence(task: CompareTask): EvidenceItem[] {
  if (isCanonical(task)) return mockEvidence
  const ids = task.documents.map((d) => d.id)
  if (!ids.length) return mockEvidence
  return mockEvidence.map((ev, i) => ({
    ...ev,
    taskId: task.id,
    docIds: ev.docIds.map((_, j) => ids[(i + j) % ids.length]),
    refs: ev.refs.map((r, j) => ({ ...r, docId: ids[(i + j) % ids.length] })),
  }))
}

function remapOverview(task: CompareTask): TaskOverview {
  if (isCanonical(task)) return mockOverview
  const n = task.documents.length
  return {
    ...mockOverview,
    docLabels: task.documents.map((_, i) => String.fromCharCode(65 + i)),
    simMatrix: mockOverview.simMatrix.slice(0, n).map((row) => row.slice(0, n)),
    simMatrixSelf: mockOverview.simMatrixSelf.slice(0, n).map((row) => row.slice(0, n)),
    evidence: remapEvidence(task),
  }
}

/** 新建任务生成元数据：前两份刻意同源（作者/GUID/IP 一致），模拟串标迹象 */
function genMetaInfos(task: CompareTask): void {
  if (isCanonical(task)) return
  const now = new Date().toISOString().slice(0, 16).replace('T', ' ')
  task.documents.forEach((d, i) => {
    if (mockDocMetaInfos.some((m) => m.docId === d.id)) return
    if (i < 2) {
      mockDocMetaInfos.push({ docId: d.id, author: 'zhang.wei', creatorTool: 'WPS 11.1.0', producer: 'WPS Office PDF', guid: '9F3A-27C1-88BD-0042', ip: '172.16.8.23', createdAt: now })
    } else {
      mockDocMetaInfos.push({ docId: d.id, author: `user.${i}`, creatorTool: 'Microsoft Word 16.0', producer: 'Adobe PDF Library 21.5', guid: `7D42-F1B3-02CA-55${10 + i}`, ip: `10.20.3.${100 + i}`, createdAt: now })
    }
  })
}

// axios-mock-adapter 用拼接后的完整 URL 做匹配，但回调里 config.url 仍是相对路径，
// 因此 handler 内提取参数的正则不能依赖 /api 前缀
function extractId(url: string | undefined, pattern: RegExp): string {
  return url?.match(pattern)?.[1] || ''
}

export function registerCompareMock(
  mock: MockAdapter,
  wrap: (h: () => unknown) => () => Promise<[number, unknown]>,
): void {
  mock.onGet('/api/compare/tasks').reply(wrap(() => ({ items: mockTasks, totalCount: mockTasks.length })))

  mock.onPost('/api/compare/tasks').reply((config) => {
    const { name, documentIds, tenderDocumentId, docNames, tenderFileName } = JSON.parse(config.data) as {
      name: string
      documentIds: string[]
      tenderDocumentId?: string
      docNames?: string[]
      tenderFileName?: string
    }
    const id = `task-${nextTaskSeq++}`
    const documents = documentIds.map((docId: string, i: number) => ({
      id: docId,
      taskId: id,
      fileName: docNames?.[i] || `标书${i + 1}.pdf`,
      pages: 100 + i * 10,
      sizeBytes: 8_000_000 + i * 1_000_000,
      parseStatus: 'pending' as const,
    }))
    if (tenderDocumentId) {
      documents.unshift({
        id: tenderDocumentId,
        taskId: id,
        fileName: tenderFileName || '招标文件.pdf',
        pages: 60,
        sizeBytes: 5_000_000,
        parseStatus: 'pending' as const,
      })
    }
    const newTask: CompareTask = {
      id,
      name: name || '未命名比标任务',
      status: 'uploading',
      documents,
      progress: { parse: 0, compare: 0, ai: 0 },
      createdAt: new Date().toISOString(),
    }
    mockTasks.unshift(newTask)
    genMetaInfos(newTask)

    setTimeout(() => {
      const t = mockTasks.find((x) => x.id === id)
      if (t && t.status === 'uploading') { t.status = 'parsing'; t.progress.parse = 45 }
    }, 4000)
    setTimeout(() => {
      const t = mockTasks.find((x) => x.id === id)
      if (t && t.status === 'parsing') { t.progress.parse = 100; t.status = 'comparing'; t.progress.compare = 30 }
    }, 9000)
    setTimeout(() => {
      const t = mockTasks.find((x) => x.id === id)
      if (t && t.status === 'comparing') { t.progress.compare = 100; t.status = 'ai_analyzing'; t.progress.ai = 40 }
    }, 14000)
    setTimeout(() => {
      const t = mockTasks.find((x) => x.id === id)
      if (t && t.status === 'ai_analyzing') {
        t.progress.ai = 100
        t.status = 'completed'
        t.finishedAt = new Date().toISOString()
        t.riskSummary = { high: 3, mid: 2, low: 2, clauseMissing: 1 }
        t.responseMatrix = [
          ['√', '√', '√', '√', '△'],
          ['√', '√', '√', '×', '√'],
          ['√', '√', '√', '√', '√'],
        ]
      }
    }, 19000)

    return [200, newTask]
  })

  mock.onGet(/\/api\/compare\/tasks\/([^/]+)$/).reply((config) => {
    const id = extractId(config.url, /compare\/tasks\/([^/]+)$/)
    const task = mockTasks.find((t) => t.id === id)
    if (!task) return [404, { message: 'Task not found' }]
    if ((task.status === 'comparing' || task.status === 'ai_analyzing' || task.status === 'completed') && !task.riskSummary) {
      task.riskSummary = { high: 1, mid: 1, low: 0, clauseMissing: 0 }
    }
    return [200, task]
  })

  mock.onGet(/\/api\/compare\/tasks\/([^/]+)\/overview$/).reply((config) => {
    const id = extractId(config.url, /compare\/tasks\/([^/]+)\/overview$/)
    const task = mockTasks.find((t) => t.id === id)
    return task ? [200, remapOverview(task)] : [404, { message: 'Task not found' }]
  })

  mock.onGet(/\/api\/compare\/tasks\/([^/]+)\/evidence$/).reply((config) => {
    const id = extractId(config.url, /compare\/tasks\/([^/]+)\/evidence$/)
    const task = mockTasks.find((t) => t.id === id)
    return task ? [200, remapEvidence(task)] : [404, { message: 'Task not found' }]
  })

  mock.onGet(/\/api\/compare\/tasks\/([^/]+)\/documents$/).reply((config) => {
    const id = extractId(config.url, /compare\/tasks\/([^/]+)\/documents$/)
    const task = mockTasks.find((t) => t.id === id)
    return task ? [200, task.documents] : [404, { message: 'Task not found' }]
  })

  mock.onPost(/\/api\/compare\/tasks\/([^/]+)\/clauses$/).reply((config) => {
    const id = extractId(config.url, /compare\/tasks\/([^/]+)\/clauses$/)
    const task = mockTasks.find((t) => t.id === id)
    return task ? [200, { confirmed: true }] : [404, { message: 'Task not found' }]
  })

  const exportJobs = new Map<string, { status: 'processing' | 'done' | 'failed', downloadUrl?: string }>()

  mock.onPost(/\/api\/compare\/tasks\/([^/]+)\/export$/).reply(() => {
    const exportId = `exp-${Date.now()}`
    exportJobs.set(exportId, { status: 'processing' })
    setTimeout(() => {
      const job = exportJobs.get(exportId)
      if (job) {
        job.status = 'done'
        job.downloadUrl = sampleUrl
      }
    }, 2500)
    return [200, { exportId, status: 'processing' }]
  })

  mock.onGet(/\/api\/compare\/tasks\/([^/]+)\/export\/([^/]+)$/).reply((config) => {
    const exportId = extractId(config.url, /export\/([^/]+)$/)
    const job = exportJobs.get(exportId)
    return job ? [200, { exportId, ...job }] : [404, { message: 'Export not found' }]
  })

  mock.onGet('/api/compare/clauses').reply(wrap(() => mockClauseLibrary))

  mock.onPost('/api/compare/clauses').reply((config) => {
    const body = JSON.parse(config.data) as Omit<ClauseItem, 'id'>
    const clause: ClauseItem = { ...body, id: `cl-${clauseSeq++}` }
    mockClauseLibrary.push(clause)
    return [200, clause]
  })

  mock.onPut(/\/api\/compare\/clauses\/([^/]+)$/).reply((config) => {
    const id = extractId(config.url, /compare\/clauses\/([^/]+)$/)
    const clause = mockClauseLibrary.find((c) => c.id === id)
    if (!clause) return [404, { message: 'Clause not found' }]
    Object.assign(clause, JSON.parse(config.data))
    return [200, clause]
  })

  mock.onDelete(/\/api\/compare\/clauses\/([^/]+)$/).reply((config) => {
    const id = extractId(config.url, /compare\/clauses\/([^/]+)$/)
    const idx = mockClauseLibrary.findIndex((c) => c.id === id)
    if (idx >= 0) mockClauseLibrary.splice(idx, 1)
    return [204, undefined]
  })

  mock.onPost('/api/compare/upload').reply((config) => {
    const onProgress = (config as { onUploadProgress?: (e: { loaded: number, total: number }) => void }).onUploadProgress
    // 模拟渐进上传，让前端进度条可见
    return new Promise((resolve) => {
      let pct = 0
      const timer = setInterval(() => {
        pct += 20
        onProgress?.({ loaded: pct, total: 100 })
        if (pct >= 100) {
          clearInterval(timer)
          resolve([200, { documentId: `doc-${Date.now()}-${Math.floor(Math.random() * 1000)}` }])
        }
      }, 150)
    })
  })
}
