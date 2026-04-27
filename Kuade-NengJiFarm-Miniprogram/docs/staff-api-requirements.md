# 能记农场 — 员工模块 API 需求文档（完整版）

> 版本：v2.0 | 日期：2026-04-27  
> 项目：能记农场微信小程序  
> 模块：角色登录 + 员工界面 + 扫码核销  
> 合并自：`staff-verify-api-requirements.md` + `staff-login-api-requirements.md`

---

## 一、通用约定

### 1.1 响应格式

所有接口统一使用项目已有的 `ApiResult` 结构：

```json
{
  "code": 0,          // 0=成功，非0=失败
  "message": "string", // 描述信息
  "data": {}           // 业务数据（失败时可为 null）
}
```

### 1.2 鉴权方式

- 请求头携带 `Authorization: Bearer {JWT token}`
- 员工接口（`/api/staff/*`）需额外校验当前用户角色为 `staff`，非员工返回 `403`

### 1.3 错误码约定

| code | 含义 | 说明 |
|------|------|------|
| 0 | 成功 | - |
| 401 | 未登录 | token 无效或过期 |
| 403 | 无权限 | 非员工访问员工接口，或券已过期 |
| 404 | 未找到 | 券码不存在 |
| 409 | 冲突 | 券已使用（重复核销） |

---

## 二、整体流程

```
用户点击「微信手机号一键登录」
        ↓
前端调用 POST /api/Auth/wx-phone-login
        ↓
后端验证手机号，判断角色（user / staff）
        ↓
返回 token + role 字段
        ↓
前端根据 role 跳转：
  - role = 'user'  → 普通用户首页（switchTab /pages/index/index）
  - role = 'staff' → 员工工作台（redirectTo /staff-pages/staff-home/staff-home）

员工工作台：
  ├── 顶部：头像 + 昵称 + "员工端"徽章 + 退出按钮
  ├── 今日核销统计卡片（GET /api/staff/today-stats）
  ├── 核心功能：🎫 核销券类 → staff-verify 页面
  └── 底部导航：[工作台] [我的]

员工「我的」页面：
  ├── 头部：头像 + 昵称 + "员工账号" + 设置图标
  ├── 快捷入口：核销券类 / 核销记录
  ├── 账号信息卡：角色类型 + 在线状态
  └── 退出登录按钮

核销操作：
  ├── 点击扫码 → wx.scanCode → 获取 code
  ├── 手动输入券码
  └── 调用 POST /api/staff/verify { code }
       ├── 成功(code:0) → 显示成功弹窗 + 券详情
       └── 失败 → 显示对应错误提示
```

---

## 三、接口详情

### 3.1 登录接口改造 — 新增 `role` 返回字段

**接口路径**：`POST /api/Auth/wx-phone-login`（已有接口，仅修改返回值）

**请求参数**：不变

```json
{
  "code": "string",       // wx.login 获取的 code（必填）
  "phoneCode": "string"   // getPhoneNumber 获取的 phoneCode（可选）
}
```

**响应 data 变更**：

```json
{
  "token": "string",
  "user_id": 1,
  "user_guid": "string",
  "openid": "string",
  "phone_number": "string",
  "register_time": "string",
  "role": "user"          // 🆕 新增字段
}
```

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `role` | string | 是 | 用户角色：`"user"` 普通用户 / `"staff"` 员工 |

**业务规则**：
1. 后端需在用户表中增加 `role` 字段（默认值 `"user"`）
2. 登录时根据用户记录的 `role` 字段返回对应值
3. 管理员可通过后台将特定用户的 `role` 设为 `"staff"`
4. 如果用户记录中无 `role` 字段，默认返回 `"user"`

---

### 3.2 获取用户信息接口 — 新增 `role` 返回字段

**接口路径**：`GET /api/user/profile`（已有接口，仅修改返回值）

**权限**：需登录（Bearer token）

**请求参数**：无

**响应 data 变更**：

