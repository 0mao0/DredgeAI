<template>
  <SectionCard title="会后报告" flush>
    <template #extra>
      <span v-if="report" class="report-step__extra">{{ formatTime(report.createdAt) }}</span>
    </template>

    <a-skeleton v-if="!report" :paragraph="{ rows: 6 }" />
    <template v-else>
      <!-- 一、到场人员 / 未识别人脸 -->
      <div class="report-step__block">
        <div class="report-step__label">到场人员</div>
        <div class="report-step__row">
          <div class="report-step__count-card is-success">
            <span class="report-step__count-num">{{ presentCount }}</span>
            <span class="report-step__count-label">到场</span>
          </div>
          <div v-for="item in presentList" :key="item.workerId ?? item.name" class="report-step__person-card">
            <img v-if="item.facePhotoUrl" :src="item.facePhotoUrl" class="report-step__face" alt="">
            <span v-else class="report-step__face-fallback">{{ item.name.slice(0, 1) }}</span>
            <span class="report-step__person-name">{{ personName(item) }}</span>
          </div>
        </div>
        <div class="report-step__row">
          <div class="report-step__count-card is-muted">
            <span class="report-step__count-num">{{ unrecognizedCount }}</span>
            <span class="report-step__count-label">未识别</span>
          </div>
          <div v-for="face in report.unrecognizedFaces" :key="face.id" class="report-step__person-card">
            <img v-if="face.photoUrl" :src="face.photoUrl" class="report-step__face" alt="">
            <span v-else class="report-step__face-fallback"><UserOutlined /></span>
            <span class="report-step__person-name">{{ Math.round(face.confidence * 100) }}%</span>
          </div>
        </div>
      </div>

      <!-- 二、晨会内容 -->
      <div class="report-step__block">
        <div class="report-step__label">晨会内容</div>
        <div v-if="draftParagraphs.length" class="report-step__draft">
          <p v-for="(para, index) in draftParagraphs" :key="index" class="report-step__draft-para">
            {{ para }}
          </p>
        </div>
        <div v-else class="report-step__empty">（暂无晨会内容）</div>
      </div>

      <!-- 三、会议纪要 -->
      <div class="report-step__block">
        <div class="report-step__label">会议纪要</div>
        <div class="report-step__transcript">
          <template v-if="report.transcript">
            <FileTextOutlined class="report-step__transcript-icon" />
            {{ report.transcript }}
          </template>
          <span v-else>（暂无纪要内容，录音转写完成后将展示在这里）</span>
        </div>

        <div v-if="report.qaRecords.length" class="report-step__qa-sub">
          <div class="report-step__label">问答</div>
          <div v-for="qa in report.qaRecords" :key="qa.id" class="report-step__qa">
            <div class="report-step__qa-head">
              <span class="report-step__qa-intent" :class="`is-${qa.intentType}`">{{ intentText(qa.intentType) }}</span>
              <span class="report-step__qa-time">{{ formatTime(qa.createdAt) }}</span>
            </div>
            <div class="report-step__qa-q"><b>问</b>{{ qa.question }}</div>
            <div class="report-step__qa-a"><b>答</b>{{ qa.answer }}</div>
          </div>
        </div>
      </div>
    </template>
  </SectionCard>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { FileTextOutlined, UserOutlined } from '@ant-design/icons-vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
import type { AttendanceItemDto, QaRecordDto, ReportDto } from '@/types'
import { displayAttendanceName } from '@/utils/attendanceName'

const props = defineProps<{
  report: ReportDto | null
  draftContent: string
}>()

const presentList = computed(
  () => props.report?.attendance.filter((a) => a.status === 'present' || a.status === 'late') ?? [],
)
const presentCount = computed(() => new Set(presentList.value.map((a) => a.workerId).filter(Boolean)).size)
const unrecognizedCount = computed(() => props.report?.unrecognizedFaces.length ?? 0)

function personName(item: AttendanceItemDto): string {
  return displayAttendanceName(item, presentList.value)
}

const draftParagraphs = computed(() =>
  props.draftContent
    .split('\n')
    .map((s) => s.trim())
    .filter(Boolean),
)

function intentText(intent: QaRecordDto['intentType']): string {
  return {
    knowledge: '知识库',
    chitchat: '闲聊',
    meeting: '会议',
  }[intent] ?? intent
}

