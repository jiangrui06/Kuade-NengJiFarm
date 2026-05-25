# 产品订单退款 API 接口文档

> 版本：v1.0
> 日期：2026-05-25
> 基础路径：`/api`
> Controller：`ProductOrderController`
> Service：`ProductOrderService`

---

## 目录

| # | 内容 |
|---|------|
| 一 | 订单状态说明 |
| 二 | 申请退款 |
| 三 | 处理退款 |
| 四 | 驳回退款 |
| 五 | 错误码与异常处理 |
| 六 | 前端调用速查 |

---

## 一、订单状态说明

### 1.1 状态流转图

```
待支付(1) ──支付──> 待发货(2) ──发货──> 待收货(3) ──收货──> 已完成(4)
                      │   │                     │
                      │   ├──申请退款──────────> 退款中(6) ──处理退款──> 已退款(7)
                      │   │                                             │
                      │   └──取消订单────────────────────────────────> 已取消(5)
                      │
                      └──取消待支付─────────────────────────────────> 已取消(5)
```

括号内为 `order_status_id` 数据库值。

### 1.2 退款相关状态

| order_status_id | 显示状态 | 说明 |
|:---------------:|---------|------|
| 2 | 待发货 | 可申请退款 |
| 6 | 退款中 | 可处理退款 / 可驳回退款 |
| 7 | 已退款 | 退款最终态 |

---

## 二、申请退款

管理员直接发起退款，适用于待发货/运输中的订单。

### 2.1 接口信息

```
POST /api/product/order/refund
Content-Type: application/json
Authorization: Bearer ***
```

### 2.2 请求体

| 字段 | 类型 | 必填 | 说明 |
|------|------|:----:|------|
| orderId | int | **是** | 产品订单主键ID（数字型，非订单号） |
| refundReason | string | 否 | 退款原因 |

```json
{
  "orderId": 12345,
  "refundReason": "商品缺货，无法发货"
}
```

> **注意**：此处 `orderId` 是数据库自增主键（数字型），不是订单号。

### 2.3 业务规则

| 校验项 | 规则 |
|--------|------|
| orderId | 不能为空，必须在产品订单表中存在 |
| 订单状态 | 仅 `待发货`(2) 或 `运输中`(3) 允许退款 |
| 重复退款 | 已退款的订单返回错误"该订单已完成退款，请勿重复操作" |

### 2.4 后端处理流程

```
1. 根据 orderId 查询产品订单
2. 校验订单存在性 → 不存在返回错误
3. 校验状态 → 非待发货/运输中返回错误
4. 幂等性检查 → 已退款返回错误
5. 调用微信退款（原路退回）
   - 检查 WxPayNo 是否为有效微信订单号（纯数字）
   - 跳过 MOCK_ / LOCKING: 开头的模拟订单
6. 恢复商品库存（RestoreCommodityStockAsync）
7. 写入退款记录（refund_record），状态为 completed
8. 更新订单状态 → order_status_id = 7（已退款）
9. 返回退款结果
```

### 2.5 成功响应

```json
{
  "code": 200,
  "message": "退款成功",
  "data": {
    "refundId": "RF20260525120002123",
    "orderId": "12345",
    "refundAmount": "299.00",
    "refundTime": "2026-05-25 12:05:00",
    "operator": "admin"
  }
}
```

### 2.6 错误响应

```json
// 参数错误
{ "code": 400, "message": "请求参数不完整：orderId 不能为空" }

// 订单不存在
{ "code": 400, "message": "订单不存在或已被删除" }

// 已退款
{ "code": 400, "message": "该订单已完成退款，请勿重复操作" }

// 状态不允许
{ "code": 400, "message": "当前订单状态不允许退款（仅已支付订单可退款）" }

// 微信退款失败
{ "code": 400, "message": "微信退款失败：{具体原因}" }
```

---

## 三、处理退款

将退款中(6)的订单变为已退款(7)，适用于用户已申请退款后管理员确认。

### 3.1 接口信息

```
PUT /api/product/order/updateStatus
Content-Type: application/json
Authorization: Bearer ***
```

### 3.2 请求体

| 字段 | 类型 | 必填 | 说明 |
|------|------|:----:|------|
| orderNo | string | **是** | 产品订单号 |
| action | string | **是** | 固定值 `"refund-process"` |

```json
{
  "orderNo": "GOODS20260519154644401209",
  "action": "refund-process"
}
```

### 3.3 业务规则

| 校验项 | 规则 |
|--------|------|
| orderNo | 必须存在 |
| action | 必须为 `"refund-process"` |
| 订单状态 | 仅 `退款中`(6) 允许处理 |
| 退款记录 | 必须有待处理的 `RefundRecord`（status=pending） |

### 3.4 后端处理流程

```
1. 根据 orderNo 查询订单
2. 校验订单状态 → 非退款中返回错误
3. 查找待处理的退款记录 → 不存在返回错误
4. 调用微信退款（原路退回）
   - 检查 WxPayNo 是否为有效微信订单号（纯数字）
   - 跳过 MOCK_ / LOCKING: 开头的模拟订单
5. 恢复商品库存
6. 生成退款编号（RF + 时间戳 + 随机3位）
7. 更新退款记录：status=completed, ProcessTime, AdminReply, RefundAmount
8. 更新订单状态 → order_status_id = 7（已退款）
```