```json
{
  "nickname": "string",
  "avatar": "string",
  "email": "string",
  "balance": 0,
  "reward": 0,
  "role": "user"          // 🆕 新增字段
}
```

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `role` | string | 是 | 用户角色：`"user"` 普通用户 / `"staff"` 员工 |

**业务规则**：
1. 返回当前用户的 `role` 字段值
2. 前端用此接口做：个人中心显示角色标识；缓存 role 到本地供 tabBar 判断

---

### 3.3 员工今日核销统计（新增接口）

**接口路径**：`GET /api/staff/today-stats`

**权限**：仅 `role=staff` 可访问

**请求参数**：无

**成功响应**：

```json
{
  "code": 0,
  "message": "success",
  "data": {
    "today_verify_count": 12,
    "last_verify_time": "2026-04-27T15:30:00"
  }
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| `today_verify_count` | int | 今日核销总数 |
| `last_verify_time` | string | 最近一次核销时间（可选，前端可展示） |

**业务规则**：
1. 统计当前员工今日 00:00:00 至 23:59:59 的核销次数
2. 仅统计核销成功的记录
3. 非员工访问返回 403

---

### 3.4 核销券码

**接口路径**：`POST /api/staff/verify`

**权限**：仅 `role=staff` 可访问

**请求参数**：

```json
{
  "code": "PICK-20260427-001"   // 券码（扫码或手动输入的字符串）
}
```

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `code` | string | 是 | 券码字符串，来源：二维码内容或手动输入 |

**成功响应**（`code: 0`）：

```json
{
  "code": 0,
  "message": "核销成功",
  "data": {
    "voucher_id": "string",
    "voucher_type": "pick",
    "title": "草莓采摘体验券",
    "user_name": "张三",
    "user_phone": "138****8888",
    "order_id": "string",
    "expire_time": "2026-12-31",
    "verify_time": "2026-04-27 15:30:00"
  }
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| `voucher_id` | string | 券唯一标识 |
| `voucher_type` | string | 券类型：`"pick"` 采摘券 / `"activity"` 活动券 |
| `title` | string | 券的展示名称 |
| `user_name` | string | 持券用户姓名 |
| `user_phone` | string | 用户手机号（脱敏显示） |
| `order_id` | string | 该券关联的订单编号 |
| `expire_time` | string | 有效期截止日期 |
| `verify_time` | string | 核销操作的时间戳 |

**失败响应示例**：

```json
// 券码不存在
{ "code": 404, "message": "未找到该券码", "data": null }

// 券已使用
{ "code": 409, "message": "该券已使用，无法重复核销", "data": null }

// 券已过期
{ "code": 403, "message": "该券已过期", "data": null }

// 非员工访问
{ "code": 403, "message": "无权限，仅员工可执行核销", "data": null }
```

**业务规则**：
1. 必须校验当前用户 `role=staff`，否则返回 403
2. 根据 `code` 查找券记录，不存在返回 404
3. 检查券状态：已使用返回 409，已过期返回 403
4. 核销成功后更新券状态为"已使用"，记录核销时间和核销员工ID
5. 同一券码不可重复核销

---

### 3.5 获取待核销券列表

**接口路径**：`GET /api/staff/vouchers`

**权限**：仅 `role=staff` 可访问

**请求参数**（Query）：

| 参数 | 类型 | 必填 | 默认值 | 说明 |
|------|------|------|--------|------|
| `type` | string | 否 | `""` | 按券类型筛选：`"pick"` / `"activity"` / `""` 全部 |
| `status` | string | 否 | `"unused"` | 券状态：`"unused"` 待使用 / `"used"` 已使用 / `"all"` 全部 |
| `page` | int | 否 | 1 | 页码 |
| `pageSize` | int | 否 | 20 | 每页条数 |

**成功响应**：

```json
{
  "code": 0,
  "message": "success",
  "data": {
    "total": 50,
    "page": 1,
    "pageSize": 20,
    "list": [
      {
        "voucher_id": "string",
        "voucher_type": "pick",
        "title": "草莓采摘体验券",
        "user_name": "张三",
        "user_phone": "138****8888",
        "order_id": "string",
        "status": "unused",
        "expire_time": "2026-12-31",
        "create_time": "2026-04-20 10:00:00"
      }
    ]
  }
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| `total` | int | 符合条件的总数 |
| `page` | int | 当前页码 |
| `pageSize` | int | 每页条数 |
| `list` | array | 券列表 |
| `list[].voucher_id` | string | 券唯一标识 |
| `list[].voucher_type` | string | 券类型 |
| `list[].title` | string | 券名称 |
| `list[].user_name` | string | 用户姓名 |
| `list[].user_phone` | string | 手机号（脱敏） |
| `list[].order_id` | string | 关联订单ID |
| `list[].status` | string | `"unused"` / `"used"` / `"expired"` |
| `list[].expire_time` | string | 有效期 |
| `list[].create_time` | string | 券创建时间 |

**业务规则**：
1. 默认返回待使用（`unused`）的券，方便员工查看
2. 支持按券类型和状态筛选
3. 列表按创建时间倒序排列

---

### 3.6 获取核销历史

**接口路径**：`GET /api/staff/verify-history`

**权限**：仅 `role=staff` 可访问

**请求参数**（Query）：

| 参数 | 类型 | 必填 | 默认值 | 说明 |
|------|------|------|--------|------|
| `today` | bool | 否 | `true` | 是否只看今日：`true` 仅今日 / `false` 全部 |
| `page` | int | 否 | 1 | 页码 |
| `pageSize` | int | 否 | 20 | 每页条数 |

**成功响应**：

```json
{
  "code": 0,
  "message": "success",
  "data": {
    "total": 8,
    "page": 1,
    "pageSize": 20,
    "list": [
      {
        "verify_id": "string",
        "voucher_type": "pick",
        "title": "草莓采摘体验券",
        "user_name": "张三",
        "verify_time": "2026-04-27 15:30:00",
        "verify_staff": "李四"
      }
    ]
  }
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| `total` | int | 核销记录总数 |
| `page` | int | 当前页码 |
| `pageSize` | int | 每页条数 |
| `list` | array | 核销记录列表 |
| `list[].verify_id` | string | 核销记录唯一标识 |
| `list[].voucher_type` | string | 券类型：`"pick"` / `"activity"` |
| `list[].title` | string | 券名称 |
| `list[].user_name` | string | 用户姓名 |
| `list[].verify_time` | string | 核销时间 |
| `list[].verify_staff` | string | 执行核销的员工姓名 |

**业务规则**：
1. 前端默认传 `today=true`，只查当天核销记录
2. 当 `today=true` 时，后端自动过滤为当天 00:00:00 至 23:59:59 的记录
3. 列表按核销时间倒序排列（最新在前）
4. 仅返回当前员工自己的核销记录（如果需要看所有人的，需额外参数）

---

## 四、接口汇总

| 序号 | 接口 | 方法 | 状态 | 改动说明 |
|------|------|------|------|---------|
| 1 | `/api/Auth/wx-phone-login` | POST | 已有，需改 | 响应新增 `role` 字段 |
| 2 | `/api/user/profile` | GET | 已有，需改 | 响应新增 `role` 字段 |
| 3 | `/api/staff/today-stats` | GET | **新增** | 员工今日核销统计 |
| 4 | `/api/staff/verify` | POST | **新增** | 核销券码 |
| 5 | `/api/staff/vouchers` | GET | **新增** | 待核销券列表 |
| 6 | `/api/staff/verify-history` | GET | **新增** | 核销历史记录 |

---

## 五、数据库变更建议

### 5.1 用户表（Users）新增字段

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `role` | varchar(20) | `"user"` | 用户角色：`user` / `staff` |

### 5.2 券表（Vouchers）— 建议新建

| 字段 | 类型 | 说明 |
|------|------|------|
| `id` | bigint PK | 主键 |
| `code` | varchar(64) UNIQUE | 券码（二维码内容） |
| `voucher_type` | varchar(20) | 券类型：`pick` / `activity` |
| `title` | varchar(128) | 券名称 |
| `user_id` | bigint FK | 持券用户ID |
| `order_id` | bigint FK | 关联订单ID |
| `status` | varchar(20) | 状态：`unused` / `used` / `expired` |
| `expire_time` | datetime | 有效期截止 |
| `used_time` | datetime nullable | 实际使用时间 |
| `used_by` | bigint nullable FK | 核销员工ID |
| `create_time` | datetime | 创建时间 |

### 5.3 核销记录表（VerifyLogs）— 建议新建

| 字段 | 类型 | 说明 |
|------|------|------|
| `id` | bigint PK | 主键 |
| `voucher_id` | bigint FK | 券ID |
| `staff_id` | bigint FK | 核销员工ID |
| `verify_time` | datetime | 核销时间 |
| `remark` | varchar(256) nullable | 备注 |

### 5.4 独立员工表（Staff）— 可选，建议后续扩展时建

| 字段 | 类型 | 说明 |
|------|------|------|
| `id` | INT PK | 主键 |
| `user_id` | INT FK | 关联 users 表 |
| `name` | VARCHAR(50) | 员工姓名 |
| `phone` | VARCHAR(20) | 手机号（用于登录识别） |
| `status` | TINYINT | 状态：1=在职 0=离职 |
| `created_at` | DATETIME | 创建时间 |

---

## 六、前端已适配说明

前端代码已完成以下适配，后端接口按本文档实现即可直接对接：

### 6.1 文件适配清单

| 前端文件 | 适配内容 |
|----------|----------|
| `login.js` | 登录成功后读取 `loginData.role` 存入本地，员工跳转工作台 |
| `custom-tab-bar/` | 根据 `user_role` 动态切换底部导航（用户4tab / 员工2tab） |
| `profile.js` | `onShow` 检测 `user_role`，设置 `isStaff` 状态；测试模式放行 |
| `profile.wxml` | 员工：头像+昵称+设置图标 + 快捷入口 + 账号信息 + 退出登录 |
| `staff-home/` | 员工工作台：渐变头部 + 今日统计 + 核心功能入口 + 底部导航 |
| `staff-verify/` | 核销页面：扫码/手动输入/结果弹窗/今日历史 |
| `api.js` | `staff.verifyVoucher(code)` / `staff.getPendingVouchers(params)` / `staff.getVerifyHistory(params)` |

### 6.2 前端错误码处理映射

```javascript
// staff-verify.js 中的错误码处理逻辑
err.code === 404  → "未找到该券码"
err.code === 409  → "该券已使用，无法重复核销"
err.code === 403  → "该券已过期"
其他               → err.message || "核销失败"
```

### 6.3 登录跳转逻辑（待后端 role 字段就绪后启用）

```javascript
handleLoginSuccess(loginData) {
  wx.setStorageSync('token', loginData.token || '');
  wx.setStorageSync('user_role', loginData.role || 'user');
  // ...

  const userRole = loginData.role || 'user';
  if (userRole === 'staff') {
    wx.redirectTo({ url: '/staff-pages/staff-home/staff-home' });
  } else {
    wx.switchTab({ url: '/pages/index/index' });
  }
}
```

> ⚠️ 当前前端测试模式：login.js 直接写入 `user_role: 'staff'` 并跳转工作台，上线前需删除测试代码并启用上述角色分流逻辑。

---

## 七、最简实现方案（快速上线）

如果后端时间紧，**最小改动**只需 2 步：

1. **users 表加 `role` 字段**（默认 `user`）
2. **登录接口 + profile 接口返回 `role`**

这样前端就能自动按角色分流，员工工作台全部功能可用。

其余接口可分批上线：
- **第一批**（核心）：登录返回 role + 核销接口（`/api/staff/verify`）
- **第二批**（完善）：`/api/staff/vouchers` + `/api/staff/verify-history`
- **第三批**（优化）：`/api/staff/today-stats`（前端先用 verify-history 兜底）
