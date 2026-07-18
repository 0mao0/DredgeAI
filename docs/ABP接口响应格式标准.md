# ABP 接口响应格式标准

---

## 1. 通用约定

| 约定项 | 说明 |
|--------|------|
| 命名风格 | 所有属性使用 `camelCase` 驼峰命名 |
| 日期时间 | UTC 时间，ISO 8601 格式，以 `Z` 后缀结尾。例：`2019-08-24T14:15:22Z` |
| UUID | 标识符使用字符串形式的 UUID，如 `"497f6eca-6276-4993-bfeb-53cbbbba6f08"` |
| 枚举 | 整型数值传递，如 `"type": 0`、`"iconType": 1` |
| 可空字段 | 序列化时显式输出 `null`，不省略键 |
| 认证方式 | OAuth 2.0，authorizationCode 流程 |

---

## 2. CRUD 操作规范

### 2.1 操作与 HTTP 方法映射

| 操作 | HTTP 方法 | URL 模式 | 请求体 | 成功响应 |
|------|-----------|----------|--------|----------|
| 分页查询 | `GET` | `/api/{resource}` | Query 参数 | `200` + `PagedResultDto<T>` |
| 列表查询 | `GET` | `/api/{resource}` | Query 参数 | `200` + `T[]` |
| 按 ID 查询 | `GET` | `/api/{resource}/{id}` | — | `200` + `T` (DTO) |
| 创建 | `POST` | `/api/{resource}` | `CreateUpdateDto` | `200` + 完整 DTO |
| 全量更新 | `PUT` | `/api/{resource}/{id}` | `CreateUpdateDto` | `200` + 完整 DTO |
| 删除 | `DELETE` | `/api/{resource}/{id}` | — | `200` 或 `204`（无响应体） |

### 2.2 分页查询参数规范

| 参数 | 类型 | 必选 | 说明 |
|------|------|------|------|
| `SkipCount` | `integer(int32)` | 否 | 跳过记录数（偏移量） |
| `MaxResultCount` | `integer(int32)` | 否 | 每页最大返回条数 |
| `Sorting` | `string` | 否 | 排序字段及方向，如 `"sortId asc"` |
| 业务筛选字段 | 按实际定义 | 否 | 如 `Name`、`Type`、`IsEnabled` 等 |

---

## 3. 成功响应

### 3.1 分页查询 — `PagedResultDto<T>`

> **模型**: `Volo.Abp.Application.Dtos.PagedResultDto<T>`

