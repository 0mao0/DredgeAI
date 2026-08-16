import { computed, ref } from 'vue'
import { message } from 'ant-design-vue'
import { previewStandard, uploadStandard } from '@/api/modules/standards'
import type { StandardPropertyInput } from '@/types'

export type UploadTaskStatus
  = | 'previewing'
    | 'ready'
    | 'preview_failed'
    | 'uploading'
    | 'uploaded'
    | 'upload_failed'

export interface StandardUploadTask {
  id: string
  file: File
  fileName: string
  status: UploadTaskStatus
  progress: number
  form: StandardPropertyInput
  error?: string
  standardId?: string
}

export const MAX_UPLOAD_FILES = 10
export const MAX_FILE_SIZE = 50 * 1024 * 1024

let taskSeq = 0

function nextTaskId(): string {
  taskSeq += 1
  return `upload-${Date.now()}-${taskSeq}`
}

export function useStandardUpload(onCompleted?: () => void) {
  const tasks = ref<StandardUploadTask[]>([])
  const timers = new Map<string, ReturnType<typeof setInterval>>()

  const runningCount = computed(() =>
    tasks.value.filter((t) => t.status === 'previewing' || t.status === 'uploading').length,
  )
  const hasTasks = computed(() => tasks.value.length > 0)

  function clearTimer(id: string): void {
    const timer = timers.get(id)
    if (timer) {
      clearInterval(timer)
      timers.delete(id)
    }
  }

  function startProgress(id: string, target = 90, step = 8): void {
    clearTimer(id)
    timers.set(id, setInterval(() => {
      const task = tasks.value.find((t) => t.id === id)
      if (!task) {
        clearTimer(id)
        return
      }
      if (task.progress >= target) {
        clearTimer(id)
        return
      }
      task.progress = Math.min(target, task.progress + step)
    }, 120))
  }

  function patchTask(id: string, patch: Partial<StandardUploadTask>): void {
    const task = tasks.value.find((t) => t.id === id)
    if (task) Object.assign(task, patch)
  }

  function addFiles(files: File[]): void {
    const validFiles = files.filter((file) => {
      if (!/\.pdf$/i.test(file.name)) {
        message.warning(`「${file.name}」仅支持 PDF 文件`)
        return false
      }
      if (file.size > MAX_FILE_SIZE) {
        message.warning(`「${file.name}」超过 50MB 限制`)
        return false
      }
      return true
    })
    const activeCount = tasks.value.filter((t) => t.status !== 'uploaded').length
    const remaining = MAX_UPLOAD_FILES - activeCount
    if (remaining <= 0) {
      message.warning(`单批最多上传 ${MAX_UPLOAD_FILES} 个文件`)
      return
    }
    const accepted = validFiles.slice(0, remaining)
    if (accepted.length < validFiles.length) {
      message.warning(`单批最多上传 ${MAX_UPLOAD_FILES} 个文件，已保留前 ${accepted.length} 个`)
    }
    for (const file of accepted) {
      const task: StandardUploadTask = {
        id: nextTaskId(),
        file,
        fileName: file.name,
        status: 'previewing',
        progress: 0,
        form: { name: '', code: '' },
      }
      tasks.value.push(task)
      void runPreview(task.id)
    }
  }

  async function runPreview(id: string): Promise<void> {
    const task = tasks.value.find((t) => t.id === id)
    if (!task) return
    patchTask(id, { status: 'previewing', progress: 0, error: undefined })
    startProgress(id)
    try {
      const form = await previewStandard(task.file)
      patchTask(id, { status: 'ready', progress: 100, form })
      clearTimer(id)
    } catch {
      patchTask(id, { status: 'preview_failed', error: 'AI 预读失败，请重试' })
      clearTimer(id)
    }
  }

  async function uploadOne(id: string): Promise<void> {
    const task = tasks.value.find((t) => t.id === id)
    if (!task) return
    patchTask(id, { status: 'uploading', progress: 0, error: undefined })
    startProgress(id, 90, 6)
    try {
      const record = await uploadStandard(task.file, task.form)
      patchTask(id, { status: 'uploaded', progress: 100, standardId: record.id })
      clearTimer(id)
    } catch {
      patchTask(id, { status: 'upload_failed', error: '上传失败，请重试' })
      clearTimer(id)
    }
  }

  async function submitUploads(): Promise<void> {
    const invalid = tasks.value.filter(
      (t) => t.status === 'ready' && (!t.form.name?.trim() || !t.form.code?.trim()),
    )
    if (invalid.length) {
      message.warning(`有 ${invalid.length} 个文件未填写名称/编号，请补充后重试`)
      return
    }
    const pending = tasks.value.filter((t) => t.status === 'ready')
    if (!pending.length) return
    await Promise.all(pending.map((t) => uploadOne(t.id)))
    const pendingIds = new Set(pending.map((t) => t.id))
    const successCount = tasks.value.filter((t) => pendingIds.has(t.id) && t.status === 'uploaded').length
    const failedCount = tasks.value.filter((t) => pendingIds.has(t.id) && t.status === 'upload_failed').length
    if (successCount) message.success(`上传完成 ${successCount} 个文件`)
    if (failedCount) message.error(`${failedCount} 个文件上传失败，可在“上传任务”中重试`)
    onCompleted?.()
  }

  function retryTask(id: string): void {
    const task = tasks.value.find((t) => t.id === id)
    if (!task) return
    if (task.status === 'preview_failed') void runPreview(id)
    if (task.status === 'upload_failed') void uploadOne(id)
  }

  function removeTask(id: string): void {
    const task = tasks.value.find((t) => t.id === id)
    if (!task) return
    if (task.status === 'uploading') return
    clearTimer(id)
    tasks.value = tasks.value.filter((t) => t.id !== id)
  }

  function updateForm(id: string, form: StandardPropertyInput): void {
    patchTask(id, { form })
  }

  function dispose(): void {
    timers.forEach((timer) => clearInterval(timer))
    timers.clear()
  }

  return {
    tasks,
    runningCount,
    hasTasks,
    addFiles,
    removeTask,
    retryTask,
    submitUploads,
    updateForm,
    dispose,
  }
}
