<template>
  <a-drawer
    :open="open"
    :title="isEditing ? '编辑项目' : '新建项目'"
    placement="right"
    :width="560"
    @close="onClose"
  >
    <div class="project-create">
      <a-form layout="vertical">
        <a-form-item label="项目名称">
          <a-input
            v-model:value="name"
            placeholder="如：XX 地块基坑支护项目"
            :disabled="busy"
          />
          <div class="project-create__name-hint">
            <a-spin v-if="nameExtracting" size="small" />
            <span v-if="nameExtracting">正在从施工方案提取项目名称…</span>
            <span v-else-if="autoFilled" class="is-auto">已自动提取项目名称，可修改</span>
          </div>
        </a-form-item>

        <a-form-item label="施工方案">
          <a-upload-dragger
            multiple
            accept=".pdf,.doc,.docx"
            :show-upload-list="false"
            :before-upload="onBeforeUpload"
            :disabled="busy"
          >
            <p class="ant-upload-drag-icon">
              <InboxOutlined />
            </p>
            <p class="ant-upload-text">点击或拖拽 PDF/Word 施工方案到此处（可多份）</p>
            <p class="ant-upload-hint">上传后自动解析入知识库，并提取项目名称与主要内容</p>
          </a-upload-dragger>

          <div v-if="planItems.length" class="project-create__list">
            <div
              v-for="item in planItems"
              :key="item.key"
              class="project-create__file-item"
              :class="{ 'is-previewable': canPreview(item) }"
              @click="onPreviewItem(item)"
            >
              <UploadFileRow
                :item="item"
                :disabled="busy"
                @retry="onRetry(item.key)"
                @remove="onRemove(item.key)"
              />
            </div>
          </div>
        </a-form-item>
      </a-form>

      <a-alert v-if="error" type="error" show-icon :message="error" class="project-create__alert" />

      <div class="project-create__actions">
        <a-popconfirm
          v-if="isEditing"
          title="删除项目和全部方案文件？"
          ok-text="删除"
          cancel-text="取消"
          @confirm="onDeleteProject"
        >
          <AppButton variant="danger" :loading="deleting" :disabled="busy">删除项目</AppButton>
        </a-popconfirm>
        <AppButton :disabled="busy" @click="onClose">取消</AppButton>
        <AppButton variant="primary" :loading="busy" :disabled="!canSubmit" @click="onSubmit">
          {{ isEditing ? '保存项目' : '创建项目' }}
        </AppButton>
      </div>
    </div>
  </a-drawer>

  <a-drawer
    v-model:open="previewOpen"
    title="施工方案预览"
    placement="right"
    :width="900"
    :content-wrapper-style="{ right: '560px' }"
    :root-style="{ zIndex: 1001 }"
    :mask="false"
    :footer="null"
    @close="onPreviewClose"
  >
    <a-spin :spinning="previewLoading">
      <div class="project-preview">
        <StandardPdfViewer
          v-if="previewFileUrl"
          :file-url="previewFileUrl"
          :parsed-content="previewMarkdown"
          empty-title="暂无 PDF 预览"
        />
      </div>
    </a-spin>
  </a-drawer>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { InboxOutlined } from '@ant-design/icons-vue'
import AppButton from '@shared/web/components/AppButton.vue'
import { StandardPdfViewer, UploadFileRow } from '@shared/web'
import type { MeetingProjectDto, UploadFileItem } from '@/types'
import {
  createMeetingProject,
  deleteMeetingProject,
  extractMeetingProject,
  getMeetingProject,
  getMeetingProjectDocumentContent,
  getKnowledgeDocumentStatus,
  meetingProjectDocumentFileUrl,
  suggestMeetingProjectName,
  updateMeetingProject,
  uploadKnowledgeDocument,
} from '@/api/modules/aiMeeting'
import { extractErrorMessage } from '@/utils/audioToWav'

const props = defineProps<{ open: boolean, project?: MeetingProjectDto | null }>()
const emit = defineEmits<{
  'update:open': [open: boolean]
  'created': [project: MeetingProjectDto]
  'deleted': [id: string]
}>()

