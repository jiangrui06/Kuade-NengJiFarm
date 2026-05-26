# 菜品订单退款 API 接口文档

> 版本：v2.0
> 日期：2026-05-26
> 基础路径：`/api/dish/order`
> Controller：`DishOrderController`

---

## 一、接口说明

菜品订单退款后台管理员操作，一步完成：微信退款 → 写入退款记录 → 更新订单状态。

> 菜品退款**不恢复菜品库存**（菜品库存由 `InventoryService.DeductAsync` 管理，退款时不做反向操作）。

---

## 二、退款 POST /api/dish/order/refund

### 2.1 请求地址

- **URL：** `/api/dish/order/refund`
- **Method：** `POST`
- **Content-Type：** `application/json`

### 2.2 请求参数

```json
{
  "orderNo": "DISH20260522201714974182",
  "refundReason": "管理员退款"
}
```

| 参数名 | 类型 | 必填 | 说明 |
|--------|------|------|------|
| `orderNo` | string | **是** | **订单号** |
| `refundReason` | string | 否 | 退款原因 |

### 2.3 响应结构

```json
{
  "code": 200,
  "message": "退款成功",
  "data": {
    "refundId": "RF20260526142000123456",
    "orderId": "DISH20260522201714974182",
    "refundAmount": "88.00",
    "refundTime": "2026-05-26 14:20",
    "operator": "admin"
  }
}
```

### 2.4 退款流程

1. **参数校验** — 检查 `orderNo` 不为空
2. **Token 验证** — 从 Bearer token 提取操作人
3. **订单查询** — 按 `OrderNo` 查找 `dish_orders`
4. **幂等性检查** — 已有 `OrderType = "food"` 的退款记录则拒绝
5. **状态检查** — 仅待出餐/备餐中（status=1或2）可退款，已完成/已取消（status=3或4）不可退款
6. **微信退款** — 调用微信支付退款（真实 `WxPayNo` 才走微信）
7. **写入记录** — 创建 `RefundRecord`，`OrderType = "food"`，`Reason = "管理员退款"`
8. **更新状态** — `order_status_id = 4`（已取消/已退款）

### 2.5 微信退款条件

仅当 `WxPayNo` 满足以下**所有**条件时才调用微信退款：
- 不为空
- 不以 `MOCK_` 开头
- 不以 `LOCKING:` 开头
- 全为数字（`All(char.IsDigit)`）

### 2.6 异常响应

| code | message | 说明 |
|------|---------|------|
| 400 | 请求参数不完整：orderNo 不能为空 | 未提供订单号 |
| 400 | 订单不存在或已被删除 | 未找到订单 |
| 400 | 该订单已完成退款，请勿重复操作 | 已退款 |
| 400 | 该订单已完成或已取消，无法退款 | 状态不合法 |
| 400 | 当前订单状态不允许退款 | 状态不合法 |
| 400 | 微信退款失败：{原因} | 微信接口异常 |
| 401 | 登录已过期，请重新登录 | Token 无效 |

### 2.7 退款记录字段

| 字段 | 值 |
|------|-----|
| `OrderType` | `"food"` |
| `Status` | `"completed"` |
| `Reason` | `"管理员退款"` |
| `Description` | `request.RefundReason`（前端传入） |

---

## 三、向后兼容说明

菜品退款接口始终使用 `orderNo`（订单号）作为唯一标识，不使用数字主键 `orderId`。

| 对比 | 取值 |
|------|------|
| 查询字段 | `OrderNo`（字符串，如 `DISH20260522201714974182`） |
| 数据表 | `dish_orders` |
| 列表返回的订单号字段 | `orderId`（DishOrderListItemDto） |

---

## 四、后端改动记录

| 文件 | 改动 |
|------|------|
| `Services/DishOrderService.cs` | `RefundAsync` 按 `OrderNo` 查订单；`Reason` 改为中文"管理员退款" |
| `Dtos/DishOrderListItemDto.cs` | 新增 `dishOrderId`(long) 供前端取数字 PK（预防性） |