```json
{
  "items": [],
  "totalCount": 0
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| `items` | `T[]` | 当前页数据列表 |
| `totalCount` | `integer(int64)` | 符合筛选条件的总记录数 |

**完整示例**（菜单分页查询）：

```json
{
  "items": [
    {
      "extraProperties": {},
      "id": "497f6eca-6276-4993-bfeb-53cbbbba6f08",
      "creationTime": "2019-08-24T14:15:22Z",
      "creatorId": "688ebf54-d343-4104-8711-82c2feac534a",
      "lastModificationTime": "2019-08-24T14:15:22Z",
      "lastModifierId": "f2b9d124-6853-48ad-ac21-7964cdf660db",
      "parentId": "70850378-7d3c-4f45-91b7-942d4dfbbd43",
      "type": 0,
      "name": "Dashboard",
      "title": "仪表盘",
      "componentPath": "/dashboard/index",
      "routePath": "/dashboard",
      "redirectPath": null,
      "icon": "dashboard",
      "iconType": 1,
      "routeType": 2,
      "permissionCode": "Dashboard.View",
      "sortId": 1,
      "isEnabled": true,
      "isCache": true,
      "isFixed": true,
      "isHidden": false,
      "isStatic": true,
      "remark": null,
      "children": []
    }
  ],
  "totalCount": 42
}
```

### 3.2 列表查询 — `T[]`

适用于无需分页的下拉选项、树形数据等场景。直接返回数组。

**示例**（菜单权限树）：

```json
[
  {
    "id": "System",
    "name": "系统管理",
    "code": "System",
    "children": [
      {
        "id": "User.View",
        "name": "用户查看",
        "code": "User.View",
        "children": []
      },
      {
        "id": "Role.Manage",
        "name": "角色管理",
        "code": "Role.Manage",
        "children": []
      }
    ]
  }
]
```

### 3.3 单个实体查询 — DTO

```json
{
  "extraProperties": {},
  "id": "497f6eca-6276-4993-bfeb-53cbbbba6f08",
  "creationTime": "2019-08-24T14:15:22Z",
  "creatorId": "688ebf54-d343-4104-8711-82c2feac534a",
  "lastModificationTime": "2020-01-10T08:30:00Z",
  "lastModifierId": "f2b9d124-6853-48ad-ac21-7964cdf660db",
  "parentId": null,
  "type": 0,
  "name": "Settings",
  "title": "系统设置",
  "componentPath": "/settings/index",
  "routePath": "/settings",
  "icon": "setting",
  "iconType": 0,
  "routeType": 2,
  "permissionCode": "Settings.View",
  "sortId": 100,
  "isEnabled": true,
  "isCache": false,
  "isFixed": false,
  "isHidden": false,
  "isStatic": false,
  "remark": "系统配置菜单",
  "children": [/* 子级菜单递归 */]
}
```

### 3.4 创建 / 更新 — 返回完整 DTO

创建和更新操作成功后返回**完整的实体 DTO**（与按 ID 查询返回格式一致），前端可直接用返回数据更新本地状态，避免再次查询。

**创建请求体** (`MenuCreateUpdateDto`)：

```json
{
  "parentId": null,
  "type": 0,
  "name": "NewMenu",
  "title": "新菜单",
  "componentPath": "/new/index",
  "redirectPath": null,
  "icon": "plus",
  "iconType": 1,
  "routeType": 2,
  "permissionCode": "NewMenu.View",
  "sortId": 200,
  "isEnabled": true,
  "isCache": true,
  "isFixed": false,
  "isHidden": false,
  "remark": null
}
```

> **注意**：创建和更新**共用同一个请求 DTO**，区别仅在于创建时无需 `id`（由服务端生成），更新时 `id` 在 URL 路径中传递。

**更新请求体** — 结构与创建完全相同（全量更新，不含 `routePath`、`isStatic` 等只读字段）：

```json
{
  "parentId": "70850378-7d3c-4f45-91b7-942d4dfbbd43",
  "type": 0,
  "name": "UpdatedMenu",
  "title": "更新后的菜单",
  "componentPath": "/updated/index",
  "redirectPath": null,
  "icon": "edit",
  "iconType": 0,
  "routeType": 2,
  "permissionCode": "UpdatedMenu.View",
  "sortId": 150,
  "isEnabled": true,
  "isCache": false,
  "isFixed": true,
  "isHidden": false,
  "remark": "已更新"
}
```

### 3.5 删除 — 无响应体

删除成功返回 **`200` 或 `204`**，无响应体（`None`）。

```http
HTTP/1.1 204 No Content
```

---

## 4. 实体审计字段

ABP 实体的标准审计字段，在所有 DTO 的响应（非请求）中出现：

| 字段 | 类型 | 约束 | 说明 |
|------|------|------|------|
| `id` | `string(uuid)` | — | 主键 |
| `extraProperties` | `object` | `read-only` | 扩展属性字典（键值对） |
| `creationTime` | `string(date-time)` | — | 创建时间（UTC ISO 8601） |
| `creatorId` | `string(uuid) \| null` | — | 创建者 ID |
| `lastModificationTime` | `string(date-time) \| null` | — | 最后修改时间 |
| `lastModifierId` | `string(uuid) \| null` | — | 最后修改者 ID |

> **约定**：
> - 审计字段为**只读**，不出现在 `CreateUpdateDto` 请求体中。
> - `routePath`、`isStatic` 等系统计算字段也不出现在请求体中。
> - `children`（树形结构的子节点）仅在响应中返回，请求时通过 `parentId` 表达层级关系。

---

## 5. 错误响应

所有非 2xx 状态码统一返回 `Volo.Abp.Http.RemoteServiceErrorResponse`。

### 5.1 顶层结构

```json
{
  "error": {
    "code": "string",
    "message": "string",
    "details": "string",
    "data": {},
    "validationErrors": []
  }
}
```

### 5.2 `RemoteServiceErrorResponse`

| 字段 | 类型 | 说明 |
|------|------|------|
| `error` | `RemoteServiceErrorInfo` | 错误信息对象 |

### 5.3 `RemoteServiceErrorInfo`

| 字段 | 类型 | 说明 |
|------|------|------|
| `code` | `string \| null` | 业务错误码，用于程序判断 |
| `message` | `string \| null` | 面向用户的错误描述 |
| `details` | `string \| null` | 面向开发者的详细信息（堆栈等） |
| `data` | `object \| null` | 附加错误数据（任意键值对） |
| `validationErrors` | `RemoteServiceValidationErrorInfo[] \| null` | 字段级校验错误列表 |

### 5.4 `RemoteServiceValidationErrorInfo`

| 字段 | 类型 | 说明 |
|------|------|------|
| `message` | `string \| null` | 校验错误描述 |
| `members` | `string[] \| null` | 出错的属性/字段名称列表 |

### 5.5 HTTP 状态码映射

| 状态码 | 含义 | 返回模型 | 触发场景 |
|--------|------|----------|----------|
| `200` | OK | 业务 DTO | 操作成功 |
| `204` | No Content | — | 删除成功（无响应体） |
| `400` | Bad Request | `RemoteServiceErrorResponse` | 参数校验失败 / 业务规则冲突 |
| `401` | Unauthorized | `RemoteServiceErrorResponse` | 未认证或 Token 过期 |
| `403` | Forbidden | `RemoteServiceErrorResponse` | 无访问权限 |
| `404` | Not Found | `RemoteServiceErrorResponse` | 资源不存在 |
| `500` | Internal Server Error | `RemoteServiceErrorResponse` | 服务器未处理异常 |
| `501` | Not Implemented | `RemoteServiceErrorResponse` | 功能未实现 |

---

## 6. 错误响应示例

### 6.1 字段校验失败（400）

```json
{
  "error": {
    "code": null,
    "message": "请求参数校验失败",
    "details": null,
    "data": null,
    "validationErrors": [
      {
        "message": "菜单名称不能为空",
        "members": ["name"]
      },
      {
        "message": "同级菜单名称不能重复",
        "members": ["name", "parentId"]
      }
    ]
  }
}
```

### 6.2 业务规则冲突（400）

```json
{
  "error": {
    "code": "MuckTracing:Menu:DuplicateName",
    "message": "同级菜单下已存在同名菜单",
    "details": null,
    "data": {
      "conflictField": "name",
      "conflictValue": "Dashboard"
    },
    "validationErrors": null
  }
}
```

### 6.3 未认证（401）

```json
{
  "error": {
    "code": null,
    "message": "未登录或登录已过期",
    "details": null,
    "data": null,
    "validationErrors": null
  }
}
```

### 6.4 权限不足（403）

```json
{
  "error": {
    "code": null,
    "message": "您没有执行此操作的权限",
    "details": "Required permission: MenuManagement.Delete",
    "data": null,
    "validationErrors": null
  }
}
```

### 6.5 资源未找到（404）

```json
{
  "error": {
    "code": null,
    "message": "未找到指定的菜单",
    "details": "Entity type: Menu, id: 497f6eca-6276-4993-bfeb-53cbbbba6f08",
    "data": null,
    "validationErrors": null
  }
}
```

### 6.6 删除冲突 — 如存在子菜单时删除父菜单（400）

```json
{
  "error": {
    "code": "MuckTracing:Menu:HasChildren",
    "message": "该菜单下存在子菜单，无法删除",
    "details": null,
    "data": {
      "childCount": 5
    },
    "validationErrors": null
  }
}
```

---

## 7. 请求 / 响应字段对照

### 7.1 DTO 读写字段分离

| 字段类别 | 出现位置 | 示例 |
|----------|----------|------|
| **输入字段**（请求体） | `CreateUpdateDto` | `name`、`title`、`parentId`、`sortId`、`isEnabled` 等 |
| **输出字段**（响应体） | `Dto` | 所有输入字段 + `id` + 审计字段 + `children` + `routePath` + `isStatic` |
| **只读字段** | 仅 `Dto`（响应） | `id`、`creationTime`、`creatorId`、`lastModificationTime`、`lastModifierId`、`extraProperties`、`routePath`、`isStatic` |

### 7.2 Create / Update 请求 DTO 约定

- 创建（POST）和更新（PUT）**共享**同一个 `XxxCreateUpdateDto`。
- 请求 DTO 仅包含**用户可编辑**的字段。
- 系统计算字段（`routePath`、`isStatic` 等）和审计字段不出现在请求体。
- `parentId` 为 `null` 表示根节点；为具体 UUID 表示子节点。
- 全量更新，所有必填字段均需传递。

---

## 8. 类型速查

### 8.1 ABP 框架类型

| ABP 类型 | 用途 | 核心字段 |
|----------|------|----------|
| `PagedResultDto<T>` | 分页查询成功响应 | `items`、`totalCount` |
| `RemoteServiceErrorResponse` | 所有错误响应的顶层包装 | `error` |
| `RemoteServiceErrorInfo` | 错误详情 | `code`、`message`、`details`、`data`、`validationErrors` |
| `RemoteServiceValidationErrorInfo` | 单条字段校验错误 | `message`、`members` |

### 8.2 本系统业务枚举

**`MenuType`** — 菜单类型：

| 值 | 说明 |
|----|------|
| `0` | 目录 |
| `1` | 菜单 |
| `2` | 按钮/权限点 |

**`IconType`** — 图标类型：

| 值 | 说明 |
|----|------|
| `0` | Ant Design 图标 |
| `1` | 自定义 SVG |
| `2` | 图片 URL |

**`RouteType`** — 路由类型：

| 值 | 说明 |
|----|------|
| `0` | 普通路由 |
| `1` | 内嵌路由 |
| `2` | 外链路由 |

---

## 9. 前端处理建议

### 9.1 TypeScript 类型定义

```typescript
// === 通用类型 ===

