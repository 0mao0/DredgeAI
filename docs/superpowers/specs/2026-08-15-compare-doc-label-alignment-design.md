# 比标文档别名对齐文件名设计

日期：2026-08-15

## 背景

比标模块目前给标书按**上传顺序**硬编 A/B/C/D 别名（招标文件固定显示「招标」）。当用户上传的标书本身叫 `C.docx`、`D.docx` 时，界面却显示 A、B，造成「名字对不上」的错觉。

别名编号逻辑还散落在多处：`user-web/src/views/ai-bid/compare/constants.ts` 的 `docLabel`，以及 CollusionPanel / EvidenceTable / MetricsTable / ResponseMatrix / EvidenceCard / IndicatorTable 各自的本地副本；API `getOverview` 又单独用 A/B/C 生成热力图标签。因此本次需要统一编号逻辑，避免各视图标签不一致。

## 目标

- 文件名中自带字母的标书，别名尽量与其对齐。
- 同一字母重复时并列编号（C、C1、C2），不做抢占。
- 所有展示位置（双栏、面板、证据、矩阵、热力图）使用同一套别名。

## 编号规则

1. 只给标书编号；招标文件始终显示「招标」。
2. 从文件名主干（去掉扩展名）识别「自报字母」，大小写统一转大写：
   - 字母独立成词才算：`C.docx`、`C-商务标.pdf`、`标书C.docx`、`技术标（C）.pdf` 均识别为 C。
   - 嵌在英文单词/数字串中不算：`CIF条款.pdf`、`B2B报价.docx` 不识别，按顺序补位。
   - 文件名含多个独立字母时取第一个（如 `标书C-D.docx` → C）。
3. 分配顺序（按上传顺序）：
   - 有自报字母的直接使用该字母；同字母重复时：第一个 C，第二个 C1，第三个 C2。
   - 无自报字母的文件从 A 开始按上传顺序补位，跳过已被占用的字母（例：C、D 被占用，两个无字母文件 → A、B；三个无字母文件 → A、B、E）。
4. 上限为 8 份标书；识别范围 A–H 之外的字母不参与自报（按无字母处理）。

## 改造点

- 在共享层新增唯一编号函数（如 `buildDocLabels(documents)`），返回 `docId → 标签` 的映射；`docLabel` 改为它的薄封装。
- `user-web/src/views/ai-bid/compare/constants.ts` 与各组件本地副本统一改为引用该函数。
- `getOverview` 的 `docLabels` 改为由同一函数基于真实文档列表生成，保证热力图与页面其他位置一致。
- 上传上限由 5 份提升到 8 份：前端校验与提示文案、后端 `MaxBidDocuments`（CompareTask / CompareDraft）、对应后端测试同步更新。
- 不改后端，别名不持久化，仅前端展示层。

## 验证

- Node 单测覆盖：`C.docx`/`D.docx` 对齐、`标书C.docx` 识别、`CIF`/`B2B` 不识别、重复字母 C/C1、无字母补位跳过已占用字母、招标不变。
- `pnpm run typecheck` 通过。
- 后端测试全绿，含更新后的 8 份上限用例。
