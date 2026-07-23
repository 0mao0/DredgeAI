import type { VoiceItem, DubbingTask, DubbingUsageTimeSeries, DubbingUsageSummary } from '@shared/types'

export const voiceItems: VoiceItem[] = [
  { id: 'zh-male-wangfei', name: '王飞男声', gender: '男声', provider: 'CosyVoice 3', tags: [], visibility: 'public', createdAt: '2026-01-15T00:00:00' },
  { id: 'zh-male-news', name: '男声·播报', gender: '男声', provider: 'CosyVoice 3', tags: ['新闻', '正式'], visibility: 'public', createdAt: '2026-01-15T00:00:00' },
  { id: 'zh-female-news', name: '女声·播报', gender: '女声', provider: 'CosyVoice 3', tags: ['新闻', '正式'], visibility: 'public', createdAt: '2026-01-15T00:00:00' },
  { id: 'zh-male-general', name: '男声·日常', gender: '男声', provider: 'CosyVoice 3', tags: ['日常'], visibility: 'public', createdAt: '2026-01-15T00:00:00' },
  { id: 'zh-male-narrator', name: '男声·纪录片', gender: '男声', provider: 'CosyVoice 3', tags: ['纪录片', '正式'], visibility: 'public', createdAt: '2026-01-15T00:00:00' },
]

let nextId = 1

function makeTask(overrides: Partial<DubbingTask> & { text: string }): DubbingTask {
  const id = `dubbing-${nextId++}`
  const charCount = overrides.text.length
  const tokenCost = Math.ceil(charCount / 1.5) + 50
  const now = new Date()
  return {
    id,
    charCount,
    tokenCost,
    speed: 1.0,
    voiceId: 'zh-female-general',
    voiceName: '知柔·女声',
    category: '通用',
    createdAt: now.toISOString(),
    ...overrides,
    status: overrides.status || '已完成',
  }
}

