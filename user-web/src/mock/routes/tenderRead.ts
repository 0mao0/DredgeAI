import type MockAdapter from 'axios-mock-adapter'
import {
  tenderReadBaseline,
  tenderReadDocuments,
  tenderReadOutline,
  tenderReadTasks,
} from '@shared/mock/data/tenderRead'
import type { TenderReadingTask } from '@/types'

const TASK_PATH = /^\/api\/tender-read\/tasks$/
const TASK_DETAIL_PATH = /^\/api\/tender-read\/tasks\/([^/]+)$/
const TASK_DOCUMENTS_PATH = /^\/api\/tender-read\/tasks\/([^/]+)\/documents$/
const TASK_DOCUMENT_PATH = /^\/api\/tender-read\/tasks\/([^/]+)\/document$/
const TASK_PARSE_PATH = /^\/api\/tender-read\/tasks\/([^/]+)\/parse$/
const TASK_REPARSE_PATH = /^\/api\/tender-read\/tasks\/([^/]+)\/reparse$/
const TASK_OUTLINE_PATH = /^\/api\/tender-read\/tasks\/([^/]+)\/outline$/
const TASK_BASELINE_PATH = /^\/api\/tender-read\/tasks\/([^/]+)\/baseline$/
const TASK_FIELD_PATH = /^\/api\/tender-read\/tasks\/([^/]+)\/fields\/([^/]+)$/
const TASK_RE_EXTRACT_PATH = /^\/api\/tender-read\/tasks\/([^/]+)\/re-extract$/
const TASK_EXPORT_PATH = /^\/api\/tender-read\/tasks\/([^/]+)\/export$/

// axios-mock-adapter 匹配时使用完整 URL（含 baseURL），但 handler 中 config.url 仍是相对路径。
// 因此注册用 /api/ 前缀，参数提取用相对路径正则。
const REL_TASK_DETAIL = /^\/tender-read\/tasks\/([^/]+)$/
const REL_TASK_DOCUMENTS = /^\/tender-read\/tasks\/([^/]+)\/documents$/
const REL_TASK_DOCUMENT = /^\/tender-read\/tasks\/([^/]+)\/document$/
const REL_TASK_PARSE = /^\/tender-read\/tasks\/([^/]+)\/parse$/
const REL_TASK_REPARSE = /^\/tender-read\/tasks\/([^/]+)\/reparse$/
const REL_TASK_OUTLINE = /^\/tender-read\/tasks\/([^/]+)\/outline$/
const REL_TASK_BASELINE = /^\/tender-read\/tasks\/([^/]+)\/baseline$/
const REL_TASK_FIELD = /^\/tender-read\/tasks\/([^/]+)\/fields\/([^/]+)$/
const REL_TASK_RE_EXTRACT = /^\/tender-read\/tasks\/([^/]+)\/re-extract$/
const REL_TASK_EXPORT = /^\/tender-read\/tasks\/([^/]+)\/export$/

/** 模拟后台重抽：提交后进入 extracting，下一次轮询任务详情时落定 ready */
const reExtractingTaskIds = new Set<string>()