/** 分页查询响应 */
interface PagedResult<T> {
  items: T[];
  totalCount: number;
}

/** 错误响应 */
interface AbpErrorResponse {
  error: {
    code: string | null;
    message: string | null;
    details: string | null;
    data: Record<string, unknown> | null;
    validationErrors: Array<{
      message: string;
      members: string[];
    }> | null;
  };
}

/** 实体审计字段 */
interface AuditedEntity {
  id: string;
  extraProperties: Record<string, unknown> | null;
  creationTime: string;
  creatorId: string | null;
  lastModificationTime: string | null;
  lastModifierId: string | null;
}

// === 业务类型 ===

/** 菜单 DTO（响应） */
interface MenuDto extends AuditedEntity {
  parentId: string | null;
  type: MenuType;
  name: string;
  title: string;
  componentPath: string | null;
  routePath: string | null;
  redirectPath: string | null;
  icon: string | null;
  iconType: IconType;
  routeType: RouteType;
  permissionCode: string;
  sortId: number;
  isEnabled: boolean;
  isCache: boolean;
  isFixed: boolean;
  isHidden: boolean;
  isStatic: boolean;
  remark: string | null;
  children: MenuDto[] | null;
}

/** 菜单创建/更新请求 */
interface MenuCreateUpdateDto {
  parentId: string | null;
  type: MenuType;
  name: string;
  title: string;
  componentPath: string | null;
  redirectPath: string | null;
  icon: string | null;
  iconType: IconType;
  routeType: RouteType;
  permissionCode: string;
  sortId: number;
  isEnabled: boolean;
  isCache: boolean;
  isFixed: boolean;
  isHidden: boolean;
  remark: string | null;
}