const name = ref('')
const planItems = ref<UploadFileItem[]>([])
const nameExtracting = ref(false)
const autoFilled = ref(false)
const busy = ref(false)
const deleting = ref(false)
const error = ref('')
const previewOpen = ref(false)
const previewDocId = ref('')
const previewMarkdown = ref('')
const previewLoading = ref(false)
const isEditing = computed(() => Boolean(props.project))
const previewFileUrl = computed(() =>
  props.project && previewDocId.value
    ? meetingProjectDocumentFileUrl(props.project.id, previewDocId.value)
    : '',
)

const uploadedCount = computed(() => planItems.value.filter((i) => i.docId).length)
const canSubmit = computed(() => {
  if (busy.value) return false
  return isEditing.value ? true : uploadedCount.value > 0
})

watch(
  () => props.open,
  (open) => {
    if (!open) return
    if (props.project) {
      loadProject(props.project)
    } else {
      reset()
    }
  },
  { immediate: true },
)

async function onBeforeUpload(file: File): Promise<boolean> {
  const item: UploadFileItem = {
    key: `plan-${Date.now()}-${Math.random().toString(36).slice(2, 7)}`,
    name: file.name,
    size: file.size,
    file,
    role: 'bid',
    status: 'uploading',
    percent: 0,
  }
  planItems.value.push(item)
  // 必须用响应式数组中的代理对象做后续变更，直接改原始对象不会触发重新渲染
  void uploadPlan(planItems.value[planItems.value.length - 1]!)
  return false
}

async function uploadPlan(item: UploadFileItem): Promise<void> {
  error.value = ''
  item.status = 'uploading'
  item.percent = 0
  item.error = undefined
  item.warning = undefined
  try {
    const result = await uploadKnowledgeDocument(item.file, (percent) => {
      item.percent = percent
    })
    item.docId = result.docId
    // 文件上传完成即可删除；解析状态用行内提示展示
    item.status = 'done'
    item.percent = undefined
    item.warning = '正在解析… 0%'
    if (result.status?.state === 'succeeded') {
      item.warning = '已解析入库'
      onParsed(item)
      return
    }
    await pollStatus(item, result.docId)
  } catch (err) {
    item.status = 'error'
    item.error = `上传失败：${extractErrorMessage(err)}`
  }
}

async function pollStatus(item: UploadFileItem, id: string, tries = 60): Promise<void> {
  const status = await getKnowledgeDocumentStatus(id)
  if (status.state === 'succeeded') {
    item.warning = '已解析入库'
    onParsed(item)
    return
  }
  if (status.state === 'failed' || status.state === 'partial') {
    item.status = 'error'
    item.error = status.stageMessage ?? '解析失败，可点击重试'
    item.warning = undefined
    return
  }
  item.warning = `正在解析… ${status.progress ?? 0}%`
  if (tries <= 0) {
    item.status = 'error'
    item.error = '解析超时，可点击重试'
    item.warning = undefined
    return
  }
  await new Promise((resolve) => setTimeout(resolve, 2000))
  await pollStatus(item, id, tries - 1)
}

function onParsed(item: UploadFileItem): void {
  // 从第一份解析完成的方案自动提取项目名称
  if (!name.value.trim() && item.docId) {
    void autoFillName(item.docId)
  }
}

async function autoFillName(docId: string): Promise<void> {
  nameExtracting.value = true
  try {
    const result = await suggestMeetingProjectName(docId)
    if (result.name?.trim()) {
      name.value = result.name.trim()
      autoFilled.value = true
    }
  } catch {
    // 提取失败不阻塞，用户可手动填写
  } finally {
    nameExtracting.value = false
  }
}

function onRetry(key: string): void {
  const item = planItems.value.find((i) => i.key === key)
  if (item) void uploadPlan(item)
}

function onRemove(key: string): void {
  planItems.value = planItems.value.filter((i) => i.key !== key)
}

function canPreview(item: UploadFileItem): boolean {
  return Boolean(item.docId && item.status === 'done')
}