function formatTime(iso: string): string {
  if (!iso) return ''
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return ''
  const pad = (n: number): string => String(n).padStart(2, '0')
  return `${d.getMonth() + 1}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.report-step__extra {
  font-size: @font-size-sm;
  color: @text-tertiary;
  white-space: nowrap;
}

.report-step__block {
  margin-bottom: @spacing-xl;

  &:last-child {
    margin-bottom: 0;
  }
}
.report-step__label {
  display: flex;
  align-items: center;
  gap: @spacing-sm;
  font-size: @font-size-sm;
  font-weight: @font-weight-medium;
  color: @text-secondary;
  margin-bottom: @spacing-sm;
}

.report-step__row {
  display: flex;
  flex-wrap: wrap;
  gap: @spacing-sm;
  margin-bottom: @spacing-base;

  &:last-child {
    margin-bottom: 0;
  }
}
.report-step__count-card {
  flex: none;
  width: 72px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 2px;
  padding: @spacing-sm @spacing-xs;
  border-radius: @radius-base;
  background: @content-bg;

  &.is-success .report-step__count-num {
    color: @success;
  }
  &.is-muted .report-step__count-num {
    color: @text-tertiary;
  }
}
.report-step__count-num {
  font-size: @font-size-lg;
  font-weight: @font-weight-semibold;
  font-variant-numeric: tabular-nums;
  line-height: 1.1;
}
.report-step__count-label {
  font-size: @font-size-xs;
  color: @text-secondary;
}

.report-step__person-card {
  position: relative;
  width: 56px;
  height: 72px;
  border-radius: @radius-base;
  overflow: hidden;
  background: @content-bg;
}
.report-step__face {
  width: 100%;
  height: 100%;
  object-fit: cover;
  display: block;
}
.report-step__face-fallback {
  width: 100%;
  height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: @font-size-lg;
  font-weight: @font-weight-medium;
  color: @text-secondary;
  background: color-mix(in srgb, var(--color-brand) 12%, var(--color-bg-elevated));
}
.report-step__person-name {
  position: absolute;
  left: 0;
  right: 0;
  bottom: 0;
  padding: 2px @spacing-xs;
  background: color-mix(in srgb, #000 55%, transparent);
  color: #fff;
  font-size: @font-size-xs;
  line-height: 1.4;
  text-align: center;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.report-step__draft {
  background: @content-bg;
  border-radius: @radius-base;
  padding: @spacing-md @spacing-base;
}
.report-step__draft-para {
  margin: 0 0 @spacing-base;
  line-height: 1.8;
  font-size: @font-size-base;
  color: @text-primary;

  &:last-child {
    margin-bottom: 0;
  }
}

.report-step__transcript {
  max-height: 320px;
  overflow-y: auto;
  background: @content-bg;
  border-radius: @radius-base;
  padding: @spacing-md @spacing-base;
  white-space: pre-wrap;
  word-break: break-word;
  line-height: 1.7;
  font-size: @font-size-sm;
  color: @text-primary;
}
.report-step__transcript-icon {
  margin-right: @spacing-sm;
  color: @text-tertiary;
}

.report-step__qa-sub {
  margin-top: @spacing-base;
}
.report-step__qa {
  padding: @spacing-sm @spacing-md @spacing-md;
  background: @content-bg;
  border-radius: @radius-base;
  border-left: 3px solid @brand-primary;
  margin-bottom: @spacing-sm;

  &:last-child {
    margin-bottom: 0;
  }
}
.report-step__qa-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: @spacing-sm;
  margin-bottom: @spacing-xs;
}
.report-step__qa-intent {
  font-size: @font-size-xs;
  font-weight: @font-weight-medium;
  padding: 1px @spacing-sm;
  border-radius: @radius-sm;

  &.is-knowledge {
    color: @success;
    background: color-mix(in srgb, var(--color-success) 12%, transparent);
  }
  &.is-chitchat {
    color: @text-tertiary;
    background: color-mix(in srgb, var(--color-text-tertiary) 12%, transparent);
  }
  &.is-meeting {
    color: @brand-primary;
    background: color-mix(in srgb, var(--color-brand) 12%, transparent);
  }
}
.report-step__qa-time {
  font-size: @font-size-xs;
  color: @text-tertiary;
}
.report-step__qa-q {
  color: @text-primary;
  margin-bottom: @spacing-xs;
  line-height: 1.6;

  b {
    margin-right: @spacing-xs;
    color: @brand-primary;
  }
}
.report-step__qa-a {
  color: @text-secondary;
  line-height: 1.6;

  b {
    margin-right: @spacing-xs;
    color: @text-tertiary;
  }
}
.report-step__empty {
  color: @text-tertiary;
  font-size: @font-size-sm;
}
</style>
