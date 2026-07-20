<template>
  <div class="voice-picker">
    <a-skeleton v-if="loading" active :paragraph="{ rows: 3 }" :title="false" />

    <template v-else>
      <a-input-search
        v-model:value="query"
        placeholder="搜索音色名称 / 风格 / 厂商"
        allow-clear
        class="voice-picker__search"
      />
      <a-tabs v-model:activeKey="activeCategory" @change="handleTabChange">
        <a-tab-pane v-for="cat in categories" :key="cat" :tab="cat">
          <div class="voice-grid">
            <div
              v-for="voice in filteredVoices"
              :key="voice.id"
              class="voice-card"
              :class="{ 'voice-card--selected': voice.id === modelValue }"
              @click="selectVoice(voice.id)"
            >
            <div class="voice-card__row voice-card__row--top">
              <div class="voice-card__title">
                <span class="voice-card__name">{{ voice.name }}</span>
                <a-tag v-if="voice.gender" color="default" class="voice-card__gender">{{ voice.gender }}</a-tag>
              </div>
              <a-tooltip :title="playingId === voice.id ? '停止试听' : '试听'">
                <span class="voice-card__play" @click.stop="togglePlay(voice)">
                  <CustomerServiceOutlined v-if="playingId !== voice.id" />
                  <PauseCircleOutlined v-else />
                </span>
              </a-tooltip>
            </div>
            <div class="voice-card__row voice-card__row--bottom">
              <span class="voice-card__style">{{ voice.style }}</span>
              <a-tag color="blue" class="voice-card__provider">{{ voice.provider }}</a-tag>
            </div>
            <span v-if="voice.id === modelValue" class="voice-card__check">
              <CheckOutlined />
            </span>
          </div>
        </div>
        <a-empty v-if="filteredVoices.length === 0" description="该分类暂无音色" />
      </a-tab-pane>
    </a-tabs>
    </template>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import {
  CustomerServiceOutlined,
  PauseCircleOutlined,
  CheckOutlined,
} from '@ant-design/icons-vue'
import type { VoiceItem } from '@/types'

const props = defineProps<{
  voices: VoiceItem[]
  modelValue: string
  loading?: boolean
}>()

const emit = defineEmits<{
  'update:modelValue': [value: string]
}>()

const categories = ['通用', '播音', '客服', '解说', '方言', '儿童'] as const
const activeCategory = ref<string>('通用')
const query = ref('')
const playingId = ref<string | null>(null)
let audioEl: HTMLAudioElement | null = null

const filteredVoices = computed(() => {
  const q = query.value.trim().toLowerCase()
  return props.voices.filter((v) => {
    if (v.category !== activeCategory.value) return false
    if (!q) return true
    return (
      v.name.toLowerCase().includes(q) ||
      v.style.toLowerCase().includes(q) ||
      v.provider.toLowerCase().includes(q)
    )
  })
})

function handleTabChange(key: string): void {
  activeCategory.value = key
}

function selectVoice(id: string): void {
  emit('update:modelValue', id)
}

function togglePlay(voice: VoiceItem): void {
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
    audioEl.play()
    playingId.value = voice.id
  }
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.voice-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(160px, 1fr));
  gap: @spacing-sm;
}

.voice-picker__search {
  margin-bottom: @spacing-md;
}

.voice-card {
  position: relative;
  border: 1px solid @border-color;
  border-radius: @radius-base;
  padding: 8px 10px;
  cursor: pointer;
  background: @card-bg;
  transition: border-color 0.2s ease, box-shadow 0.2s ease, transform 0.15s ease, background 0.2s ease;
  &:hover {
    border-color: @brand-primary;
    box-shadow: @shadow-sm;
    transform: translateY(-2px);
  }
  &:active { transform: translateY(0) scale(0.99); }
  &--selected {
    border-color: @brand-primary;
    background: color-mix(in srgb, @brand-primary 6%, transparent);
    box-shadow: 0 0 0 2px color-mix(in srgb, @brand-primary 22%, transparent);
  }
  &__row {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: @spacing-xs;
    &--top { margin-bottom: 4px; }
    &--bottom { min-width: 0; }
  }
  &__title { display: flex; align-items: center; gap: @spacing-xs; min-width: 0; }
  &__name { font-weight: @font-weight-medium; font-size: @font-size-sm; color: @text-primary; line-height: 18px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
  &__gender { font-size: @font-size-xs; line-height: 16px; flex-shrink: 0; }
  &__style {
    font-size: @font-size-xs;
    color: @text-secondary;
    line-height: 16px;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    min-width: 0;
  }
  &__provider { font-size: @font-size-xs; line-height: 16px; flex-shrink: 0; }
  &__play {
    color: @brand-primary;
    font-size: 16px;
    cursor: pointer;
    padding: 2px;
    border-radius: 50%;
    transition: color 0.2s ease, background 0.2s ease;
    flex-shrink: 0;
    &:hover { color: #fff; background: @brand-primary; }
  }
  &__check {
    position: absolute;
    top: -8px;
    right: -8px;
    width: 20px;
    height: 20px;
    border-radius: 50%;
    background: @brand-primary;
    color: #fff;
    display: flex;
    align-items: center;
    justify-content: center;
    font-size: 11px;
    box-shadow: @shadow-sm;
    animation: pop 0.2s ease;
  }
}

@keyframes pop {
  from { transform: scale(0); opacity: 0; }
  to { transform: scale(1); opacity: 1; }
}

@media (prefers-reduced-motion: reduce) {
  .voice-card { transition: border-color 0.2s ease, background 0.2s ease; &:hover, &:active { transform: none; } }
  .voice-card__check { animation: none; }
}
</style>