function onPreviewItem(item: UploadFileItem): void {
  if (!canPreview(item)) return
  previewDocId.value = item.docId!
  previewMarkdown.value = ''
  previewOpen.value = true
  void loadPreview()
}

async function loadPreview(): Promise<void> {
  if (!props.project || !previewDocId.value) return
  previewLoading.value = true
  try {
    previewMarkdown.value = await getMeetingProjectDocumentContent(
      props.project.id,
      previewDocId.value,
    )
  } catch {
    previewMarkdown.value = ''
  } finally {
    previewLoading.value = false
  }
}

function onPreviewClose(): void {
  previewDocId.value = ''
  previewMarkdown.value = ''
}

async function onSubmit(): Promise<void> {
  if (!canSubmit.value) return
  busy.value = true
  error.value = ''
  try {
    const docIds = planItems.value
      .filter((i) => i.docId)
      .map((i) => i.docId!)
    const docNames = planItems.value
      .filter((i) => i.docId)
      .map((i) => i.name)
    if (isEditing.value && props.project) {
      const project = await updateMeetingProject(props.project.id, {
        name: name.value.trim(),
        docIds,
        docNames,
      })
      emit('created', project)
    } else {
      const project = await createMeetingProject({
        name: name.value.trim(),
        docId: docIds[0] ?? '',
        docIds,
        docNames,
      })
      // 提取项目信息与主要内容（失败不阻塞创建，可后续重试）
      try {
        await extractMeetingProject(project.id)
      } catch {
        // 提取失败时项目仍可用，仅缺少摘要
      }
      const refreshed = await getMeetingProject(project.id)
      emit('created', refreshed)
    }
    emit('update:open', false)
  } catch (err) {
    error.value = `创建失败：${extractErrorMessage(err)}`
  } finally {
    busy.value = false
  }
}

async function onDeleteProject(): Promise<void> {
  if (!props.project) return
  deleting.value = true
  error.value = ''
  try {
    await deleteMeetingProject(props.project.id)
    emit('deleted', props.project.id)
    emit('update:open', false)
  } catch (err) {
    error.value = `删除失败：${extractErrorMessage(err)}`
  } finally {
    deleting.value = false
  }
}

function loadProject(project: MeetingProjectDto): void {
  name.value = project.name
  planItems.value = project.docIds.map((docId, index) =>
    existingItem(docId, index, project.docNames[index] ?? ''),
  )
  nameExtracting.value = false
  autoFilled.value = false
  busy.value = false
  deleting.value = false
  error.value = ''
}

function existingItem(docId: string, index: number, docName = ''): UploadFileItem {
  const label = docName || `施工方案 ${index + 1}`
  return {
    key: `doc-${docId}`,
    name: label,
    size: 0,
    file: new File([], label),
    role: 'bid',
    status: 'done',
    docId,
    warning: '已入库',
  }
}

function reset(): void {
  name.value = ''
  planItems.value = []
  nameExtracting.value = false
  autoFilled.value = false
  busy.value = false
  deleting.value = false
  error.value = ''
}

function onClose(): void {
  emit('update:open', false)
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.project-create {
  display: flex;
  flex-direction: column;
  gap: @spacing-lg;
}
.project-create__name-hint {
  display: flex;
  align-items: center;
  gap: @spacing-xs;
  min-height: 20px;
  margin-top: @spacing-xs;
  font-size: @font-size-xs;
  color: @text-tertiary;

  .is-auto {
    color: @success;
  }
}
.project-create__list {
  display: flex;
  flex-direction: column;
  gap: @spacing-xs;
  margin-top: @spacing-sm;
}
.project-create__file-item {
  border-radius: @radius-base;
  transition: box-shadow @transition-fast;

  &.is-previewable {
    cursor: pointer;

    &:hover {
      box-shadow: @shadow-sm;
    }
  }
}
.project-create__alert {
  margin-bottom: 0;
}
.project-preview {
  height: calc(100vh - 140px);
  min-height: 0;
}
.project-create__actions {
  display: flex;
  gap: @spacing-sm;

  > * {
    flex: 1;
  }
}
</style>
