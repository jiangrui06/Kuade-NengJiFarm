# 员工端API需求文档

## 概述

本文档描述员工端需要的API接口需求。

## 员工登录 Staff Login

| 方法 | 地址 | 鉴权 | 用途 | 参数/请求体 | 返回 data |
| --- | --- | --- | --- | --- | --- |
| POST | `/api/Staff/login` | 否 | 员工用户名密码登录 | Body：`username` 必填，`password` 必填 | `token`、`user_id`、`role`（固定为 `staff`、`nickname`、`phone`、`avatar` |

### 请求示例

```json
POST /api/Staff/login
{
  "username": "admin",
  "password": "123456"
}
```

### 响应示例

```json
{
  "code": 200,
  "message": "success",
  "data": {
    "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "user_id": "staff_001",
    "role": "staff",
    "nickname": "张三",
    "phone": "13800138000",
    "avatar": ""
  }
}
```

### 错误响应

```json
{
  "code": 400,
  "message": "用户名或密码错误",
  "data": null
}
```

---

## 员工首页统计

| 方法 | 地址 | 鉴权 | 用途 | 参数/请求体 | 返回 data |
| --- | --- | --- | --- | --- | --- |
| GET | `/api/staff/today-stats` | 是，员工 | 员工首页今日核销统计 | 无 | `todayVerified`、`pendingCount`、`activityVerified`、`pickingVerified`、`today_verify_count`、`last_verify_time` |

---

## 券核销

| 方法 | 地址 | 鉴权 | 用途 | 参数/请求体 | 返回 data |
| --- | --- | --- | --- | --- | --- |
| POST | `/api/staff/verify` | 是，员工 | 核销活动券/认购券 | Body：`code` 必填，支持订单号、二维码 URL、`ACT-`/`PICK-`前缀码 | 核销结果、券类型、用户信息、核销时间 |

### 请求示例

```json
POST /api/staff/verify
{
  "code": "ACT-20260429-0001"
}
```

### 响应示例

```json
{
  "code": 200,
  "message": "success",
  "data": {
    "success": true,
    "type": "activity",
    "user_name": "张三",
    "user_phone": "13800138000",
    "verify_time": "2026-04-29 10:30:00"
  }
}
```

---

## 券列表查询

| 方法 | 地址 | 鉴权 | 用途 | 参数/请求体 | 返回 data |
| --- | --- | --- | --- | --- |
| GET | `/api/staff/vouchers` | 是，员工 | 查询待核销/已核销/过期券列表 | Query：`type` 可选 `activity`/`picking`/`pick`，`status` 可选 `unused`/`used`/`expired`/`all`，`page`，`pageSize` | `total`、`page`、`pageSize`、`list` |

### Query参数说明

- `type`: 券类型
  - `activity`: 活动券
  - `picking` 或 `pick`: 采摘券/认购券
- `status`: 状态
  - `unused`: 待核销
  - `used`: 已核销
  - `expired`: 已过期
  - `all`: 全部
- `page`: 页码
- `pageSize`: 每页数量

---

## 核销历史

| 方法 | 地址 | 鉴权 | 用途 | 参数/请求体 | 返回 data |
| --- | --- | --- | --- | --- | --- |
| GET | `/api/staff/verify-history` | 是，员工 | 查询核销历史 | Query：`today` 默认 `true`，`page`，`pageSize`，`startDate`，`endDate` | `total`、`page`、`pageSize`、`list` |

### Query参数说明

- `today`: 是否只查今日
- `page`: 页码
- `pageSize`: 每页数量
- `startDate`: 开始日期
- `endDate`: 结束日期

---

## 数据模型

### 券对象字段

```json
{
  "id": "string",
  "code": "string",
  "type": "activity|picking",
  "user_name": "string",
  "user_phone": "string",
  "status": "unused|used|expired",
  "create_time": "string",
  "expire_time": "string"
}
```

### 核销记录对象字段

```json
{
  "id": "string",
  "code": "string",
  "user_name": "string",
  "user_phone": "string",
  "verify_time": "string"
}
```

---

## 通用说明

1. 所有员工接口需要在请求头中携带 `Authorization: Bearer <token>`
2. 返回格式：`{ "code": 200, "message": "success", "data": ... }`
3. 业务失败返回 `code !== 200` 并显示 `message`

