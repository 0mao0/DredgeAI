#!/usr/bin/env node
/**
 * 一键发版：同步 bump 4 个 package.json 版本 + 写 CHANGELOG 段 → 提交 → 打 tag → 推送。
 *
 * 用法：
 *   pnpm release patch --note "整稿流式 TTS 链路"
 *   pnpm release minor
 *   pnpm release 0.3.0 --dry-run          # 只预览不落地
 *   pnpm release patch --no-push          # 只提交+打 tag，不推送
 *
 * 约定（对齐 Angineer 发版方式，tag 驱动）：
 *   1) 4 个 package.json 的 version 必须一致，CI 从根 package.json 读版本拼镜像号；
 *   2) CHANGELOG.md 里 `## <版本>` 段若已存在则保留（允许先手工精修），否则自动生成；
 *   3) 打 `v<版本>` 标注 tag，推送后由 CI 构建部署、企微通知读取该版本段。
 */
import { execFileSync } from 'node:child_process'
import fs from 'node:fs'
import path from 'node:path'
import process from 'node:process'
import { fileURLToPath } from 'node:url'

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const PKG_FILES = [
  'package.json',
  'user-web/package.json',
  'admin-web/package.json',
  'packages/shared/package.json',
]
const CHANGELOG = 'CHANGELOG.md'
const MAX_CHANGELOG_ITEMS = 12
const DEFAULT_REMOTES = ['origin', 'github']
const BUMP_KINDS = new Set(['patch', 'minor', 'major'])

function fail(message) {
  console.error(`✖ ${message}`)
  process.exit(1)
}

function git(args, { allowFail = false, raw = false } = {}) {
  try {
    const out = execFileSync('git', args, { cwd: ROOT, encoding: 'utf8' })
    // 默认 trim 便于比较；porcelain 等按行解析的输出需保留前导状态位，用 raw
    return raw ? out : out.trim()
  } catch (error) {
    if (allowFail) return ''
    fail(`git ${args.join(' ')} 失败：${error.stderr?.trim() || error.message}`)
  }
}

function parseArgs(argv) {
  const options = { kind: '', note: '', dryRun: false, push: true, remotes: [...DEFAULT_REMOTES] }
  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i]
    if (arg === '--dry-run') options.dryRun = true
    else if (arg === '--no-push') options.push = false
    else if (arg === '--note') options.note = (argv[++i] ?? '').trim()
    else if (arg === '--remote') {
      options.remotes = (argv[++i] ?? '')
        .split(',')
        .map((r) => r.trim())
        .filter(Boolean)
    } else if (arg.startsWith('-')) fail(`未知参数：${arg}`)
    else if (!options.kind) options.kind = arg
    else fail(`多余参数：${arg}`)
  }
  if (!options.kind) fail('用法：pnpm release <patch|minor|major|x.y.z> [--note "说明"] [--dry-run] [--no-push]')
  return options
}

function readVersion(file) {
  const raw = fs.readFileSync(path.join(ROOT, file), 'utf8')
  const match = raw.match(/"version"\s*:\s*"([^"]+)"/)
  if (!match) fail(`${file} 里找不到 version 字段`)
  return match[1]
}

