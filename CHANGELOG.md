# Changelog

本文件记录每个发版版本的变化。发版流程（`pnpm release patch|minor|major`）会自动
在本文件顶部插入新版本段：若该版本段已存在（手工精修过）则保留不覆盖，
否则按上个 tag 以来的提交自动生成。

## 0.2.2

- feat: AI 晨会稿整稿流式播放链路（真流式直通 / 整段缓存写回 / 停止播放不掐断上游 / 进行中任务复用）
- feat: 集成认证权限重构、YARP 网关与 Blob 存储迁移
- chore: angineer-docs-ui submodule 升级至 v0.2.2（新增 `@angineer/smartree` 依赖）
- fix: 补回日志管理页面，修复 admin-web 构建失败
- feat(ci): 发版通知带版本号与分类 CHANGELOG

## 0.2.1

- AI 晨会全流程 + 项目文件管理 + 发版通知

## 0.2.0

- 模型服务一容器一部署 + 读标来源定位

## 0.1.1

- 读标溯源跨页/表格、docs-ui 升级等修复批次

## 0.1.0

- 统一仓库版本为 0.1.0