/** 权限节点 */
interface MenuPermissionDto {
  id: string;
  name: string;
  code: string;
  children: MenuPermissionDto[] | null;
}

enum MenuType { Directory = 0, Menu = 1, Button = 2 }
enum IconType { AntDesign = 0, Svg = 1, ImageUrl = 2 }
enum RouteType { Normal = 0, Embedded = 1, ExternalLink = 2 }
```

### 9.2 拦截器处理逻辑

```
HTTP 200 + response.items / response.totalCount → 分页成功，取 PagedResultDto<T>
HTTP 200 + Array.isArray(response)             → 列表成功，取 T[]
HTTP 200 + response.id                         → 单实体 / 创建 / 更新成功，取 DTO
HTTP 204                                       → 删除成功
HTTP 400 + error.validationErrors != null      → 字段校验，按 members 绑定到表单
HTTP 400 + error.code != null                  → 业务规则冲突，Toast 提示 message
HTTP 401                                       → 跳转登录页
HTTP 403                                       → Toast："无权限"
HTTP 404                                       → Toast："资源不存在"
HTTP 500                                       → Toast："服务器异常"，dev 环境可展示 details
```

### 9.3 CRUD 调用示例

```typescript
// 分页查询
const list = await api.get<PagedResult<MenuDto>>('/api/menu-management/menus', {
  params: { Name: '仪表', Type: 1, IsEnabled: true, SkipCount: 0, MaxResultCount: 20 }
});

// 按 ID 查询
const detail = await api.get<MenuDto>(`/api/menu-management/menus/${id}`);

// 创建
const created = await api.post<MenuDto>('/api/menu-management/menus', payload);
// 直接用 created 更新本地列表，无需二次请求

// 更新
const updated = await api.put<MenuDto>(`/api/menu-management/menus/${id}`, payload);

// 删除
await api.delete(`/api/menu-management/menus/${id}`);
// 200/204 即成功，无需解析响应体
```

---

## 10. 设计决策速览

| 决策 | 约定 |
|------|------|
| 创建/更新共享 DTO | `XxxCreateUpdateDto`，PUT 为全量更新 |
| 树形结构传输 | 通过 `parentId` 写入，通过 `children` 读取 |
| 枚举序列化 | 整型值，不传字符串名 |
| 可空字段 | 序列化时保留键，值为 `null` |
| 分页删除 | 单条删除；批量操作需独立接口 |
| 请求体 | JSON，`Content-Type: application/json` |
| 响应体空 | DELETE 返回 200/204 无响应体 |
