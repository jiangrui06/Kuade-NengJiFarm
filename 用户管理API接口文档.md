# 用户管理 API 接口文档

**基础路径：** `/api/back-user`

**通用响应格式：**
```json
{
  "code": 200,
  "message": "操作成功",
  "data": { ... }
}
```

---

## 核心设计原则：数据全量来自数据库

本接口所有**用户数据、角色数据均从数据库表动态读取**，后端代码无任何硬编码状态映射。

| 数据来源 | 数据库表 | 说明 |
|---|---|---|
| 用户信息 | `user` | 用户ID、手机号、昵称、角色等 |
| 角色信息 | `role` | 角色ID、角色名称 |

---

## 一、数据库用户相关表

### 1.1 user 表

| 字段 | 类型 | 说明 |
|---|---|---|
| `user_id` | int (PK) | 用户ID |
| `user_guid` | varchar | 用户全局唯一标识 |
| `phone_number` | varchar | 手机号 |
| `real_name` | varchar | 真实姓名 |
| `wx_nickname` | varchar | 微信昵称 |
| `wx_openid` | varchar | 微信 OpenID |
| `wx_image` | varchar | 微信头像 |
| `gender` | varchar | 性别（保密/男/女） |
| `password_hash` | varchar | 密码哈希 |
| `role_id` | int (FK → role) | 角色ID |
| `register_time` | datetime | 注册时间 |

### 1.2 role 表

| 字段 | 类型 | 说明 |
|---|---|---|
| `role_id` | int (PK) | 角色ID |
| `role_name` | varchar(50) | 角色名称（如：管理员、普通用户） |

> **数据动态性：** `role` 表增删改角色后，API 自动适配，后端无需更改代码。用户详情中的 `roleName` 通过 `user.role_id` 关联 `role.role_name` 动态读取。

---

## 二、获取用户列表

### 2.1 接口说明

从 `user` 表分页查询用户列表，关联 `role` 表获取角色名称，所有数据从数据库动态读取。

```
GET /api/back-user/list?pageNum=1&pageSize=20&keyword=xxx
```

**同时支持：** `GET /api/user/list`（别名路由）

### 2.2 查询参数

| 参数 | 类型 | 必填 | 默认值 | 说明 |
|---|---|---|---|---|
| pageNum | int | 否 | 1 | 页码 |
| pageSize | int | 否 | 20 | 每页条数（最大100） |
| keyword | string | 否 | - | 模糊搜索（昵称/手机号） |

### 2.3 响应结构

```json
{
  "code": 200,
  "message": "获取成功",
  "data": {
    "records": [ ... ],
    "total": 100,
    "pageNum": 1,
    "pageSize": 20,
    "pages": 5
  }
}
```

### 2.4 records 字段说明

| 字段 | 类型 | 数据来源 | 说明 |
|---|---|---|---|
| id | string | `user.user_id` | 用户ID（字符串格式） |
| Guid | string | `user.user_guid` | 用户全局唯一标识 |
| phone | string | `user.phone_number` | 手机号 |
| nickname | string | `user.wx_nickname` | 微信昵称 |
| gender | string/null | `user.gender` | 性别 |
| WxOpenid | string | `user.wx_openid` | 微信 OpenID |
| role | string | **`role.role_name`** | **从数据库动态读取** |
| selected | bool | 前端控制 | 是否选中（默认 false） |
| userType | string | 固定值 | `user` |
| loginTime | string/null | `user.register_time` | 注册时间 |

> **`role` 数据流：** `user.role_id` → JOIN `role.role_id` → `role.role_name` → API 响应。数据库 `role` 表修改角色名称后，API 自动返回新名称。

### 2.5 响应示例

```json
{
  "code": 200,
  "message": "获取成功",
  "data": {
    "records": [
      {
        "id": "1",
        "Guid": "a1b2c3d4-...",
        "phone": "13800138001",
        "nickname": "张三",
        "gender": "男",
        "WxOpenid": "wx_xxx",
        "role": "管理员",
        "selected": false,
        "userType": "user",
        "loginTime": "2026-01-01 10:00"
      }
    ],
    "total": 50,
    "pageNum": 1,
    "pageSize": 20,
    "pages": 3
  }
}
```

---

## 三、获取用户详情

### 3.1 按用户ID查询

```
GET /api/back-user/detail?id=1
```

### 3.2 按用户ID路径参数查询

```
GET /api/back-user/1
```

### 3.3 按 UserGuid 查询

```
GET /api/back-user/detail/guid?guid=xxx-xxx-xxx
```

### 3.4 查询参数

| 参数 | 类型 | 必填 | 说明 |
|---|---|---|---|
| id | int/string | 是 | 用户ID（detail接口） |
| guid | string | 是 | 用户全局标识（detail/guid接口） |

### 3.5 响应字段说明