export function registerTenderReadMock(
  mock: MockAdapter,
  wrap: (h: () => unknown) => () => Promise<[number, unknown]>,
): void {
  mock.onGet(TASK_PATH).reply(wrap(() => ({ items: tenderReadTasks, totalCount: tenderReadTasks.length })))

  mock.onPost(TASK_PATH).reply((config) => {
    const body = JSON.parse(config.data as string) as { name?: string, projectCode?: string }
    const task: TenderReadingTask = {
      id: `tender-read-${Date.now()}`,
      name: body.name ?? '未命名读标任务',
      projectCode: body.projectCode ?? null,
      status: 'uploading',
      progressStage: 'uploading',
      progressPercent: 5,
      baselineVersion: 1,
      failureReason: null,
      docIds: [],
      createdAt: new Date().toISOString(),
    }
    tenderReadTasks.unshift(task)
    return [200, task]
  })

  mock.onGet(TASK_DETAIL_PATH).reply((config) => {
    const id = (config.url?.match(REL_TASK_DETAIL)?.[1] ?? '') as string
    const task = tenderReadTasks.find((t) => t.id === id)
    if (!task) return [404, { error: { message: '读标任务不存在' } }]
    if (reExtractingTaskIds.delete(id)) markMockReady(task)
    return [200, task]
  })

  mock.onDelete(TASK_DETAIL_PATH).reply((config) => {
    const id = (config.url?.match(REL_TASK_DETAIL)?.[1] ?? '') as string
    const index = tenderReadTasks.findIndex((t) => t.id === id)
    if (index >= 0) tenderReadTasks.splice(index, 1)
    return [204, null]
  })

  mock.onGet(TASK_DOCUMENTS_PATH).reply((config) => {
    const id = (config.url?.match(REL_TASK_DOCUMENTS)?.[1] ?? '') as string
    return [200, tenderReadDocuments[id] ?? []]
  })

  mock.onPost(TASK_DOCUMENT_PATH).reply((config) => {
    const id = (config.url?.match(REL_TASK_DOCUMENT)?.[1] ?? '') as string
    const docs = tenderReadDocuments[id] ?? []
    const doc = {
      id: `tr-doc-${Date.now()}`,
      taskId: id,
      fileName: '招标文件.pdf',
      fileSize: 1024 * 1024,
      parseStatus: 'pending' as const,
      parseError: null,
      parseProgress: null,
      parseStage: null,
      parseStageMessage: null,
      parseStartedAt: null,
      parseFinishedAt: null,
      pageCount: null,
      createdAt: new Date().toISOString(),
    }
    docs.push(doc)
    tenderReadDocuments[id] = docs
    const task = tenderReadTasks.find((t) => t.id === id)
    if (task) {
      task.docIds.push(doc.id)
      task.status = 'uploading'
      task.progressStage = 'uploading'
      task.progressPercent = 5
    }
    return [200, doc]
  })

  mock.onPost(TASK_PARSE_PATH).reply((config) => {
    const id = (config.url?.match(REL_TASK_PARSE)?.[1] ?? '') as string
    const task = tenderReadTasks.find((t) => t.id === id)
    if (!task) return [404, { error: { message: '读标任务不存在' } }]
    markMockReady(task)
    return [200, task]
  })

  mock.onPost(TASK_REPARSE_PATH).reply((config) => {
    const id = (config.url?.match(REL_TASK_REPARSE)?.[1] ?? '') as string
    const task = tenderReadTasks.find((t) => t.id === id)
    if (!task) return [404, { error: { message: '读标任务不存在' } }]
    markMockReady(task)
    return [200, task]
  })

  mock.onGet(TASK_OUTLINE_PATH).reply((config) => {
    const id = (config.url?.match(REL_TASK_OUTLINE)?.[1] ?? '') as string
    return id === tenderReadBaseline.taskId ? [200, tenderReadOutline] : [200, []]
  })

  mock.onGet(TASK_BASELINE_PATH).reply((config) => {
    const id = (config.url?.match(REL_TASK_BASELINE)?.[1] ?? '') as string
    return [200, id === tenderReadBaseline.taskId ? tenderReadBaseline : { taskId: id, baselineVersion: 1, fields: [] }]
  })

  mock.onPut(TASK_FIELD_PATH).reply((config) => {
    const id = (config.url?.match(REL_TASK_FIELD)?.[1] ?? '') as string
    const fieldId = (config.url?.match(REL_TASK_FIELD)?.[2] ?? '') as string
    if (id !== tenderReadBaseline.taskId) return [404, { error: { message: '读标任务不存在' } }]
    const field = tenderReadBaseline.fields.find((f) => f.id === fieldId)
    if (!field) return [404, { error: { message: '字段不存在' } }]
    const body = JSON.parse(config.data as string) as {
      valueJson?: string
      rawText?: string
      status?: 'confirmed' | 'edited'
      confidence?: number
    }
    field.valueJson = body.valueJson ?? field.valueJson
    if (body.rawText !== undefined) field.rawText = body.rawText
    if (body.status === 'confirmed') field.status = 'confirmed'
    if (body.status === 'edited') field.status = 'edited'
    if (body.confidence !== undefined) field.confidence = body.confidence
    return [200, field]
  })

  mock.onPost(TASK_RE_EXTRACT_PATH).reply((config) => {
    const id = (config.url?.match(REL_TASK_RE_EXTRACT)?.[1] ?? '') as string
    const task = tenderReadTasks.find((t) => t.id === id)
    if (!task) return [404, { error: { message: '读标任务不存在' } }]
    // 模拟后台重抽：返回抽取中快照，下一次轮询任务详情时落定 ready
    task.status = 'extracting'
    task.progressStage = 'extracting'
    task.progressPercent = 45
    reExtractingTaskIds.add(id)
    return [200, task]
  })

  mock.onGet(TASK_EXPORT_PATH).reply((config) => {
    const id = (config.url?.match(REL_TASK_EXPORT)?.[1] ?? '') as string
    return [200, id === tenderReadBaseline.taskId ? tenderReadBaseline : { taskId: id, baselineVersion: 1, fields: [] }]
  })
}

function markMockReady(task: TenderReadingTask): void {
  const nameField = tenderReadBaseline.fields.find(
    (f) => f.category === 'project_info' && f.fieldKey === 'name',
  )
  if (nameField) {
    try {
      const value = JSON.parse(nameField.valueJson) as { value?: string }
      if (value.value) task.name = value.value
    } catch {
      // 保持原名称
    }
  }
  task.status = 'ready'
  task.progressStage = 'ready'
  task.progressPercent = 100
  task.baselineVersion = 1
}