function writeVersion(file, next) {
  const full = path.join(ROOT, file)
  const raw = fs.readFileSync(full, 'utf8')
  // 只替换首个 "version" 字段，保留文件原有格式与 BOM
  fs.writeFileSync(full, raw.replace(/("version"\s*:\s*")[^"]+(")/, `$1${next}$2`), 'utf8')
}

function bumpVersion(current, kind) {
  if (!BUMP_KINDS.has(kind)) return kind
  const parts = current.split('.').map(Number)
  if (parts.length !== 3 || parts.some(Number.isNaN)) fail(`当前版本号无法解析：${current}`)
  const [major, minor, patch] = parts
  if (kind === 'major') return `${major + 1}.0.0`
  if (kind === 'minor') return `${major}.${minor + 1}.0`
  return `${major}.${minor}.${patch + 1}`
}

function compareVersions(a, b) {
  const pa = a.split('.').map(Number)
  const pb = b.split('.').map(Number)
  for (let i = 0; i < 3; i++) {
    if ((pa[i] ?? 0) !== (pb[i] ?? 0)) return (pa[i] ?? 0) - (pb[i] ?? 0)
  }
  return 0
}

/** 没给 --note 时，用上个 tag 以来的 feat/fix 自动拼一句发版说明 */
function autoNote() {
  const base = git(['describe', '--tags', '--abbrev=0', 'HEAD'], { allowFail: true })
  const range = base ? `${base}..HEAD` : 'HEAD'
  const subjects = git(['log', '--no-merges', '--format=%s', range], { allowFail: true })
    .split('\n')
    .filter(Boolean)
  const highlights = subjects
    .map((subject) => subject.match(/^(?:feat|fix)(?:\([^)]*\))?!?:\s*(.+)$/)?.[1])
    .filter(Boolean)
    .slice(0, 3)
  return highlights.length > 0 ? highlights.join('、').slice(0, 80) : '发版'
}

/** 上个 tag 以来的提交，作为新版本段的默认条目 */
function changelogBullets() {
  const base = git(['describe', '--tags', '--abbrev=0', 'HEAD'], { allowFail: true })
  const range = base ? `${base}..HEAD` : 'HEAD'
  const subjects = git(['log', '--no-merges', '--format=%s', range], { allowFail: true })
    .split('\n')
    .filter(Boolean)
  const bullets = subjects.slice(0, MAX_CHANGELOG_ITEMS).map((subject) => `- ${subject}`)
  if (subjects.length > bullets.length) {
    bullets.push(`- …（另有 ${subjects.length - bullets.length} 项，见提交记录）`)
  }
  return bullets
}

function changelogText() {
  const full = path.join(ROOT, CHANGELOG)
  return fs.existsSync(full) ? fs.readFileSync(full, 'utf8') : '# Changelog\n'
}

function hasSection(text, version) {
  return new RegExp(`^##\\s+v?${version.replace(/\./g, '\\.')}\\s*$`, 'm').test(text)
}

/** 把 `## <版本>` 段插到第一个已有版本段之前（保持倒序） */
function prependSection(text, version, bullets) {
  const lines = text.split('\n')
  const index = lines.findIndex((line) => line.startsWith('## '))
  const block = [`## ${version}`, '', ...bullets, '']
  if (index === -1) return [...lines, ...block].join('\n')
  lines.splice(index, 0, ...block)
  return lines.join('\n')
}

function preview(payload) {
  console.log('\n── 发版计划 ─────────────────────────────')
  console.log(`  分支    : ${payload.branch}`)
  console.log(`  版本    : ${payload.current} → ${payload.next}`)
  console.log(`  tag     : v${payload.next}`)
  console.log(`  说明    : ${payload.note}`)
  console.log(`  版本号  : ${PKG_FILES.join('、')}`)
  console.log(`  CHANGELOG: ${payload.changelogTitle}`)
  for (const line of payload.bullets) {
    console.log(`    ${line}`)
  }
  if (payload.dirty.length > 0) {
    console.log(`  未提交(不影响本次) : ${payload.dirty.join(', ')}`)
  }
  console.log(`  推送    : ${payload.push ? payload.remotes.join(', ') : '不推送（--no-push）'}`)
  console.log('─────────────────────────────────────────\n')
}

const options = parseArgs(process.argv.slice(2))
const branch = git(['rev-parse', '--abbrev-ref', 'HEAD'])
if (branch !== 'master') fail(`当前分支是 ${branch}，发版请在 master 上执行`)

const dirty = git(['status', '--porcelain'], { raw: true })
  .split('\n')
  .filter((line) => line.trim())
  .map((line) => line.slice(3).trim())
const dirtyPkg = dirty.filter((file) => PKG_FILES.includes(file))
if (dirtyPkg.length > 0) fail(`以下文件有未提交改动，先处理再发版：${dirtyPkg.join(', ')}`)

const versions = PKG_FILES.map((file) => ({ file, version: readVersion(file) }))
const current = versions[0].version
const drifted = versions.filter((item) => item.version !== current)
if (drifted.length > 0) {
  fail(`package.json 版本不一致：${versions.map((v) => `${v.file}=${v.version}`).join('、')}`)
}

const next = bumpVersion(current, options.kind)
if (compareVersions(next, current) <= 0) fail(`新版本 ${next} 不大于当前 ${current}`)
const tag = `v${next}`
if (git(['tag', '--list', tag], { allowFail: true })) fail(`tag ${tag} 已存在`)

const note = options.note || autoNote()
const currentChangelog = changelogText()
const changelogExists = hasSection(currentChangelog, next)
const bullets = changelogExists ? [`（CHANGELOG.md 已存在 ## ${next} 段，保留原内容）`] : changelogBullets()
const changelogTitle = changelogExists ? `已有 ## ${next} 段，保留` : `自动生成 ## ${next} 段（${bullets.length} 条）`
preview({ branch, current, next, note, push: options.push, remotes: options.remotes, dirty, bullets, changelogTitle })

if (options.dryRun) {
  console.log('（--dry-run：未做任何改动）')
  process.exit(0)
}

for (const file of PKG_FILES) writeVersion(file, next)
if (!changelogExists) {
  fs.writeFileSync(
    path.join(ROOT, CHANGELOG),
    prependSection(currentChangelog, next, bullets),
    'utf8',
  )
}
git(['add', ...PKG_FILES, CHANGELOG])
git(['commit', '-m', `chore(version): 升版 ${next}（${note}）`])
git(['tag', '-a', tag, '-m', `${tag}: ${note}`])
console.log(`✔ 已提交并打 tag：${tag}`)

if (options.push) {
  for (const remote of options.remotes) {
    git(['push', remote, 'master'])
    git(['push', remote, tag])
    console.log(`✔ 已推送到 ${remote}`)
  }
  console.log('\nCI 将按新版本构建部署；企微通知会读取 CHANGELOG 该版本段。')
} else {
  console.log(`\n未推送。手动推送：git push origin master && git push origin ${tag}`)
}
