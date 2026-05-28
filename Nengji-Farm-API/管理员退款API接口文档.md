# 管理员退款 API 接口文档

## 基础信息

- **Base URL**: `http://192.168.101.50`
- **Content-Type**: `application/json`
- **认证**: Bearer Token（管理后台管理员登录后获取）

---

## 1. 商品订单退款

```
POST /api/product/order/refund
```

### Request Body

```json
{
  "orderNo": "ACT202605281234567890",
  "orderId": 1,
  "refundReason": "商品质量问题"
}
```

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `orderNo` | string | 条件必填 | 订单号（与 orderId 二选一） |
| `orderId` | long | 条件必填 | 订单 ID（与 orderNo 二选一） |
| `refundReason` | string | 否 | 退款原因 |

**注意**: `orderNo` 和 `orderId` 必须至少传一个，否则返回 `"请求参数不完整：orderNo 或 orderId 不能为空"`。

### Response

```json
{
  "code": 200,
  "message": "退款成功",
  "data": {
    "refundId": "RF202605281234567890123",
    "orderId": "1",
    "refundAmount": "99.00",
    "refundTime": "2026-05-28 14:30",
    "operator": "admin01"
  }
}
```

---

## 2. 活动券订单退款

```
POST /api/activity-order/refund
```

支持三种操作：首次退款、确认退款、驳回退款，通过 `action` 字段区分。

### Request Body

```json
{
  "orderNo": "ACT202605281234567890",
  "orderId": 1,
  "refundReason": "活动取消，申请退款",
  "action": "",
  "refundId": "",
  "adminReply": "",
  "processNote": ""
}
```

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `orderNo` | string | 条件必填 | 订单号（首次退款时必填，与 orderId 二选一） |
| `orderId` | long | 条件必填 | 订单 ID（首次退款时必填，与 orderNo 二选一） |
| `refundReason` | string | 否 | 退款原因（首次退款时建议传） |
| `action` | string | 否 | `reject`（驳回退款）、`refund-process`（确认退款） |
| `refundId` | string | 条件必填 | 退款编号（驳回/确认退款时必填，填 RefundNo） |
| `adminReply` | string | 否 | 管理员回复（驳回原因） |
| `processNote` | string | 否 | 处理备注 |

**注意**:
- 首次退款时，`orderNo` 和 `orderId` 必须至少传一个，否则返回 `"请求参数不完整：orderNo 或 orderId 不能为空"`
- 驳回/确认退款时只需传 `action` + `refundId`，不需要传 `orderNo`

### 三种场景

#### 场景一：首次申请退款
```json
{
  "orderNo": "ACT202605281234567890",
  "refundReason": "活动取消"
}
```

#### 场景二：确认退款
```json
{
  "refundId": "RF202605281234567890123",
  "action": "refund-process"
}
```

#### 场景三：驳回退款
```json
{
  "refundId": "RF202605281234567890123",
  "action": "reject",
  "adminReply": "活动可正常举行"
}
```

---

## 3. 菜品订单退款

```
POST /api/dish-order/refund
```

```json
{
  "orderNo": "ACT202605281234567890",
  "orderId": 1,
  "refundReason": "菜品与描述不符"
}
```

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `orderNo` | string | 条件必填 | 订单号（与 orderId 二选一） |
| `orderId` | long | 条件必填 | 订单 ID（与 orderNo 二选一） |
| `refundReason` | string | 否 | 退款原因 |

---

## 4. 小程序用户退款（与管理员流程不同）

```
POST /api/orders/{id}/refund
```

**注意**: 这是小程序端用户自行发起的退款，不走管理后台。

- 订单标识 `{id}` 通过 URL 路径参数传入，不是放在请求体
- 请求体中只需要传退款原因、说明、图片
- 走真实微信退款：成功则直接标记为已退款，失败则保持退款中等待管理员处理

```json
{
  "reason": "商品/菜品与描述不符",
  "description": "具体说明",
  "images": ["https://..."]
}
```

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `reason` | string | 是 | 退款原因（固定枚举值） |
| `description` | string | 否 | 具体说明（"其他原因"时必填） |
| `images` | string[] | 否 | 凭证图片 URL 列表（最多 3 张） |

### 有效 reason 枚举

```
商品/菜品与描述不符
商品破损/质量问题
与预期不符
配送延迟
重复下单
其他原因
```

---

## 常见问题

### Q: 为什么返回 "请求参数不完整：orderNo 或 orderId 不能为空"？

管理后台三个退款接口（商品/活动券/菜品）都要求请求体中必须包含 `orderNo` 或 `orderId` 字段（至少一个）。

**检查项**:

1. **请求体是否为空** — 确保传了 JSON body 且格式正确
2. **字段名是否正确** — JSON 字段名是 `orderNo`（驼峰），不是 `order_no`、`OrderNo` 等
3. **值是否有效** — `orderNo` 不能是空字符串 `""`，`orderId` 必须大于 0

### Q: 小程序退款也要求 orderNo 吗？

不要求。小程序退款走 `POST /api/orders/{id}/refund`，订单号通过 URL 路径传入，不在请求体中。
