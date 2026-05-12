# 员工核销 API 文档

## 基础信息

- **Base URL**: `https://api.nengjifarm.com`
- **认证方式**: JWT Bearer Token（需要员工角色权限）
- **响应格式**: 统一 `ApiResult` 包装

```json
{
  "code": 200,
  "message": "success",
  "data": { ... }
}
```

---

## 1. 核销接口

### 1.1 `POST /api/staff/verify`

通过 **核销码** 核销券。支持多种输入格式：

- 二维码扫码内容（含 `verifyCode=` 参数的 URL）
- 核销码（6-8 位字母数字）
- 订单号
- 订单 ID

#### 请求体

```json
{
  "code": "ABC12345"
}
```

#### 核销成功响应（200）

```json
{
  "code": 200,
  "message": "核销成功",
  "data": {
    "verified": true,
    "alreadyVerified": false,
    "success": true,
    "voucherId": "42",
    "voucherType": "activity",
    "userName": "张三",
    "userPhone": "138****5678",
    "content": "草莓采摘体验券",
    "verifyTime": "2026-05-12 14:30:00",
    "participantCount": 2,
    "voucher_id": "42",
    "voucher_type": "activity",
    "title": "草莓采摘体验券",
    "user_name": "张三",
    "user_phone": "138****5678",
    "order_id": "ACT20260512123456789123",
    "expire_time": "2026-06-11",
    "verify_time": "2026-05-12 14:30:00"
  }
}
```

#### 已核销响应（200，alreadyVerified=true）

```json
{
  "code": 200,
  "message": "该券已核销",
  "data": {
    "verified": true,
    "alreadyVerified": true,
    "voucherId": "42",
    "voucherType": "activity",
    "userName": "张三",
    "userPhone": "138****5678",
    "content": "草莓采摘体验券",
    "participantCount": 2,
    "order_id": "ACT20260512123456789123",
    "message": "该券已核销"
  }
}
```

#### 错误响应

| 状态码 | message | 说明 |
|--------|---------|------|
| 400 | 券码不能为空 | 未传入 Code |
| 403 | 无权限，仅员工可执行核销 | 当前用户非员工角色 |
| 403 | 该券已过期，有效期至 xxx | 超过有效期 |
| 403 | 该券未支付或已取消，无法核销 | 状态为未支付/已取消 |
| 404 | 未找到该券码 | 券码无效 |

---

### 1.2 `POST /api/staff-verify/voucher`

通过 **二维码内容（activity_qrcode）** 核销券。与 `POST /api/staff/verify` 的区别：

- 仅通过 `ActivityOrderDetails.ActivityQrcode` 精确匹配
- 返回值中券类型通过 `ActivityType` 表动态解析

#### 请求体

```json
{
  "code": "A3B7K9X2P5M1"
}
```

#### 核销成功响应（200）

```json
{
  "code": 200,
  "message": "核销成功",
  "data": {
    "voucherType": "activity",
    "typeName": "活动券",
    "userName": "张三",
    "userPhone": "138****5678",
    "content": "草莓采摘体验券",
    "participantCount": 2,
    "verifyTime": "2026-05-12 14:30:00",
    "verified": true,
    "alreadyVerified": false
  }
}
```

---

## 2. 核销历史

### 2.1 `GET /api/staff/verify-history`

获取当前员工的核销记录。默认仅查当天。

#### 查询参数

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| today | bool | true | 是否仅查当天 |
| page | int | 1 | 页码 |
| pageSize | int | 20 | 每页条数（最大 100） |
| startDate | string | - | 开始日期 `yyyy-MM-dd`（today=false 时生效） |
| endDate | string | - | 结束日期 `yyyy-MM-dd` |

#### 响应示例

```json
{
  "code": 200,
  "data": {
    "total": 50,
    "page": 1,
    "pageSize": 20,
    "list": [
      {
        "id": "42",
        "verifyId": "42",
        "voucherType": "activity",
        "title": "草莓采摘体验券",
        "userName": "张三",
        "verifyTime": "2026-05-12 14:30:00",
        "verifyStaff": "李四",
        "participantCount": 2,
        "verify_id": "42",
        "voucher_type": "activity",
        "user_name": "张三",
        "verify_time": "2026-05-12 14:30:00",
        "verify_staff": "李四"
      }
    ]
  }
}
```

