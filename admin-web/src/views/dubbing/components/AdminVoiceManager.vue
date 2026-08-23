<template>
  <div class="admin-voice-manager">
    <DataTable
      v-model:query="query"
      :columns="columns"
      :data-source="voices"
      :loading="loading"
      row-key="id"
      :pagination="{ pageSize: 15 }"
      :filters="filters"
      empty-text="暂无音色数据"
    >
      <template #toolbarExtra>
        <AppButton variant="primary" @click="showAddModal = true">
          <template #icon><PlusOutlined /></template>
          添加公有音色
        </AppButton>
      </template>
      <template #bodyCell="{ column, record }: { column: { key: string }; record: VoiceItem }">
        <template v-if="column.key === 'gender'">
          <span
            class="voice-gender"
            :class="{
              'voice-gender--male': record.gender === '男声',
              'voice-gender--female': record.gender === '女声',
              'voice-gender--child': record.gender === '童声',
            }"
          >
            <ManOutlined v-if="record.gender === '男声'" />
            <WomanOutlined v-else-if="record.gender === '女声'" />
            <SmileOutlined v-else />
          </span>
        </template>
        <template v-if="column.key === 'visibility'">
          <a-tag :color="record.visibility === 'public' ? 'green' : 'purple'">
            {{ record.visibility === 'public' ? '公有' : '私有' }}
          </a-tag>
        </template>
        <template v-if="column.key === 'user'">
          {{ record.userName || '-' }}
        </template>
        <template v-if="column.key === 'deletedByUser'">
          <a-tag :color="record.deletedByUser ? 'red' : 'green'">
            {{ record.deletedByUser ? '用户已删除' : '保留中' }}
          </a-tag>
        </template>
        <template v-if="column.key === 'createdAt'">
          {{ formatTime(record.createdAt) }}
        </template>
        <template v-if="column.key === 'action'">
          <a-tooltip :title="record.sampleUrl ? '试听' : '暂无样本'">
            <AppButton variant="link" size="sm" :disabled="!record.sampleUrl" @click="playSample(record)">
              <CustomerServiceOutlined v-if="playingId !== record.id" />
              <LoadingOutlined v-else spin />
            </AppButton>
          </a-tooltip>
          <a-divider type="vertical" />
          <a-popconfirm
            v-if="record.visibility === 'public'"
            title="确定删除此公有音色？"
            @confirm="handleDelete(record.id)"
          >
            <AppButton variant="link" danger size="sm">删除</AppButton>
          </a-popconfirm>
          <a-tooltip v-else-if="!record.deletedByUser" title="用户未删除，受隐私限制不可彻底删除">
            <AppButton variant="link" danger size="sm" disabled>删除</AppButton>
          </a-tooltip>
          <a-popconfirm v-else title="用户已删除，确定彻底移除此音色？" @confirm="handleDelete(record.id)">
            <AppButton variant="link" danger size="sm">删除</AppButton>
          </a-popconfirm>
        </template>
      </template>
    </DataTable>

    <VoiceRegisterModal
      v-model:open="showAddModal"
      title="添加公有音色"
      @confirmed="handleVoiceConfirmed"
    />

    <!-- Hidden audio element for sample playback -->
    <audio ref="audioRef" @ended="playingId = null" @error="playingId = null" />
  </div>
</template>

<script setup lang="ts">
import { AppButton, DataTable, VoiceRegisterModal } from '@shared/web'
import type { DataTableColumn, DataTableFilter } from '@shared/web'
import { ref, watch } from 'vue'
import {
  PlusOutlined,
  ManOutlined,
  WomanOutlined,
  SmileOutlined,
  CustomerServiceOutlined,
  LoadingOutlined,
} from '@ant-design/icons-vue'
import { message } from 'ant-design-vue'
import type { VoiceItem } from '@/types'

defineProps<{ voices: VoiceItem[], loading?: boolean }>()

const emit = defineEmits<{
  search: [params: { keyword: string, deletedOnly: boolean }]
  create: [formData: FormData]
  delete: [id: string]
}>()

const columns: DataTableColumn[] = [
  { title: '音色名称', dataIndex: 'name', key: 'name', width: 180, minWidth: 140, resizable: true },
  { title: '性别', key: 'gender', width: 60, minWidth: 60, resizable: true },
  { title: '公有/私有', key: 'visibility', width: 100, minWidth: 90, resizable: true },
  { title: '所属用户', key: 'user', width: 100, minWidth: 90, resizable: true },
  { title: '用户删除状态', key: 'deletedByUser', width: 140, minWidth: 110, resizable: true },
  { title: '创建时间', key: 'createdAt', width: 160, minWidth: 140, resizable: true },
  { title: '操作', key: 'action', width: 150, minWidth: 150, fixed: 'right', resizable: true },
]

const query = ref({ keyword: '', deletedOnly: false })
const filters: DataTableFilter[] = [
  { key: 'keyword', type: 'input', placeholder: '搜索音色名称 / 用户', width: 240 },
  { key: 'deletedOnly', type: 'switch', label: '仅看用户已删除', defaultValue: false },
]
const showAddModal = ref(false)
const playingId = ref<string | null>(null)
const audioRef = ref<HTMLAudioElement>()

watch(query, () => {
  emit('search', { keyword: query.value.keyword.trim(), deletedOnly: query.value.deletedOnly })
}, { deep: true })

function playSample(voice: VoiceItem): void {
  if (!voice.sampleUrl || playingId.value) return
  playingId.value = voice.id
  if (audioRef.value) {
    audioRef.value.src = voice.sampleUrl
    audioRef.value.play().catch(() => {
      playingId.value = null
      message.warning('试听失败，样本文件不可用')
    })
  }
}

function handleVoiceConfirmed(payload: { voice: VoiceItem, formData: FormData }): void {
  emit('create', payload.formData)
}

function handleDelete(id: string): void {
  emit('delete', id)
}

function formatTime(iso?: string): string {
  if (!iso) return '-'
  const d = new Date(iso)
  const y = d.getFullYear()
  const mo = String(d.getMonth() + 1).padStart(2, '0')
  const da = String(d.getDate()).padStart(2, '0')
  const h = String(d.getHours()).padStart(2, '0')
  const mi = String(d.getMinutes()).padStart(2, '0')
  return `${y}-${mo}-${da} ${h}:${mi}`
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.admin-voice-manager :deep(.ant-table-cell) {
  font-size: @font-size-sm;
}

.voice-gender {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 24px;
  height: 24px;
  border-radius: 50%;
  font-size: 14px;
  &--male { color: @voice-gender-male; background: color-mix(in srgb, @voice-gender-male 12%, transparent); }
  &--female { color: @voice-gender-female; background: color-mix(in srgb, @voice-gender-female 12%, transparent); }
  &--child { color: @voice-gender-child; background: color-mix(in srgb, @voice-gender-child 12%, transparent); }
}
</style>
