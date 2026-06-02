# 餐桌管理 API 接口文档

> **版本：** v2.1（2026-06-02）
> **变更：** 状态切换接口限定仅"使用中"↔"停用"，"删除"只能通过独立删除按钮触发
> **基础路径：** `/api/table` / `/api/dining-table`

---

## 设计说明

- 餐桌删除为**软删除**，仅将 `table_status` 改为"删除"状态
- 已删除或停用的桌号可**复用**（新增或编辑时自动接管）
- 状态数据全部从 `dining_table_status_dict` 表动态获取，无硬编码
- `scope=form` 和 `scope=toggle` 均通过 `is_toggle` 字段过滤（`is_toggle=1` 的状态才能出现在状态下拉和切换中）
- 删除状态 `is_toggle=0`，只能通过独立删除接口触发
- `GET /api/dining-table/statuses` 支持 `?scope` 参数，不同页面获取对应状态列表

### 状态来源

| statusId | statusName | list 页面 | form 页面 | toggle 页面 |
|:--------:|-----------|:---------:|:---------:|:----------:|
| 1 | 使用中 | ✅ | ✅ | ✅ |
| 2 | 删除 | ✅ | ❌ | ❌ |
| 3 | 停用 | ✅ | ✅ | ✅ |

---

## 接口一：获取状态列表

### 基本信息

```
GET /api/dining-table/statuses
```

### 查询参数

| 字段 | 类型 | 必填 | 说明 |
|------|------|:----:|------|
| scope | string | 否 | `list`=全部状态，`form`/`toggle`=仅 `is_toggle=1` 的状态（不传则返回全部） |

### 请求示例

```
GET /api/dining-table/statuses?scope=form
```

### 成功响应（不传 scope）

```json
{
  "code": 0,
  "message": "success",
  "data": [
    { "statusId": 1, "statusName": "使用中" },
    { "statusId": 2, "statusName": "删除" },
    { "statusId": 3, "statusName": "停用" }
  ]
}
```

### 成功响应（scope=form）

```json
{
  "code": 0,
  "message": "success",
  "data": [
    { "statusId": 1, "statusName": "使用中" },
    { "statusId": 3, "statusName": "停用" }
  ]
}
```

### 成功响应（scope=toggle）

```json
{
  "code": 0,
  "message": "success",
  "data": [
    { "statusId": 1, "statusName": "使用中" },
    { "statusId": 3, "statusName": "停用" }
  ]
}
```

---

## 接口二：获取餐桌列表（基础）

### 基本信息

```
GET /api/dining-table/list
```

### 查询参数

| 字段 | 类型 | 必填 | 说明 |
|------|------|:----:|------|
| pageNum | int | 否 | 页码（默认 1） |
| pageSize | int | 否 | 每页条数（默认 15，最大 100） |
| keyword | string | 否 | 搜索关键字（匹配桌号） |

### 成功响应

```json
{
  "code": 0,
  "message": "success",
  "data": {
    "records": [
      {
        "id": "1号桌",
        "tableNo": "1号桌",
        "seatCount": 4,
        "tableStatus": 1,
        "statusName": "使用中",
        "qrCodeImageUrl": "/images/qrcode/table_a001.png",
        "createdAt": "2026-05-20 10:30:00"
      }
    ],
    "total": 10,
    "pageNum": 1,
    "pageSize": 15,
    "pages": 1
  }
}
```

---

## 接口三：新增餐桌

### 基本信息

```
POST /api/dining-table/add
```

### 请求体

| 字段 | 类型 | 必填 | 说明 |
|------|------|:----:|------|
| tableNo | string | 是 | 桌号（如 "1号桌"，纯数字自动补后缀） |
| seatCount | int | 否 | 座位数（默认 4） |
| tableStatus | int | 否 | 状态 ID（默认 1 = 使用中） |
| qrCodeImageUrl | string | 否 | 二维码图片 URL |

### 复用逻辑

```
新增请求 → 查桌号是否存在？
  ├── 不存在 → 新建记录 ✓
  ├── 存在且状态 = 删除/停用 → 更新该记录为新数据，复用桌号 ✓
  └── 存在且状态 = 使用中 → 返回 400 "桌号已存在" ✗
```

### 成功响应

```json
{
  "code": 0,
  "message": "success",
  "data": { "id": "1号桌" }
}
```

### 错误响应

```json
{ "code": 400, "message": "桌号 '1号桌' 已存在" }
```

---

## 接口四：删除餐桌（软删除）

### 基本信息

```
POST /api/dining-table/delete
```

### 请求体

| 字段 | 类型 | 必填 | 说明 |
|------|------|:----:|------|
| id | string | 是 | 桌号 |

### 说明
- 仅将 `table_status` 设为"删除"，不物理删除记录
- 被删除的桌号可通过新增或编辑复用

### 成功响应

```json
{ "code": 0, "message": "success", "data": "删除成功" }
```

### 错误响应

```json
{ "code": 400, "message": "餐桌不存在" }
```

---

## 接口五：获取餐桌列表（管理端完整版）

### 基本信息

```
GET /api/table/list
```

### 查询参数

| 字段 | 类型 | 必填 | 说明 |
|------|------|:----:|------|
| pageNum | int | 否 | 页码（默认 1） |
| pageSize | int | 否 | 每页条数（默认 10，最大 100） |
| keyword | string | 否 | 搜索关键字 |
| status | string | 否 | 按状态名称筛选（如 "使用中"） |

### 说明
- 默认过滤掉"删除"状态的餐桌，除非 `status=删除` 明确查询