### 2.2 `GET /api/staff-verify/history`

更强大的核销历史查询，支持更多筛选条件。

#### 查询参数

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| page | int | 1 | 页码 |
| pageSize | int | 20 | 每页条数（最大 200） |
| voucherType | string | "all" | 券类型：`all` / `activity` / `pick` |
| keyword | string | - | 搜索订单号/核销码/用户名 |
| startDate | string | - | 开始日期 `yyyy-MM-dd` |
| endDate | string | - | 结束日期 `yyyy-MM-dd` |
| categoryName | string | - | 按活动类型名筛选（如"活动券""采摘券"） |

#### 响应示例

```json
{
  "code": 200,
  "data": {
    "list": [
      {
        "id": "1",
        "voucherType": "activity",
        "typeName": "活动券",
        "categoryName": "活动券",
        "userName": "张三",
        "userPhone": "138****5678",
        "content": "草莓采摘体验券",
        "verifyTime": "2026-05-12 14:30:00",
        "verified": true,
        "orderId": "ACT20260512123456789123",
        "participantCount": 2
      }
    ],
    "total": 50,
    "page": 1,
    "pageSize": 20
  }
}
```

---

## 3. 券列表

### 3.1 `GET /api/staff/vouchers`

分页获取所有券，支持按状态和类型筛选。

#### 查询参数

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| type | string | - | 券类型：`activity` / `pick` |
| status | string | "unused" | 状态：`unused` / `used` / `expired` / `all` |
| page | int | 1 | 页码 |
| pageSize | int | 20 | 每页条数（最大 100） |

#### 响应示例

```json
{
  "code": 200,
  "data": {
    "total": 100,
    "page": 1,
    "pageSize": 20,
    "list": [
      {
        "voucherId": "42",
        "voucherType": "activity",
        "title": "草莓采摘体验券",
        "userName": "张三",
        "userPhone": "138****5678",
        "orderId": "42",
        "status": "unused",
        "expireTime": "2026-06-11",
        "createTime": "2026-05-12 10:00:00",
        "voucher_id": "42",
        "voucher_type": "activity",
        "user_name": "张三",
        "user_phone": "138****5678",
        "order_id": "42",
        "expire_time": "2026-06-11",
        "create_time": "2026-05-12 10:00:00"
      }
    ]
  }
}
```

---

## 4. 权限与统计

### 4.1 `GET /api/staff-verify/permission`

检查当前用户是否拥有核销权限。

#### 响应示例

```json
{
  "code": 200,
  "data": {
    "hasPermission": true,
    "role": "staff",
    "staffId": "1"
  }
}
```

### 4.2 `GET /api/staff/today-stats`

获取今日核销统计概览。

#### 响应示例

```json
{
  "code": 200,
  "data": {
    "todayVerified": 15,
    "pendingCount": 42,
    "activityVerified": 15,
    "pickingVerified": 15,
    "today_verify_count": 15,
    "last_verify_time": "2026-05-12 14:30:00",
    "staff_real_name": "李四"
  }
}
```

---

## 5. 有效期规则

核销结束时间 = **下单时间 + 活动设置的有效天数（Duration）**

- 仅当活动设置了 `Duration > 0` 时才会检查有效期
- 若 `Duration` 未设置或为 0，则不检查有效期，任何待核销状态的券均可核销
- 过期返回 `403` + `"该券已过期，有效期至 xxx"`

券码生成：`POST /api/orders/{id}/qrcode` 生成的 12 位随机字符（不含 0/O/1/I）

## 6. 券状态流转

```
待付款(1) ──支付──> 待核销(2) ──核销──> 已核销(3)
    │                                         │
    └──取消──> 已取消(4)                       已核销可重复查询（返回已核销信息）
```

- 券码支持去重校验（已核销的券再次扫码返回已核销信息）
- 支付锁保证同一订单不会被重复支付
