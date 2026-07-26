<template>
  <div class="voice-picker" :class="{ 'voice-picker--disabled': disabled }">
    <a-skeleton v-if="loading" active :paragraph="{ rows: 3 }" :title="false" />

    <template v-else>
      <a-input-search
        v-model:value="query"
        placeholder="搜索音色名称 / 风格"
        allow-clear
        style="width:100%"
        class="voice-picker__search"
      />
      <ul class="voice-list">
        <li
          v-for="voice in filteredVoices"
          :key="voice.id"
          class="voice-item"
          :class="{ 'voice-item--selected': voice.id === modelValue }"
          @click="disabled ? null : selectVoice(voice.id)"
        >
          <span
            class="voice-item__gender"
            :class="{
              'voice-item__gender--male': voice.gender === '男声',
              'voice-item__gender--female': voice.gender === '女声',
              'voice-item__gender--child': voice.gender === '童声',
            }"
          >
            <ManOutlined v-if="voice.gender === '男声'" />
            <WomanOutlined v-else-if="voice.gender === '女声'" />
            <SmileOutlined v-else />
          </span>
          <div class="voice-item__info">
            <span class="voice-item__name">{{ voice.name }}</span>
            <span class="voice-item__style">{{ voice.style }}</span>
          </div>
          <a-tooltip
            :title="voice.visibility === 'private' ? '个人音色' : '公有音色'"
          >
            <span
              class="voice-item__visibility"
              :class="`voice-item__visibility--${voice.visibility}`"
            >
              <GlobalOutlined v-if="voice.visibility === 'public'" />
              <UserOutlined v-else />
            </span>
          </a-tooltip>

          <!-- 上传状态：转换中 -->
          <a-tooltip v-if="voice.uploadStatus === 'converting'" title="转换中...">
            <span class="voice-item__status voice-item__status--converting">
              <LoadingOutlined spin />
            </span>
          </a-tooltip>

          <!-- 上传状态：失败 -->
          <a-tooltip v-else-if="voice.uploadStatus === 'failed'" title="转换失败">
            <span class="voice-item__status voice-item__status--failed" @click.stop="emit('showFailDetail', voice)">
              <CloseCircleFilled />
            </span>
          </a-tooltip>

          <!-- 试听按钮（就绪 / 公有音色） -->
          <a-tooltip v-else :title="loadingVoiceId === voice.id ? '生成中...' : (playingId === voice.id ? '停止试听' : '试听')">
            <span class="voice-item__play" @click.stop="togglePlay(voice)">
              <LoadingOutlined v-if="loadingVoiceId === voice.id" spin />
              <CustomerServiceOutlined v-else-if="playingId !== voice.id" />
              <PauseCircleOutlined v-else />
            </span>
          </a-tooltip>

          <a-popconfirm
            v-if="voice.visibility === 'private'"
            title="确定删除此音色？"
            placement="left"
            @confirm="emit('deleteVoice', voice.id)"
          >
            <span class="voice-item__delete">
              <DeleteOutlined />
            </span>
          </a-popconfirm>
        </li>
      </ul>
      <EmptyState v-if="filteredVoices.length === 0" type="no-result" title="未找到音色" />
    </template>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onUnmounted } from 'vue'
import {
  CustomerServiceOutlined,
  PauseCircleOutlined,
  LoadingOutlined,
  ManOutlined,
  WomanOutlined,
  SmileOutlined,
  GlobalOutlined,
  UserOutlined,
  DeleteOutlined,
  CloseCircleFilled,
} from '@ant-design/icons-vue'
import { message } from 'ant-design-vue'
import EmptyState from '@shared/web/components/EmptyState.vue'
import type { VoiceItem } from '@/types'
import { generateDubbing } from '@/api/modules/dubbing'

const props = defineProps<{
  voices: VoiceItem[]
  modelValue: string
  loading?: boolean
  disabled?: boolean
}>()

const emit = defineEmits<{
  'update:modelValue': [value: string]
  'deleteVoice': [voiceId: string]
  'showFailDetail': [voice: VoiceItem]
}>()

const query = ref('')
const playingId = ref<string | null>(null)
const loadingVoiceId = ref<string | null>(null)
const objectURLs = new Set<string>()
let audioEl: HTMLAudioElement | null = null

const preferredOrder: string[] = ['通用', '播音', '客服', '解说', '方言', '儿童']