| 字段 | 类型 | 数据来源 | 说明 |
|---|---|---|---|
| id | int | `user.user_id` | 用户ID |
| Guid | string | `user.user_guid` | 用户全局标识 |
| phone | string | `user.phone_number` | 手机号 |
| nickname | string | `user.real_name` 或 `user.wx_nickname` | 显示名称 |
| avatar | string | `user.wx_image` | 头像URL |
| gender | string | `user.gender` | 性别 |
| loginTime | string | `user.register_time` | 注册时间 |
| realName | string | `user.real_name` | 真实姓名 |
| wxOpenId | string | `user.wx_openid` | 微信 OpenID |
| roleId | int | `user.role_id` | 角色ID |
| roleName | string | **`role.role_name`** | **从数据库动态读取** |

> **`roleName` 数据流：** 查询时 `.Include(u => u.Role)` 加载 Role 导航属性，取 `Role.RoleName`。数据库修改角色名称后自动生效。

### 3.6 响应示例

```json
{
  "code": 200,
  "message": "获取成功",
  "data": {
    "id": 1,
    "Guid": "a1b2c3d4-...",
    "phone": "13800138001",
    "nickname": "张三",
    "avatar": "https://example.com/avatar.jpg",
    "gender": "男",
    "loginTime": "2026年01月01日 10:00",
    "realName": "张三",
    "wxOpenId": "wx_xxx",
    "roleId": 1,
    "roleName": "管理员"
  }
}
```

---

## 四、新增用户

### 4.1 接口说明

向 `user` 表写入新用户记录，密码使用 `PasswordService` 哈希后存储。

```
POST /api/back-user/add
Content-Type: application/json
```

### 4.2 请求体参数

| 字段 | 类型 | 必填 | 说明 |
|---|---|---|---|
| Phone | string | 是 | 手机号（11位数字，唯一） |
| RealName | string | 是 | 真实姓名 |
| Gender | string | 是 | 性别 |
| RoleId | int | 否 | 角色ID（默认 2=普通用户） |
| Password | string | 是 | 密码 |

### 4.3 业务校验

- 手机号格式验证（11位数字）
- 手机号唯一性检查（`user` 表已存在则报错）
- `UserGuid` 由后端自动生成（`Guid.NewGuid()`）

### 4.4 请求示例

```json
{
  "Phone": "13900139001",
  "RealName": "李四",
  "Gender": "男",
  "RoleId": 2,
  "Password": "123456"
}
```

### 4.5 成功响应

```json
{
  "code": 200,
  "message": "添加成功"
}
```

---

## 五、编辑用户

### 5.1 接口说明

按 `UserGuid` 更新 `user` 表中对应用户的信息，仅更新前端发送的字段。

```
POST /api/back-user/edit
Content-Type: application/json
```

### 5.2 请求体参数

| 字段 | 类型 | 必填 | 说明 |
|---|---|---|---|
| Guid | string | 是 | 用户GUID（标识要编辑的用户） |
| nickname | string | 否 | 昵称（仅编辑时传） |
| gender | string | 否 | 性别（仅编辑时传） |
| role | string | 否 | 角色名称（仅编辑时传，通过 `role.role_name` 查询角色ID） |

### 5.3 请求示例

```json
{
  "Guid": "a1b2c3d4-...",
  "nickname": "新昵称",
  "gender": "女",
  "role": "普通用户"
}
```

### 5.4 成功响应

```json
{
  "code": 200,
  "message": "编辑成功"
}
```

---

## 六、删除用户

### 6.1 单个删除

```
POST /api/back-user/delete
Content-Type: application/json
```

#### 请求体

| 字段 | 类型 | 必填 | 说明 |
|---|---|---|---|
| Guid | string | 是 | 用户GUID |

#### 请求示例

```json
{
  "Guid": "a1b2c3d4-..."
}
```

#### 成功响应

```json
{
  "code": 200,
  "message": "删除成功"
}
```

---

### 6.2 批量删除

```
POST /api/back-user/deleteBatch
Content-Type: application/json
```

#### 请求体

| 字段 | 类型 | 必填 | 说明 |
|---|---|---|---|
| ids | string[] | 是 | 用户GUID列表 |

#### 请求示例

```json
{
  "ids": ["guid-001", "guid-002", "guid-003"]
}
```

#### 业务逻辑

- 通过 `WHERE user_guid IN (...)` 批量查询
- 使用 `RemoveRange` 一次性删除
- 返回实际删除数量

#### 成功响应

```json
{
  "code": 200,
  "message": "批量删除成功，共删除 3 个用户"
}
```

#### 错误响应

```json
{
  "code": 400,
  "message": "删除用户ID列表不能为空"
}
```

```json
{
  "code": 400,
  "message": "未找到要删除的用户"
}
```

---

## 七、获取角色列表

### 7.1 接口说明

从 `role` 表读取所有角色，数据动态从数据库获取。

```
GET /api/back-user/roles
```

### 7.2 响应结构

```json
{
  "code": 200,
  "message": "获取成功",
  "data": {
    "records": [
      { "roleId": 1, "roleName": "管理员" },
      { "roleId": 2, "roleName": "普通用户" }
    ]
  }
}
```

