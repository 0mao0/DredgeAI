import type { Component } from 'vue'
import * as Icons from '@ant-design/icons-vue'

/**
 * 应用图标全集：admin 发布管理的图标选择器、user-web 侧边栏/个人中心渲染
 * 共用同一份映射，避免两端各自维护导致漂移（如漏掉某个图标渲染成空白）。
 */
export const APP_ICONS: Record<string, Component> = {
  BookOutlined: Icons.BookOutlined,
  VideoCameraOutlined: Icons.VideoCameraOutlined,
  CustomerServiceOutlined: Icons.CustomerServiceOutlined,
  BulbOutlined: Icons.BulbOutlined,
  ToolOutlined: Icons.ToolOutlined,
  FileProtectOutlined: Icons.FileProtectOutlined,
  DashboardOutlined: Icons.DashboardOutlined,
  RadarChartOutlined: Icons.RadarChartOutlined,
  ExperimentOutlined: Icons.ExperimentOutlined,
  FileSearchOutlined: Icons.FileSearchOutlined,
  TeamOutlined: Icons.TeamOutlined,
  AppstoreOutlined: Icons.AppstoreOutlined,
}

/** 按图标名解析组件；未知图标回退到 AppstoreOutlined，避免渲染空白。 */
export function resolveAppIcon(name?: string): Component {
  return (name && APP_ICONS[name]) || Icons.AppstoreOutlined
}