### 3.5 成功响应

```json
{
  "code": 200,
  "message": "操作成功",
  "data": null
}
```

### 3.6 错误响应

```json
// 订单号为空
{ "code": 400, "message": "订单号不能为空" }

// 操作类型为空
{ "code": 400, "message": "操作类型不能为空" }

// 状态不允许
{ "code": 400, "message": "仅退款中订单可处理退款" }

// 无退款记录
{ "code": 400, "message": "未找到待处理的退款记录" }

// 微信退款失败
{ "code": 400, "message": "微信退款失败：{具体原因}" }
```

---

## 四、驳回退款

将退款中(6)的订单恢复为待发货(2)，适用于管理员拒绝退款申请。

### 4.1 接口信息

```
PUT /api/product/order/updateStatus
Content-Type: application/json
Authorization: Bearer ***
```

### 4.2 请求体

| 字段 | 类型 | 必填 | 说明 |
|------|------|:----:|------|
| orderNo | string | **是** | 产品订单号 |
| action | string | **是** | 固定值 `"refund-reject"` |
| adminReply | string | 否 | 驳回说明（展示给用户） |
| processNote | string | 否 | 处理备注（内部使用） |

```json
{
  "orderNo": "GOODS20260519154644401209",
  "action": "refund-reject",
  "adminReply": "商品已发货，不符合退款条件",
  "processNote": "物流单号SF123456已揽件"
}
```

### 4.3 业务规则

| 校验项 | 规则 |
|--------|------|
| orderNo | 必须存在 |
| action | 必须为 `"refund-reject"` |
| 订单状态 | 仅 `退款中`(6) 允许驳回 |

### 4.4 后端处理流程

```
1. 根据 orderNo 查询订单
2. 校验订单状态 → 非退款中返回错误
3. 查找待处理的退款记录
4. 更新退款记录：status=rejected, ProcessTime, AdminReply, ProcessNote
5. 恢复订单状态 → order_status_id = 2（待发货）
```

### 4.5 成功响应

```json
{
  "code": 200,
  "message": "操作成功",
  "data": null
}
```

---

## 五、错误码与异常处理

### 5.1 状态码

| 状态码 | 含义 |
|--------|------|
| 200 | 请求成功（业务结果看 code/message） |
| 400 | 参数校验失败或业务不允许 |
| 401 | 未登录/token 过期 |

### 5.2 业务错误消息

| 错误消息 | 触发条件 |
|----------|----------|
| 订单号不能为空 | orderNo 未传 |
| 操作类型不能为空 | action 未传 |
| 请求参数不完整：orderId 不能为空 | orderId 未传或为 0 |
| 订单不存在或已被删除 | orderId/orderNo 查不到订单 |
| 该订单已完成退款，请勿重复操作 | 订单已处于退款完成状态 |
| 当前订单状态不允许退款（仅已支付订单可退款） | 订单状态不在可退款白名单 |
| 仅退款中订单可处理退款 | 处理退款时订单不是退款中 |
| 未找到待处理的退款记录 | 处理退款时无 pending 状态的退款记录 |
| 微信退款失败：{原因} | 支付网关退款异常 |

---

## 六、前端调用速查

### 6.1 申请退款

```javascript
// 注意：orderId 是数字型主键，不是订单号
const response = await fetch(FarmAPI.url('/api/product/order/refund'), {
  method: 'POST',
  headers: Auth.getAuthHeaders({ 'Content-Type': 'application/json' }),
  body: JSON.stringify({
    orderId: order.orderId,       // 数字型主键
    refundReason: '商品缺货'
  })
});
```

### 6.2 处理退款

```javascript
const response = await fetch(FarmAPI.url('/api/product/order/updateStatus'), {
  method: 'PUT',
  headers: Auth.getAuthHeaders({ 'Content-Type': 'application/json' }),
  body: JSON.stringify({
    orderNo: 'GOODS20260519154644401209',
    action: 'refund-process'
  })
});
```

### 6.3 驳回退款

```javascript
const response = await fetch(FarmAPI.url('/api/product/order/updateStatus'), {
  method: 'PUT',
  headers: Auth.getAuthHeaders({ 'Content-Type': 'application/json' }),
  body: JSON.stringify({
    orderNo: 'GOODS20260519154644401209',
    action: 'refund-reject',
    adminReply: '商品已发货，不符合退款条件',
    processNote: '物流单号SF123456已揽件'
  })
});
```

---

## 附录：refund_record 表结构

| 字段 | 类型 | 说明 |
|------|------|------|
| refund_id | bigint PK | 主键 |
| refund_no | varchar(64) | 退款编号（RF20260525120001123） |
| order_id | bigint | 订单主键 |
| order_no | varchar(64) | 订单号 |
| order_type | varchar(20) | 订单类型：goods |
| user_id | int | 用户ID |
| reason | varchar(50) | 退款原因 |
| description | varchar(500) | 详细描述 |
| images | text | 图片证明（JSON数组） |
| refund_amount | decimal(10,2) | 退款金额 |
| status | varchar(20) | pending / completed / rejected |
| process_time | datetime | 处理时间 |
| process_note | varchar(500) | 处理备注 |
| admin_reply | varchar(500) | 管理员回复 |
| create_time | datetime | 创建时间 |
| update_time | datetime | 更新时间 |