### 7.3 字段说明

| 字段 | 类型 | 数据来源 | 说明 |
|---|---|---|---|
| roleId | int | `role.role_id` | 角色ID |
| roleName | string | `role.role_name` | 角色名称 |

---

## 八、数据流全景图

```
┌─────────────────────────────────────────────────────────────┐
│                        前端页面                               │
│  用户列表 / 用户详情 / 新增 / 编辑 / 删除 / 批量删除          │
└──────────────────────────┬──────────────────────────────────┘
                           │ HTTP JSON
                           ▼
┌─────────────────────────────────────────────────────────────┐
│                API 后端 (BackUserController + BackUserService)│
│                                                              │
│  list (GET):                                                 │
│    └─ user ──JOIN──> role.role_name (角色名称动态读取)       │
│    └─ 支持 keyword 模糊搜索（昵称/手机号）                   │
│    └─ 分页查询（pageNum, pageSize）                           │
│                                                              │
│  detail (GET):                                               │
│    └─ user (Include Role) → roleName 动态读取                │
│                                                              │
│  add (POST):                                                 │
│    └─ 写入 user 表，密码哈希后存储                           │
│                                                              │
│  edit (POST):                                                │
│    └─ 更新 user 表（按 Guid 定位，只更新传的字段）            │
│                                                              │
│  delete (POST):                                              │
│    └─ 删除 user 表（按 Guid）                                 │
│                                                              │
│  deleteBatch (POST):                                         │
│    └─ 批量删除 user 表（WHERE user_guid IN (...)）            │
│                                                              │
│  roles (GET):                                                │
│    └─ 读取 role 表全部角色                                   │
└──────────────────────────┬──────────────────────────────────┘
                           │ Entity Framework Core
                           ▼
┌─────────────────────────────────────────────────────────────┐
│                      数据库 (MySQL)                           │
│                                                              │
│  user ───────── 用户表                                        │
│  role ───────── 角色表（增删改后 API 自动适配）                │
└─────────────────────────────────────────────────────────────┘
```

---

## 九、接口汇总

| 接口 | 方法 | 路径 | 说明 |
|---|---|---|---|
| 获取用户列表 | GET | `/api/back-user/list` | 分页查询，支持关键词搜索 |
| 获取用户列表（别名） | GET | `/api/user/list` | 同上 |
| 获取用户详情 | GET | `/api/back-user/detail?id=xxx` | 按用户ID查询 |
| 获取用户详情（路径） | GET | `/api/back-user/{id}` | 按路径参数ID查询 |
| 获取用户详情（GUID） | GET | `/api/back-user/detail/guid?guid=xxx` | 按GUID查询 |
| 新增用户 | POST | `/api/back-user/add` | 写入新用户 |
| 编辑用户 | POST | `/api/back-user/edit` | 按GUID更新用户 |
| 删除用户 | POST | `/api/back-user/delete` | 单个删除 |
| 批量删除用户 | POST | `/api/back-user/deleteBatch` | 批量删除（P1新增） |
| 获取角色列表 | GET | `/api/back-user/roles` | 所有角色 |

---

## 十、错误码

| HTTP 状态码 | 说明 |
|---|---|
| 200 | 请求成功（业务错误在 `code`/`message` 中返回） |
| 400 | 参数错误 |
| 401 | 未授权（账号未注册/密码错误） |
| 403 | 账号已禁用 |
| 404 | 用户不存在 |
| 500 | 服务器内部错误 |

### 业务错误 message 示例

| 错误消息 | 触发条件 |
|---|---|
| `手机号不能为空` | 添加用户时未传手机号 |
| `手机号格式不正确` | 手机号不是11位数字 |
| `手机号已存在` | 添加用户时手机号已注册 |
| `用户ID不能为空` | 删除/编辑时未传 Guid |
| `用户不存在，请刷新列表` | 删除时用户已被其他管理员删除 |
| `删除用户ID列表不能为空` | 批量删除时 ids 为空 |
| `未找到要删除的用户` | 批量删除时所有 guid 均不匹配 |
| `账号和密码不能为空` | 登录时未传账号密码 |
| `该账号未注册` | 登录账号不存在 |
| `账号或密码不正确` | 登录密码错误 |
| `账号已禁用，请联系管理员` | 用户状态为禁用 |

---

## 十一、对前端的特别说明

### 11.1 角色数据的动态性

`roleName` 和列表中的 `role` 字段均来自 `role` 表：
- 数据库修改角色名称后，API 立即返回新名称
- 前端不应硬编码角色名称进行判断

### 11.2 用户唯一标识

- 列表接口使用 `Guid` 字段作为用户的唯一标识
- 删除、编辑、详情查询均使用 `Guid` 而非 `id`
- 批量删除传入的 `ids` 数组也是 Guid 列表

### 11.3 `userType` 字段

固定为 `"user"`，表示平台注册用户。预留用于区分管理员（staff）和普通用户。
