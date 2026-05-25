# BackUser（用户管理）API 接口文档

> 基础路径：`/api/back-user`
> 
> 后端控制器：`BackUserController.cs`
> 
> 服务层：`BackUserService.cs`

---

## 目录

| # | 接口 | 方法 | 路径 | 用途 |
|---|------|------|------|------|
| 1 | 用户列表 | GET | `/api/back-user/list` | 分页查询用户 |
| 2 | 新增用户 | POST | `/api/back-user/add` | 新增管理员/员工 |
| 3 | 编辑用户 | POST | `/api/back-user/edit` | 修改用户信息 |
| 4 | 用户详情(Guid) | GET | `/api/back-user/detail` | 根据 Guid 查详情 |
| 5 | 用户详情(ID) | GET | `/api/back-user/{userId:int}` | 根据 UserId 查详情 |
| 6 | 用户详情(Guid) | GET | `/api/back-user/detail/guid` | 根据 guid 参数查详情 |
| 7 | 删除用户 | POST | `/api/back-user/delete` | 删除单个用户 |
| 8 | 批量删除 | POST | `/api/back-user/deleteBatch` | 批量删除用户 |
| 9 | 角色列表 | GET | `/api/back-user/roles` | 获取所有角色 |
| 10 | 登录 | POST | `/api/back-user/login` | 管理员登录 |
| 11 | 登出 | POST | `/api/back-user/logout` | 注销 |

---

## 1. 用户列表（分页）

```
GET /api/back-user/list?keyword=&pageNum=1&pageSize=20
```

### 请求参数（Query）

| 参数 | 类型 | 必填 | 默认值 | 说明 |
|------|------|------|--------|------|
| keyword | string | 否 | - | 搜索关键字（匹配昵称/手机号） |
| pageNum | int | 否 | 1 | 页码（从 1 开始） |
| pageSize | int | 否 | 20 | 每页条数（最大 100） |

### 响应示例

```json
{
  "code": 200,
  "message": "获取成功",
  "data": {
    "pageNum": 1,
    "pageSize": 20,
    "total": 50,
    "pages": 3,
    "records": [
      {
        "id": "U20260521120019",
        "guid": "a1b2c3d4-...",
        "phone": "13800138000",
        "nickname": "张三",
        "gender": "男",
        "address": "xxx",
        "wxOpenid": "o_xxx",
        "role": "员工",
        "selected": false,
        "userType": "staff",
        "loginTime": "2026-05-21 10:30"
      }
    ]
  }
}
```

---

## 2. 新增用户

```
POST /api/back-user/add
Content-Type: application/json
```

### 请求体

```json
{
  "phone": "13800138000",
  "realName": "张三",
  "nickname": "张三",
  "gender": "男",
  "roleId": 2,
  "password": "123456"
}
```

| 字段 | 类型 | 必填 | 默认值 | 说明 |
|------|------|:----:|:------:|------|
| phone | string | **是** | - | 11 位手机号，唯一（已存在报错） |
| realName | string | **是** | - | 真实姓名 |
| nickname | string | 否 | `realName` | 显示昵称。**不传则自动用 `realName` 作为昵称** |
| gender | string | **是** | - | 性别（男/女/保密） |
| roleId | int | 否 | `2`（员工） | 角色 ID，从 `GET /api/back-user/roles` 获取 |
| password | string | **是** | - | 登录密码 |

### 角色对照表

| roleId | 角色 |
|:-----:|------|
| 1 | 普通用户 |
| 2 | 员工（默认） |
| 3 | 后勤 |
| 4 | 厨师 |

> ⚠️ 手机号已存在时会返回错误"手机号已存在"
>
> ⚠️ 若 roleId 未传或为 0，默认使用 roleId=2
>
> ⚠️ nickname 不传时，后台自动用 realName 填充 wx_nickname，确保列表中有显示名称

### 响应

```json
// 成功
{ "code": 200, "message": "添加成功" }

// 失败
{ "code": 400, "message": "手机号已存在" }

// 失败
{ "code": 400, "message": "手机号格式不正确" }
```

### 前端调用示例

```javascript
// 方式一（推荐）：使用 FarmAPI — 传 nickname
const res = await FarmAPI.user.add({
  phone: '13800138000',
  realName: '张三',
  nickname: '张三',
  gender: '男',
  roleId: 2,
  password: '123456'
});

// 方式二：不传 nickname → 自动用 realName 当昵称
const res = await FarmAPI.user.add({
  phone: '13800138000',
  realName: '张三',
  gender: '女',
  roleId: 3,          // 后勤
  password: '123456'
});

// 方式三：直接 fetch ✅
const res = await fetch(FarmAPI.url('/api/back-user/add'), {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ phone, realName, nickname, gender, roleId, password })
});
```

---

## 3. 编辑用户

```
POST /api/back-user/edit
Content-Type: application/json
```

### 请求体

```json
{
  "guid": "a1b2c3d4-...",
  "nickname": "新昵称",
  "gender": "女",
  "role": "员工"
}
```

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| guid | string | **是** | 用户的 Guid（唯一标识） |
| nickname | string | 否 | 昵称 |
| gender | string | 否 | 性别 |
| role | string | 否 | ⚠️ 角色名称（如"员工"），**不是角色 ID** |

