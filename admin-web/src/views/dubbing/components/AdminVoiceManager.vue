<template>
  <div class="admin-voice-manager">
    <div class="admin-voice-manager__toolbar">
      <a-button type="primary" @click="showAddModal = true">
        <template #icon><PlusOutlined /></template>
        添加公有音色
      </a-button>
      <a-input-search
        v-model:value="query"
        placeholder="搜索音色名称 / 用户"
        allow-clear
        style="width:240px"
        @search="fetchVoices"
        @change="fetchVoices"
      />
      <div class="toolbar-spacer" />
      <div class="toolbar-item">
        <span class="toolbar-label">仅看用户已删除</span>
        <a-switch v-model:checked="deletedOnly" @change="fetchVoices" />
      </div>
    </div>

    <a-table
      :data-source="voices"
      :columns="columns"
      :loading="loading"
      row-key="id"
      :pagination="{ pageSize: 15, showSizeChanger: false, showTotal: (t: number) => `共 ${t} 条` }"
      size="middle"
      class="admin-voice-manager__table"
      :locale="{ emptyText: '暂无音色数据' }"
    >
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
            <a-button type="link" size="small" :disabled="!record.sampleUrl" @click="playSample(record)">
              <CustomerServiceOutlined v-if="playingId !== record.id" />
              <LoadingOutlined v-else spin />
            </a-button>
          </a-tooltip>
          <a-divider type="vertical" />
          <a-popconfirm
            v-if="record.visibility === 'public'"
            title="确定删除此公有音色？"
            @confirm="handleDelete(record.id)"
          >
            <a-button type="link" danger size="small">删除</a-button>
          </a-popconfirm>
          <a-tooltip v-else-if="!record.deletedByUser" title="用户未删除，受隐私限制不可彻底删除">
            <a-button type="link" danger size="small" disabled>删除</a-button>
          </a-tooltip>
          <a-popconfirm v-else title="用户已删除，确定彻底移除此音色？" @confirm="handleDelete(record.id)">
            <a-button type="link" danger size="small">删除</a-button>
          </a-popconfirm>
        </template>
      </template>
    </a-table>

    <a-modal
      v-model:open="showAddModal"
      title="添加公有音色"
      :confirm-loading="submitting"
      @ok="handleAdd"
      @cancel="resetForm"
    >
      <a-form :model="form" layout="vertical">
        <a-form-item label="音色名称" required>
          <a-input v-model:value="form.name" placeholder="如：知远·男声" maxlength="20" />
        </a-form-item>
        <a-form-item label="性别" required>
          <a-radio-group v-model:value="form.gender" button-style="solid">
            <a-radio-button value="男声">男声</a-radio-button>
            <a-radio-button value="女声">女声</a-radio-button>
            <a-radio-button value="童声">童声</a-radio-button>
          </a-radio-group>
        </a-form-item>
      </a-form>
    </a-modal>

    <!-- Hidden audio element for sample playback -->
    <audio ref="audioRef" @ended="playingId = null" @error="playingId = null" />
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
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
import request from '@/api/request'
import { urls } from '@shared/core/api'

const columns = [
  { title: '音色名称', dataIndex: 'name', key: 'name', width: 180 },
  { title: '性别', key: 'gender', width: 60, align: 'center' },
  { title: '公有/私有', key: 'visibility', width: 100, align: 'center' },
  { title: '所属用户', key: 'user', width: 100, align: 'center' },
  { title: '用户删除状态', key: 'deletedByUser', width: 140, align: 'center' },
  { title: '创建时间', key: 'createdAt', width: 160, align: 'center' },
  { title: '操作', key: 'action', width: 150, align: 'center', fixed: 'right' },
]

const voices = ref<VoiceItem[]>([])
const loading = ref(false)
const query = ref('')
const deletedOnly = ref(false)
const showAddModal = ref(false)
const submitting = ref(false)
const playingId = ref<string | null>(null)
const audioRef = ref<HTMLAudioElement>()

const form = ref({
  name: '',
  gender: '男声' as '男声' | '女声' | '童声',
})

async function fetchVoices(): Promise<void> {
  loading.value = true
  try {
    const params: Record<string, string | number> = {}
    const q = query.value.trim()
    if (q) params.keyword = q
    if (deletedOnly.value) params.deletedOnly = 1
    const res = await request.get<any>(urls.adminVoices, { params })
    voices.value = (res?.data || res || []) as VoiceItem[]
  } catch {
    message.error('加载音色列表失败')
  } finally {
    loading.value = false
  }
}

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

async function handleAdd(): Promise<void> {
  if (!form.value.name.trim()) {
    message.warning('请输入音色名称')
    return
  }
  submitting.value = true
  try {
    await request.post(urls.adminVoices, {
      name: form.value.name.trim(),
      gender: form.value.gender,
    })
    message.success('公有音色已添加')
    showAddModal.value = false
    resetForm()
    fetchVoices()
  } catch {
    message.error('添加失败')
  } finally {
    submitting.value = false
  }
}

async function handleDelete(id: string): Promise<void> {
  try {
    await request.delete(`${urls.adminVoices}/${id}`)
    message.success('已删除')
    fetchVoices()
  } catch {
    message.error('删除失败')
  }
}

function resetForm(): void {
  form.value = { name: '', gender: '男声' }
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

onMounted(fetchVoices)
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.admin-voice-manager {
  &__toolbar {
    display: flex;
    gap: 16px;
    flex-wrap: wrap;
    align-items: center;
    margin-bottom: 16px;
  }
  &__table {
    :deep(.ant-table-cell) {
      font-size: @font-size-sm;
    }
  }
}

.toolbar-spacer {
  flex: 1 1 auto;
}
.toolbar-item {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-left: auto;
}
.toolbar-label {
  font-size: 13px;
  white-space: nowrap;
}

.voice-gender {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 24px;
  height: 24px;
  border-radius: 50%;
  font-size: 14px;
  &--male { color: #2563EB; background: color-mix(in srgb, #2563EB 12%, transparent); }
  &--female { color: #DB2777; background: color-mix(in srgb, #DB2777 12%, transparent); }
  &--child { color: #D97706; background: color-mix(in srgb, #D97706 12%, transparent); }
}
</style>