export let dubbingTasks: DubbingTask[] = [
  makeTask({ id: 'dubbing-1', text: '各位领导，各位同事，大家下午好。今天由我来为大家汇报本项目的最新进展情况。', voiceId: 'zh-male-news', voiceName: '知衡·男声', category: '播音', speed: 1.0, createdAt: '2026-07-18T14:30:00', durationSec: 8.5 }),
  makeTask({ id: 'dubbing-2', text: '尊敬的客户，您好！感谢您选择我们的服务。如需帮助，请按1转接人工客服。', voiceId: 'zh-female-service', voiceName: '知悦·女声', category: '客服', speed: 1.0, createdAt: '2026-07-18T11:20:00', durationSec: 6.2 }),
  makeTask({ id: 'dubbing-3', text: '在广阔的东海海域，一艘大型耙吸式挖泥船正在执行疏浚作业，将海底淤泥通过耙头吸入泥舱。', voiceId: 'zh-male-narrator', voiceName: '知声·男声', category: '解说', speed: 1.2, createdAt: '2026-07-17T16:45:00', durationSec: 12.8 }),
  makeTask({ id: 'dubbing-4', text: '从前，有一只小猴子，他住在一座大山的森林里。有一天，他决定出去找朋友玩。', voiceId: 'zh-female-child', voiceName: '知萌·童声', category: '儿童', speed: 0.9, createdAt: '2026-07-17T09:10:00', durationSec: 7.5 }),
  makeTask({ id: 'dubbing-5', text: '今天我们要讲的是深度学习中的Transformer架构。这是一种基于自注意力机制的神经网络模型。', voiceId: 'zh-male-tech', voiceName: '知睿·男声', category: '通用', speed: 1.0, createdAt: '2026-07-16T15:00:00', durationSec: 10.1 }),
  makeTask({ id: 'dubbing-6', text: '成都，一座来了就不想走的城市。火锅、串串、担担面，巴适得很！', voiceId: 'zh-female-sichuan', voiceName: '知蜀·女声', category: '方言', speed: 1.0, createdAt: '2026-07-16T10:30:00', durationSec: 5.8 }),
  makeTask({ id: 'dubbing-7', text: '施工安全是工程建设的重中之重，所有人员必须严格遵守安全操作规程。', voiceId: 'zh-male-general', voiceName: '知言·男声', category: '通用', speed: 1.0, createdAt: '2026-07-15T08:20:00', durationSec: 6.5, status: '生成中' }),
  makeTask({ id: 'dubbing-8', text: '本次会议的议题主要包括以下几个方面：第一，上季度工作总结；第二，下半年工作计划。', voiceId: 'zh-female-news', voiceName: '知语·女声', category: '播音', speed: 1.0, createdAt: '2026-07-15T14:00:00', durationSec: 9.2, status: '已失败' }),
  // Admin additional records (extend with user info)
  makeTask({ id: 'dubbing-9', text: '由我部负责的京杭运河航道整治工程已进入关键阶段，目前正在进行水下爆破作业。', voiceId: 'zh-male-news', voiceName: '知衡·男声', category: '播音', speed: 1.0, userId: 'u-001', userName: '张建国', department: '工程部', createdAt: '2026-07-14T09:00:00', durationSec: 11.3 }),
  makeTask({ id: 'dubbing-10', text: '新来的同事请于明日上午9点前往人力资源部办理入职手续，携带身份证及学历证明原件。', voiceId: 'zh-female-general', voiceName: '知柔·女声', category: '通用', speed: 1.0, userId: 'u-002', userName: '李小梅', department: '人事部', createdAt: '2026-07-13T16:30:00', durationSec: 7.0 }),
  makeTask({ id: 'dubbing-11', text: '尊敬的业主，您申请的施工许可已通过审批，请登录系统下载电子批文。', voiceId: 'zh-male-service', voiceName: '知诚·男声', category: '客服', speed: 1.0, userId: 'u-003', userName: '王大力', department: '工程部', createdAt: '2026-07-12T10:15:00', durationSec: 6.8, deletedByUser: true }),
  makeTask({ id: 'dubbing-12', text: '各位游客请注意，前方即将到达本次航程最壮观的三峡大坝景区，请做好准备。', voiceId: 'zh-female-narrator', voiceName: '知韵·女声', category: '解说', speed: 1.0, userId: 'u-001', userName: '张建国', department: '工程部', createdAt: '2026-07-11T13:45:00', durationSec: 9.5 }),
  makeTask({ id: 'dubbing-13', text: '财务报表显示，上半年公司营收同比增长15.3%，净利润增长率达到22.7%。', voiceId: 'zh-male-tech', voiceName: '知睿·男声', category: '通用', speed: 1.0, userId: 'u-004', userName: '赵丽华', department: '财务部', createdAt: '2026-07-10T11:00:00', durationSec: 8.2 }),
  makeTask({ id: 'dubbing-14', text: '本周末公司将组织团建活动，地点为太湖国家湿地公园，请大家于周六早7点在公司门口集合。', voiceId: 'zh-female-friendly', voiceName: '知暖·女声', category: '通用', speed: 1.0, userId: 'u-002', userName: '李小梅', department: '人事部', createdAt: '2026-07-09T15:20:00', durationSec: 10.6 }),
  makeTask({ id: 'dubbing-15', text: '上海宁，侬好呀！今朝天气老适宜，出去白相相伐？', voiceId: 'zh-male-shanghai', voiceName: '知沪·男声', category: '方言', speed: 1.0, userId: 'u-005', userName: '陈晓东', department: '市场部', createdAt: '2026-07-08T09:30:00', durationSec: 5.1, deletedByUser: true }),
  makeTask({ id: 'dubbing-16', text: '唔该，我想问下呢个文件要点样填？', voiceId: 'zh-female-cantonese', voiceName: '知粤·女声', category: '方言', speed: 1.0, userId: 'u-003', userName: '王大力', department: '工程部', createdAt: '2026-07-07T14:10:00', durationSec: 4.3, deletedByUser: true }),
  makeTask({ id: 'dubbing-17', text: '台湾是中华文化的重要传承地，闽南语作为其重要方言之一，保留了大量的古汉语发音。', voiceId: 'zh-male-minnan', voiceName: '知闽·男声', category: '方言', speed: 1.0, userId: 'u-006', userName: '林志明', department: '技术部', createdAt: '2026-07-06T11:50:00', durationSec: 8.9 }),
  makeTask({ id: 'dubbing-18', text: '经过连续48小时的奋战，抢险救援队终于在凌晨3点成功封堵了溃口。', voiceId: 'zh-male-documentary', voiceName: '知远·男声', category: '解说', speed: 1.1, userId: 'u-007', userName: '刘伟', department: '安全部', createdAt: '2026-07-05T16:40:00', durationSec: 7.6 }),
  makeTask({ id: 'dubbing-19', text: '各位代表，现在请举手表决本次会议的第一项决议。同意的请举手。', voiceId: 'zh-female-news', voiceName: '知语·女声', category: '播音', speed: 1.0, userId: 'u-004', userName: '赵丽华', department: '财务部', createdAt: '2026-07-04T10:00:00', durationSec: 6.4, deletedByUser: true }),
  makeTask({ id: 'dubbing-20', text: '小朋友，你知道吗？恐龙在数亿年前曾经是地球上的霸主呢。', voiceId: 'zh-female-child', voiceName: '知萌·童声', category: '儿童', speed: 0.8, userId: 'u-008', userName: '周慧敏', department: '市场部', createdAt: '2026-07-03T08:30:00', durationSec: 8.1 }),
  makeTask({ id: 'dubbing-21', text: '根据气象部门预报，今年第3号台风将于明日上午在我省沿海登陆，请各单位做好防台防汛准备。', voiceId: 'zh-male-news', voiceName: '知衡·男声', category: '播音', speed: 1.0, userId: 'u-009', userName: '孙建国', department: '安全部', createdAt: '2026-07-02T14:20:00', durationSec: 10.3 }),
  makeTask({ id: 'dubbing-22', text: '让我们以热烈的掌声欢迎今天的演讲嘉宾——陈教授！', voiceId: 'zh-female-general', voiceName: '知柔·女声', category: '通用', speed: 1.0, userId: 'u-010', userName: '吴芳', department: '行政部', createdAt: '2026-07-01T09:00:00', durationSec: 5.5 }),
  makeTask({ id: 'dubbing-23', text: '试验数据表明，采用新型复合材料后，结构的抗拉强度提升了约35%。', voiceId: 'zh-male-tech', voiceName: '知睿·男声', category: '通用', speed: 1.0, userId: 'u-006', userName: '林志明', department: '技术部', createdAt: '2026-06-30T15:30:00', durationSec: 7.3 }),
  makeTask({ id: 'dubbing-24', text: '一碗酸辣粉，再加两个锅盔，这就是老成都最巴适的早餐。', voiceId: 'zh-female-sichuan', voiceName: '知蜀·女声', category: '方言', speed: 1.0, userId: 'u-011', userName: '杨红', department: '行政部', createdAt: '2026-06-29T08:15:00', durationSec: 5.9 }),
  makeTask({ id: 'dubbing-25', text: '请各部门负责人于本周五前将下半年预算计划提交至财务部。', voiceId: 'zh-male-general', voiceName: '知言·男声', category: '通用', speed: 1.0, userId: 'u-005', userName: '陈晓东', department: '市场部', createdAt: '2026-06-28T11:40:00', durationSec: 7.0 }),
  makeTask({ id: 'dubbing-26', text: '在过去的半个世纪里，这座大桥见证了城市的变迁与发展。', voiceId: 'zh-male-documentary', voiceName: '知远·男声', category: '解说', speed: 1.0, userId: 'u-007', userName: '刘伟', department: '安全部', createdAt: '2026-06-27T14:00:00', durationSec: 6.6 }),
  makeTask({ id: 'dubbing-27', text: '你儿子这次考试考得不错嘛，班级前三名，继续加油哦！', voiceId: 'zh-female-sichuan', voiceName: '知蜀·女声', category: '方言', speed: 1.0, userId: 'u-012', userName: '何丽', department: '人事部', createdAt: '2026-06-26T16:20:00', durationSec: 5.3, deletedByUser: true }),
  makeTask({ id: 'dubbing-28', text: '这起安全事故给我们敲响了警钟，必须立即开展全公司范围内的安全隐患排查。', voiceId: 'zh-male-news', voiceName: '知衡·男声', category: '播音', speed: 1.0, userId: 'u-009', userName: '孙建国', department: '安全部', createdAt: '2026-06-25T09:30:00', durationSec: 9.8 }),
  makeTask({ id: 'dubbing-29', text: '欢迎收看今天的《科技前沿》节目，我是主持人张悦。', voiceId: 'zh-female-news', voiceName: '知语·女声', category: '播音', speed: 1.0, userId: 'u-013', userName: '张悦', department: '市场部', createdAt: '2026-06-24T19:00:00', durationSec: 5.7 }),
  makeTask({ id: 'dubbing-30', text: '多模态大模型是当前人工智能研究的前沿方向，它能够同时处理文本、图像、音频等多种信息。', voiceId: 'zh-male-tech', voiceName: '知睿·男声', category: '通用', speed: 1.0, userId: 'u-006', userName: '林志明', department: '技术部', createdAt: '2026-06-23T10:00:00', durationSec: 11.5 }),
]