> ⚠️ **重要**：`role` 字段传的是**角色名称字符串**（如 `"员工"`、`"管理员"`），不是数字 ID。后端通过 `GetRoleIdByName()` 按名称查询角色表。
>
> ⚠️ guid 为空时返回"用户ID不能为空"
>
> ⚠️ 用户不存在时返回"用户不存在，请刷新列表"

### 响应

```json
{ "code": 200, "message": "编辑成功" }
```

### 前端调用示例（正确 ✅）

```javascript
// 正确：直接传 bodyData，不要包一层 { dto: ... }
const res = await FarmAPI.user.edit({
  guid: 'a1b2c3d4-...',
  nickname: '新昵称',
  gender: '女',
  role: '员工'    // 传角色名称，不是 roleId
});
```

### ❌ 错误调用（当前 bug）

```javascript
// 错误！多包了一层 { dto: ... }
const res = await FarmAPI.user.edit({ dto: bodyData });
// 后端收到 {"dto": {...}}，所有字段解析为 null
```

---

## 4. 用户详情（根据 id）

```
GET /api/back-user/detail?id=xxx
```

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| id | string | 是 | 用户 ID（格式如 `U20260521120019`） |

### 响应

```json
{
  "code": 200,
  "message": "获取成功",
  "data": {
    "guid": "a1b2c3d4-...",
    "phone": "13800138000",
    "nickname": "张三",
    "avatar": "...",
    "gender": "男",
    "loginTime": "2026-05-21 10:30",
    "id": 1,
    "realName": "张三",
    "wxOpenId": "o_xxx",
    "roleId": 2,
    "roleName": "员工"
  }
}
```

---

## 5. 用户详情（根据 UserId 数字）

```
GET /api/back-user/{userId}
```

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| userId | int | 是 | 数据库自增 UserId |

> 响应格式同接口 4

---

## 6. 用户详情（根据 Guid 查询）

```
GET /api/back-user/detail/guid?guid=xxx
```

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| guid | string | 是 | 用户 Guid |

> 响应格式同接口 4

---

## 7. 删除用户

```
POST /api/back-user/delete
Content-Type: application/json
```

### 请求体

```json
{ "guid": "a1b2c3d4-..." }
```

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| guid | string | 是 | 要删除用户的 Guid |

### 响应

```json
{ "code": 200, "message": "删除成功" }
```

---

## 8. 批量删除用户

```
POST /api/back-user/deleteBatch
Content-Type: application/json
```

### 请求体

```json
{ "ids": ["guid1", "guid2", "guid3"] }
```

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| ids | string[] | 是 | 用户 Guid 列表 |

### 响应

```json
{ "code": 200, "message": "批量删除成功，共删除 3 个用户" }
```

---

## 9. 角色列表

```
GET /api/back-user/roles
```

### 响应

```json
{
  "code": 200,
  "message": "获取成功",
  "data": {
    "records": [
      { "roleId": 1, "roleName": "管理员" },
      { "roleId": 2, "roleName": "员工" }
    ]
  }
}
```

---

## 10. 登录

```
POST /api/back-user/login
Content-Type: application/json
```

### 请求体

```json
{
  "user_no": "admin",
  "password": "123456"
}
```

### 响应

```json
{
  "code": 200,
  "message": "登录成功",
  "data": {
    "user_no": "admin",
    "role": "user",
    "loginTime": "2026年05月21日 10:30",
    "token": "eyJhbGciOi...",
    "user_password": "hashed_value"
  }
}
```

---

## 11. 登出

```
POST /api/back-user/logout
```

> 服务端不做实际注销，客户端删除本地 Token 即可

### 响应

```json
{ "code": 200, "message": "注销成功，请删除本地 Token" }
```

---

## 已知问题汇总

| # | 问题 | 位置 | 说明 |
|---|------|------|------|
| 🔴 1 | 编辑 API 请求体多包一层 `{ dto: ... }` | `user-edit.html:291` | 后端收到 `{"dto":{...}}`，所有字段 null |
| 🟡 2 | 编辑 API 的 role 字段传角色 ID 但后端按角色名查 | `user-edit.html:288` → `BackUserService.cs:180` | `GetRoleIdByName("2")` 查不到，默认 roleId=1 |
| 🔵 3 | 新增用户性别下拉框没有可选值 | `user-add.html:144-148` | `<select>` 只有空 option 没有实际选项 |

---

## 前端调用速查

```javascript
// 用户列表
FarmAPI.user.list({ keyword: '张三', pageNum: 1, pageSize: 20 })

// 用户详情（根据 id 字符串）
FarmAPI.user.detail('U20260521120019')

// ✅ 新增用户
FarmAPI.user.add({ phone, realName, gender, roleId, password })

// ✅ 编辑用户（直接传数据，不要包 dto 对象）
FarmAPI.user.edit({ guid, nickname, gender, role: '员工' })

// 删除用户
FarmAPI.user.delete(guid)

// 批量删除
FarmAPI.user.deleteBatch({ ids: [guid1, guid2] })

// 角色列表
FarmAPI.user.roles()
```