### 成功响应

```json
{
  "code": 200,
  "message": "success",
  "data": {
    "records": [
      {
        "id": "1号桌",
        "tableno": "1号桌",
        "capacity": 4,
        "statusId": 1,
        "status": "使用中",
        "createTime": "2026-05-20 10:30:00"
      }
    ],
    "total": 10,
    "pages": 1,
    "pageNum": 1
  }
}
```

---

## 接口六：获取餐桌详情

### 基本信息

```
GET /api/table/detail/{id}
```

### 路径参数

| 字段 | 类型 | 必填 | 说明 |
|------|------|:----:|------|
| id | string | 是 | 桌号 |

### 说明
- 状态为"停用"或"删除"的桌台返回 null，阻止扫码使用

### 成功响应

```json
{
  "code": 200,
  "message": "success",
  "data": {
    "id": "1号桌",
    "tableno": "1号桌",
    "capacity": 4,
    "status": 1,
    "createTime": "2026-05-20 10:30:00",
    "qrCodeUrl": "https://api.nengjifarm.com/images/qrcode/table_a001.png"
  }
}
```

---

## 接口七：新增餐桌（管理端，含二维码）

### 基本信息

```
POST /api/table/add
```

### 请求体

| 字段 | 类型 | 必填 | 说明 |
|------|------|:----:|------|
| tableno | string | 是 | 桌号 |
| capacity | int | 否 | 容纳人数（1-30） |
| status | int | 否 | 状态 ID（默认 1） |

### 复用逻辑

同接口三，但会重新生成二维码。

### 成功响应

```json
{
  "code": 200,
  "message": "新增成功",
  "data": {
    "id": "1号桌",
    "tableno": "1号桌",
    "capacity": 4,
    "status": 1,
    "qrCodeUrl": "https://api.nengjifarm.com/images/qrcode/table_a001.png"
  }
}
```

---

## 接口八：编辑餐桌

### 基本信息

```
POST /api/table/edit
```

### 请求体

| 字段 | 类型 | 必填 | 说明 |
|------|------|:----:|------|
| id | string | 是 | 当前桌号 |
| tableno | string | 否 | 新桌号 |
| capacity | int | 否 | 新容纳人数 |
| status | int | 否 | 新状态 ID |

### 复用逻辑

```
编辑请求 → 检查新桌号是否被占用？
  ├── 新桌号不存在 → 直接修改桌号 ✓
  ├── 新桌号存在且状态 = 删除/停用 → 物理删除旧记录，当前桌台接管 ✓
  └── 新桌号存在且状态 = 使用中 → 返回 409 "餐桌号已存在" ✗
```

### 成功响应

```json
{
  "code": 200,
  "message": "修改成功",
  "data": {
    "id": "2号桌",
    "tableno": "2号桌",
    "capacity": 6,
    "status": 1,
    "qrCodeUrl": "https://api.nengjifarm.com/images/qrcode/table_a002.png"
  }
}
```

### 错误响应

```json
{ "code": 409, "message": "餐桌号 '2号桌' 已存在" }
```

---

## 接口九：删除餐桌（管理端）

### 基本信息

```
POST /api/table/delete
```

### 请求体

| 字段 | 类型 | 必填 | 说明 |
|------|------|:----:|------|
| id | string | 是 | 桌号 |

### 成功响应

```json
{ "code": 200, "message": "删除成功", "data": null }
```

---

## 接口十：更新餐桌状态

### 基本信息

```
POST /api/table/status
```

### 请求体

| 字段 | 类型 | 必填 | 说明 |
|------|------|:----:|------|
| tableno | string | 是 | 桌号 |
| status | int | 是 | 目标状态 ID（从状态列表获取） |

### 说明
- 仅允许切换为"使用中"或"停用"，不可通过此接口设为"删除"
- 删除操作请调用 `POST /api/table/delete`

### 成功响应

```json
{
  "code": 200,
  "message": "状态更新成功",
  "data": {
    "tableno": "1号桌",
    "status": 3
  }
}
```

---

## 接口汇总

| 接口 | 方法 | 路径 | 说明 |
|------|:----:|------|------|
| 获取状态列表 | GET | `/api/dining-table/statuses?scope=` | 支持 `?scope=form/toggle` 仅返回 `is_toggle=1` 的状态 |
| 餐桌列表 | GET | `/api/dining-table/list` | 基础分页列表 |
| 新增餐桌 | POST | `/api/dining-table/add` | 含软删除复用 |
| 删除餐桌 | POST | `/api/dining-table/delete` | 软删除 |
| 管理端列表 | GET | `/api/table/list` | 分页+状态筛选 |
| 餐桌详情 | GET | `/api/table/detail/{id}` | 单条详情，停用/删除返回 null |
| 管理端新增 | POST | `/api/table/add` | 含二维码生成+软删除复用 |
| 编辑餐桌 | POST | `/api/table/edit` | 含桌号变更+二维码重生成+软删除复用 |
| 管理端删除 | POST | `/api/table/delete` | 软删除 |
| 更新状态 | POST | `/api/table/status` | 仅"使用中"↔"停用"，删除走独立接口 |

---

## 前端页面使用参考

| 页面 | 状态接口参数 | 用于 |
|------|:----------:|------|
| 列表页（table.html） | 不传 scope 或 `scope=list` | 筛选下拉+行内状态修改 |
| 新增/编辑页（table-form.html） | `scope=form` | 状态下拉（仅 `is_toggle=1` 的状态） |
| 状态切换按钮 | `scope=toggle` | 仅 `is_toggle=1` 的状态 |
