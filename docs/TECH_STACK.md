# 前端技术栈清单

> 本清单基于 `package.json` 整理，作为新项目起步的标准化参考。

## 一、核心框架
| 技术 | 版本 |
|---|---|
| Vue | ^3.3.4 |
| TypeScript | ^5.2.2 |
| Vite | ^5.0.11 |
| vue-tsc（类型检查） | ^2.1.10 |

## 二、UI / 组件库
| 技术 | 版本 |
|---|---|
| ant-design-vue | ^4.1.0 |
| @ant-design/icons-vue | ^7.0.1 |
| @ant-design/icons | ^5.0.1 |
| @surely-vue/table（表格） | ^4.1.7 |
| Font Awesome（vue / core / icons） | ^3.1.1 / ^7.0.0 / ^7.0.0 |
| UnoCSS（原子化 CSS） | 66.1.0-beta.12 |
| LESS（样式预编译） | ^4.1.2 |

## 三、状态管理 / 路由
| 技术 | 版本 |
|---|---|
| Pinia | ^2.0.14 |
| pinia-plugin-persistedstate | ^4.2.0 |
| Vue Router | ^4.0.14 |

## 四、数据 / 网络 / 可视化
| 技术 | 版本 |
|---|---|
| axios | ^0.27.2 |
| @microsoft/signalr（实时通信） | ^6.0.8 |
| echarts | ^5.6.0 |
| vue-echarts | 6 |
| shiw-cesium-sdk（GIS，CDN） | 0.0.41 |
| @amap/amap-jsapi-loader（高德地图） | ^1.0.1 |
| overall-view-map-sdk（地图 SDK） | 0.0.138 |

## 五、工具 / 辅助库
| 技术 | 版本 |
|---|---|
| @vueuse/core | ^10.11.1 |
| dayjs | ^1.11.5 |
| lodash-es | ^4.17.21 |
| crypto-js | ^4.2.0 |
| qs | ^6.11.1 |
| nprogress | ^0.2.0 |
| screenfull | ^6.0.1 |
| v-viewer / viewerjs（图片预览） | ^3.0.21 / ^1.11.7 |
| vue3-draggable-resizable | ^1.6.5 |
| vue3-video-play | 1.3.1 |
| smooth-dnd | ^0.12.1 |
| spark-md5 | ^3.0.2 |
| katex | ^0.16.0 |
| vue-color-input | ^2.0.0 |
| @ctrl/tinycolor | ^4.1.0 |
| resize-detector | ^0.3.0 |

## 六、工程化 / 质量
| 技术 | 版本 |
|---|---|
| 包管理器 | pnpm@9.1.4 |
| Node 要求 | >=18.12.0 |
| ESLint / typescript-eslint | ^9.20.0 / ^8.23.0 |
| Prettier | ^3.5.0 |
| Stylelint | ^16.14.1 |
| vite-plugin-svgr | ^2.4.0 |
| vite-plugin-vue-devtools | ^7.7.6 |
| @vitejs/plugin-vue | ^5.0.3 |

## 七、其他硬性要求
- **Node.js >= 18.12.0**
- 新文件一律使用 TypeScript，避免 `any`
- 组件用 SFC（`.vue`）+ `<script setup>`，单组件建议 ≤200 行
- 内部模块用 `@/` 别名导入
- 第三方库 Cesium 通过 **CDN importmap** 加载，不进 npm
- 路由懒加载，动态路由名称须与菜单 API 响应匹配
- 认证令牌存于 `localStorage` 的 `STORAGE_TOKEN_KEY`