function generateTimeSeries() {
  const now = new Date()
  const categories: string[] = []
  const tasksData: number[] = []
  const tokensData: number[] = []
  const usersData: number[] = []
  for (let i = 29; i >= 0; i--) {
    const d = new Date(now)
    d.setDate(d.getDate() - i)
    categories.push(`${d.getMonth() + 1}/${d.getDate()}`)
    tasksData.push(Math.floor(5 + Math.random() * 25))
    tokensData.push(Math.floor(200 + Math.random() * 1800))
    usersData.push(Math.floor(2 + Math.random() * 8))
  }
  return { categories, tasksData, tokensData, usersData }
}

function buildTimeSeries(): DubbingUsageTimeSeries {
  const raw = generateTimeSeries()
  return {
    categories: raw.categories,
    tasks: [{ name: '任务数', data: raw.tasksData }],
    tokens: [{ name: 'Token 消耗', data: raw.tokensData }],
    users: [{ name: '活跃用户数', data: raw.usersData }],
  }
}

export const dubbingUsageTimeSeries = buildTimeSeries()

export const dubbingUsageSummary: DubbingUsageSummary = {
  totalTasks: dubbingTasks.length,
  totalTokens: dubbingTasks.reduce((s, t) => s + t.tokenCost, 0),
  totalUsers: new Set(dubbingTasks.filter(t => t.userId).map(t => t.userId)).size,
  totalAudioSec: Math.round(dubbingTasks.reduce((s, t) => s + (t.durationSec || 0), 0)),
  todayTasks: 2,
  todayTokens: 320,
}