const filteredVoices = computed(() => {
  const q = query.value.trim().toLowerCase()
  const sorted = [...props.voices].sort((a, b) => {
    if (a.visibility !== b.visibility) {
      return a.visibility === 'private' ? -1 : 1
    }
    const ta = a.createdAt ? new Date(a.createdAt).getTime() : 0
    const tb = b.createdAt ? new Date(b.createdAt).getTime() : 0
    if (ta !== tb) return tb - ta
    const ia = preferredOrder.indexOf(a.category || '未分类')
    const ib = preferredOrder.indexOf(b.category || '未分类')
    return (ia === -1 ? 99 : ia) - (ib === -1 ? 99 : ib)
  })
  if (!q) return sorted
  return sorted.filter(
    (v) =>
      v.name.toLowerCase().includes(q)
      || (v.style || '').toLowerCase().includes(q)
      || (v.category || '').toLowerCase().includes(q),
  )
})

function selectVoice(id: string): void {
  emit('update:modelValue', id)
}

async function togglePlay(voice: VoiceItem): Promise<void> {
  if (loadingVoiceId.value) return

  if (playingId.value === voice.id) {
    audioEl?.pause()
    audioEl = null
    playingId.value = null
    return
  }

  if (audioEl) {
    audioEl.pause()
    audioEl = null
  }

  if (voice.sampleUrl) {
    audioEl = new Audio(voice.sampleUrl)
    audioEl.addEventListener('ended', () => { playingId.value = null; audioEl = null })
    audioEl.addEventListener('error', () => { audioEl = null; doLiveGenerate(voice) })
    audioEl.play()
    playingId.value = voice.id
    return
  }

  await doLiveGenerate(voice)
}

async function doLiveGenerate(voice: VoiceItem): Promise<void> {
  loadingVoiceId.value = voice.id
  try {
    const blob = await generateDubbing('这是一段试听语音。', voice.id, 1.0)
    if (!blob || blob.size === 0) throw new Error('empty audio')
    const url = URL.createObjectURL(blob)
    objectURLs.add(url)
    audioEl = new Audio(url)
    audioEl.addEventListener('ended', () => { playingId.value = null; audioEl = null })
    audioEl.play()
    playingId.value = voice.id
  } catch {
    message.warning('试听生成失败，请稍后重试')
  } finally {
    loadingVoiceId.value = null
  }
}

onUnmounted(() => {
  if (audioEl) {
    audioEl.pause()
    audioEl = null
  }
  playingId.value = null
  for (const url of objectURLs) {
    URL.revokeObjectURL(url)
  }
  objectURLs.clear()
})
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.voice-picker__search {
  margin-bottom: @spacing-md;
}

.voice-picker--disabled .voice-item { cursor: default; opacity: 0.55; }
.voice-picker--disabled .voice-item--selected { opacity: 1; }

.voice-list {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.voice-item {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 8px 10px;
  border: 1px solid @border-color;
  border-radius: @radius-base;
  background: @card-bg;
  cursor: pointer;
  transition: border-color 0.2s ease, background 0.2s ease, box-shadow 0.2s ease;
  &:hover {
    border-color: @brand-primary;
    box-shadow: @shadow-sm;
  }
  &--selected {
    border-color: @brand-primary;
    background: color-mix(in srgb, @brand-primary 6%, transparent);
    box-shadow: 0 0 0 2px color-mix(in srgb, @brand-primary 22%, transparent);
  }
  &__gender {
    flex-shrink: 0;
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
  &__info {
    flex: 1;
    min-width: 0;
    display: flex;
    flex-direction: column;
    gap: 1px;
  }
  &__name {
    font-size: @font-size-sm;
    color: @text-primary;
    line-height: 1.3;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }
  &__style {
    font-size: @font-size-xs;
    color: @text-tertiary;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }
  &__visibility {
    flex-shrink: 0;
    display: inline-flex;
    align-items: center;
    justify-content: center;
    width: 20px;
    height: 20px;
    font-size: 13px;
    &--public { color: @success; }
    &--private { color: @accent; }
  }
  &__status {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    width: 28px;
    height: 28px;
    font-size: 16px;
    border-radius: 50%;
    flex-shrink: 0;
    cursor: default;
    &--converting { color: @brand-primary; }
    &--failed {
      color: @danger;
      cursor: pointer;
      &:hover { color: #fff; background: @danger; }
    }
  }
  &__play {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    width: 28px;
    height: 28px;
    color: @brand-primary;
    font-size: 16px;
    cursor: pointer;
    border-radius: 50%;
    transition: color 0.2s ease, background 0.2s ease;
    flex-shrink: 0;
    &:hover { color: #fff; background: @brand-primary; }
  }
  &__delete {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    width: 24px;
    height: 24px;
    font-size: 14px;
    color: @danger;
    cursor: pointer;
    border-radius: 50%;
    transition: color 0.2s ease, background 0.2s ease;
    flex-shrink: 0;
    &:hover { color: #fff; background: @danger; }
  }
}

@media (prefers-reduced-motion: reduce) {
  .voice-item { transition: border-color 0.2s ease, background 0.2s ease; }
  .voice-item__play { transition: none; }
}
</style>
