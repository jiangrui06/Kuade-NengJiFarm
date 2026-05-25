# 菜品订单退款 API 接口文档

> 版本：v1.0
> 日期：2026-05-25
> 基础路径：`/api`
> Controller：`DishOrderController`
> Service：`DishOrderService`

---

## 目录

| # | 内容 |
|---|------|
| 一 | 订单状态说明 |
| 二 | 退款接口 |
| 三 | 错误码与异常处理 |
| 四 | 前端调用速查 |

---

## 一、订单状态说明

### 1.1 状态流转图

```
待支付(1) ──支付──> 备餐中(2) ──出餐──> 已完成(3)
                      │
                      └──(退款)──> 已取消(4)
```

括号内为 `order_status_id` 数据库值。

### 1.2 退款相关状态

| order_status_id | 显示状态 | 可退款 |
|:---------------:|---------|:------:|
| 1 | 待支付 | ❌ |
| 2 | 备餐中 | ✅ |
| 3 | 已完成 | ✅ |
| 4 | 已取消 | ❌（终态） |

菜品订单退款是**一键式**：管理员确认后直接完成退款，无需审核流程。

---

## 二、退款接口

### 2.1 接口信息

```
POST /api/dish/order/refund
Content-Type: application/json
Authorization: Bearer ***
```

### 2.2 请求体

| 字段 | 类型 | 必填 | 说明 |
|------|------|:----:|------|
| orderId | int | **是** | 菜品订单主键ID（数字型，非订单号） |
| refundReason | string | 否 | 退款原因 |

```json
{
  "orderId": 258,
  "refundReason": "菜品缺货"
}
```

> **注意**：此处 `orderId` 是数据库自增主键（数字型），可在订单列表接口返回数据中获取。

### 2.3 业务规则

| 校验项 | 规则 |
|--------|------|
| orderId | 不能为空，必须在菜品订单表中存在 |
| 订单状态 | `已取消`(4) 拒绝退款，`已完成`(3) 和 `备餐中`(2) 允许退款 |
| 重复退款 | 已退款的订单返回错误"该订单已完成退款，请勿重复操作" |

### 2.4 后端处理流程

```
1. 根据 orderId 查询菜品订单
2. 校验订单存在性 → 不存在返回错误
3. 校验订单状态 → 已取消/已完成状态拒绝
4. 幂等性检查 → 已退款返回错误
5. 调用微信退款（原路退回）
   - 检查 WxPayNo 是否为有效微信订单号（纯数字）
   - 跳过 MOCK_ / LOCKING: 开头的模拟订单
6. 更新订单状态 → order_status_id = 4（已取消）
7. 写入退款记录（refund_record），状态为 completed，order_type = "food"
8. 返回退款结果
```

### 2.5 成功响应

```json
{
  "code": 200,
  "message": "退款成功",
  "data": {
    "refundId": "RF20260525120001123",
    "orderId": "DISH20260514195446298670",
    "refundAmount": "158.00",
    "refundTime": "2026-05-25 12:00:00",
    "operator": "admin"
  }
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| refundId | string | 退款编号（RF + 时间戳 + 随机3位） |
| orderId | string | 菜品订单号（orderNo，非主键ID） |
| refundAmount | string | 退款金额（保留2位小数） |
| refundTime | string | 退款处理时间 |
| operator | string | 操作管理员账号 |

### 2.6 错误响应

```json
// 参数错误
{ "code": 400, "message": "请求参数不完整：orderId 不能为空" }

// 订单不存在
{ "code": 400, "message": "订单不存在或已被删除" }

// 已退款
{ "code": 400, "message": "该订单已完成退款，请勿重复操作" }

// 状态不允许
{ "code": 400, "message": "该订单已完成或已取消，无法退款" }

// 支付状态不允许
{ "code": 400, "message": "当前订单状态不允许退款" }

// 微信退款失败
{ "code": 400, "message": "微信退款失败：{具体原因}" }
```

---

## 三、错误码与异常处理

### 3.1 状态码

| 状态码 | 含义 |
|--------|------|
| 200 | 请求成功（业务结果看 code/message） |
| 400 | 参数校验失败或业务不允许 |
| 401 | 未登录/token 过期 |

### 3.2 业务错误消息

| 错误消息 | 触发条件 |
|----------|----------|
| 请求参数不完整：orderId 不能为空 | orderId 未传或为 0 |
| 订单不存在或已被删除 | orderId 查不到订单 |
| 该订单已完成退款，请勿重复操作 | 已存在退款完成记录 |
| 该订单已完成或已取消，无法退款 | 订单状态为已完成(3)或已取消(4) |
| 当前订单状态不允许退款 | 订单状态不在可退款范围 |
| 微信退款失败：{原因} | 支付网关退款异常 |

---

## 四、前端调用速查

### 4.1 通过 FarmAPI 封装（推荐）

```javascript
const data = await FarmAPI.dishOrder.refund({
  orderId: 258,         // 数字型主键
  refundReason: '菜品缺货'  // 可选
});

if (FarmAPI.isSuccessResponse(data)) {
  console.log('退款成功', data.data);
} else {
  console.error('退款失败', FarmAPI.getErrorMessage(data));
}
```

### 4.2 直接 fetch

```javascript
const response = await fetch(FarmAPI.url('/api/dish/order/refund'), {
  method: 'POST',
  headers: Auth.getAuthHeaders({ 'Content-Type': 'application/json' }),
  body: JSON.stringify({
    orderId: 258,
    refundReason: '菜品缺货'
  })
});
```

---

## 附录：refund_record 表结构（菜品订单）

| 字段 | 值 | 说明 |
|------|:---:|------|
| refund_no | 自动生成 | RF + 时间戳 + 随机3位 |
| order_type | `"food"` | 菜品订单固定值 |
| reason | `"admin_refund"` | 固定值 |
| status | `"completed"` | 退款直接完成，无审核流程 |
