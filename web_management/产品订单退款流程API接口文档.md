# 产品订单退款流程 API 接口文档（前端专用）

> 本文档专为前端 `order-product.html` 退款流程设计，描述退款相关接口的状态流转、前端注意事项和修复方案。

---

## 一、订单状态生命周期（退款相关）

### 1.1 零售产品订单状态流转图

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

### 1.2 退款相关状态映射

后端 `MapRetailStatus` 方法映射关系：

| order_status_id | orderStatus（显示） | paymentStatus（支付状态） | deliveryNote（说明） |
|---|---|---|---|
| 2 | 待发货 | 已支付 | 待仓库发货 |
| 5 | 已取消 | 已退款 | 订单已取消 |
| 6 | 退款中 | 已支付 | 客户已申请退款，等待平台处理 |
| 7 | 已退款 | 已退款 | 退款已处理完成 |

---

## 二、退款相关 API 接口

### 2.1 申请退款（管理员代客户申请）

| 项目 | 内容 |
|---|---|
| 请求路径 | `PUT /api/product/order/updateStatus` |
| 说明 | 仅待发货订单可申请退款，将订单状态改为退款中 |

#### 请求体

```json
{
  "orderId": "P202604010001",
  "action": "refund-request",
  "refundReason": "客户反馈包装破损",
  "refundProofImages": ["base64图片数据"]
}
```

#### 后端处理

- 检查 `OrderStatusId` 是否为 2（待发货）
- 将 `OrderStatusId` 改为 6（退款中）
- 新增一条 `RefundRecord`，状态为 `pending`

#### 成功响应

```json
{
  "code": 200,
  "message": "操作成功",
  "data": null
}
```

---

### 2.2 处理退款（管理员确认退款）

| 项目 | 内容 |
|---|---|
| 请求路径 | `PUT /api/product/order/updateStatus` |
| 说明 | 仅退款中订单可处理退款，将订单状态改为已退款 |

#### 请求体

```json
{
  "orderId": "P202604010001",
  "action": "refund-process"
}
```

#### 后端处理

- 检查 `OrderStatusId` 是否为 6（退款中）
- 将 `OrderStatusId` 改为 7（已退款）
- 更新 `RefundRecord`：`Status = "completed"`，记录处理时间

#### 成功响应

```json
{
  "code": 200,
  "message": "操作成功",
  "data": null
}
```

---

### 2.3 驳回退款（管理员拒绝退款）

| 项目 | 内容 |
|---|---|
| 请求路径 | `PUT /api/product/order/updateStatus` |
| 说明 | 仅退款中订单可驳回退款，将订单状态恢复为待发货 |

#### 请求体

```json
{
  "orderId": "P202604010001",
  "action": "refund-reject",
  "adminReply": "不符合退款条件",
  "processNote": "已核实"
}
```

#### 后端处理

- 检查 `OrderStatusId` 是否为 6（退款中）
- 将 `OrderStatusId` 改为 2（待发货）
- 更新 `RefundRecord`：`Status = "rejected"`，记录驳回原因和处理时间

#### 成功响应

```json
{
  "code": 200,
  "message": "操作成功",
  "data": null
}
```

---

### 2.4 获取订单列表（含退款信息）

| 项目 | 内容 |
|---|---|
| 请求路径 | `GET /api/product/order/list` |
| Query参数 | `pageNum`, `pageSize`, `keyword`（按订单号/微信/姓名/电话模糊搜索） |

#### 退款相关返回字段

| 字段 | 类型 | 说明 | 示例 |
|---|---|---|---|
| `orderStatus` | String | 订单状态显示文字 | `"退款中"` / `"已退款"` |
| `paymentStatus` | String | 支付状态 | `"已支付"` / `"已退款"` |
| `deliveryNote` | String | 状态说明 | `"客户已申请退款，等待平台处理"` |
| `refundReason` | String/Null | 退款原因 | `"包装破损"` |
| `refundApplyTime` | String/Null | 退款申请时间 | `"2026-05-21 10:00"` |
| `refundProofImages` | String[]/Null | 退款图片证明 | `["url1", "url2"]` |

#### 响应示例（退款中订单）

```json
{
  "code": 200,
  "message": "获取成功",
  "data": {
    "records": [
      {
        "orderId": "P202604010001",
        "orderStatus": "退款中",
        "paymentStatus": "已支付",
        "deliveryNote": "客户已申请退款，等待平台处理",
        "refundReason": "客户反馈包装破损",
        "refundApplyTime": "2026-05-21 10:00",
        "refundProofImages": ["https://example.com/proof1.jpg"]
      }
    ],
    "total": 1,
    "pageNum": 1,
    "pageSize": 15,
    "pages": 1
  }
}
```

#### 响应示例（已退款订单）

```json
{
  "code": 200,
  "message": "获取成功",
  "data": {
    "records": [
      {
        "orderId": "P202604010001",
        "orderStatus": "已退款",
        "paymentStatus": "已退款",
        "deliveryNote": "退款已处理完成",
        "refundReason": "客户反馈包装破损",
        "refundApplyTime": "2026-05-21 10:00",
        "refundProofImages": ["https://example.com/proof1.jpg"]
      }
    ],
    "total": 1,
    "pageNum": 1,
    "pageSize": 15,
    "pages": 1
  }
}
```

---

## 三、前端筛选器问题（重要！）

### 3.1 问题描述

当管理员在"退款中"筛选标签下处理退款时，出现以下现象：

1. 管理员选择状态筛选器 `productStatusFilter = "退款中"`
2. 看到退款中订单，点击"处理退款"→ 确认
3. API 调用成功，后端将 `order_status_id` 从 6 改为 7
4. 前端调用 `fetchProductOrders()` 刷新列表
5. **但 `productStatusFilter` 仍为 `"退款中"`**
6. 已退款订单（`orderStatus` 现为 `"已退款"`）**不匹配**"退款中"筛选条件
7. 订单从列表中消失，页面显示"暂无产品订单数据"
8. 管理员误以为数据丢失、状态未更改

### 3.2 根因

前端 `fetchProductOrders()` 函数在获取数据后，会调用 `filterProductOrdersByStatus` 按当前 `productStatusFilter` 过滤：

```javascript
matchesProductStatusFilter(order, status) {
    if (!status) return true;  // 无筛选 → 显示全部
    const displayStatus = this.getProductOrderDisplayStatus(order?.orderStatus);
    if (status === '已退款') {
        return displayStatus === '已退款' || order?.paymentStatus === '已退款';
    }
    return displayStatus === status;
}
```

当 `productStatusFilter = "退款中"` 时，只有 `displayStatus === "退款中"` 的订单才会显示。退款处理后 `displayStatus` 变为 `"已退款"`，所以被过滤掉。

### 3.3 修复方案

**在 `doProcessRefund` 方法中，处理退款前先清除状态筛选器：**

```javascript
doProcessRefund(order) {
    this.productStatusFilter = '';  // 清除筛选，回到全部列表
    return this.requestProductOrderStatusUpdate(order, 'refund-process');
}
```

### 3.4 同理适用于以下操作

| 操作 | action 值 | 操作后订单状态 | 建议 |
|---|---|---|---|
| 处理退款 | `refund-process` | 退款中 → 已退款 | 清除筛选器 |
| 驳回退款 | `refund-reject` | 退款中 → 待发货 | 清除筛选器 |
| 取消待发货 | `cancel-pending-shipment` | 待发货 → 已取消 | 清除筛选器 |
| 取消待收货 | `cancel-pending-receipt` | 待收货 → 已取消 | 清除筛选器 |
| 发货 | `ship` | 待发货 → 待收货 | 清除筛选器 |

---

## 四、前端按钮显隐逻辑（退款相关）

### 4.1 "处理退款"按钮

```javascript
canProcessRefund(order) {
    return !this.isSubscriptionProductOrder(order)
        && this.getProductOrderDisplayStatus(order.orderStatus) === '退款中'
        && order.paymentStatus !== '已退款';
}
```

**显示条件**：零售订单 && `orderStatus === "退款中"` && `paymentStatus !== "已退款"`

### 4.2 "查看退款详情"按钮

```javascript
canViewRefundStatus(order) {
    if (!order || this.isSubscriptionProductOrder(order)) return false;
    const status = this.getProductOrderDisplayStatus(order.orderStatus);
    return status === '退款中'
        || status === '已退款'
        || order.paymentStatus === '已退款'
        || Boolean(order.refundReason || this.getRefundProofImages(order).length);
}
```

**显示条件**：零售订单 && （订单为退款中/已退款 或 有退款原因/图片）

### 4.3 状态 CSS 类

| 显示状态 | CSS 类 | 颜色 |
|---|---|---|
| 已完成 | `completed` | 绿色 |
| 已退款 | `refunded` | 红色 |
| 待支付 | `unpaid` | 红色 |
| 待发货 / 待收货 / 退款中 / 认购中 | `processing` | 橙色 |
| 其他 | `pending` | 灰色 |

---

## 五、RefundRecord 表结构说明（前端了解）

`refund_record` 表中与前端退款展示相关的字段：

| 字段 | 类型 | 说明 | 对应前端字段 |
|---|---|---|---|
| `status` | String | `pending`=退款中, `completed`=已退款, `rejected`=已驳回 | 详情页 refundStatus |
| `reason` | String | 退款原因 | `refundReason` |
| `images` | Text | 图片JSON数组 | `refundProofImages` |
| `admin_reply` | String | 管理员驳回回复 | 详情页 adminReply |
| `process_note` | String | 处理备注 | 详情页 processNote |
| `process_time` | DateTime | 处理时间 | 详情页 |
| `create_time` | DateTime | 申请时间 | `refundApplyTime` |

---

## 六、FAQ

**Q: 处理退款后订单去哪了？**
A: 订单状态已改为"已退款"，但当前筛选器仍为"退款中"，所以看不到。切换筛选器到"全部状态"或"已退款"即可看到。

**Q: 如何确认退款是否成功？**
A: 查看订单状态是否变为"已退款"，支付状态是否变为"已退款"。也可通过"查看退款详情"查看退款记录。

**Q: 驳回退款后订单状态变为什么？**
A: 订单状态恢复为"待发货"，可重新发货或再次申请退款。

**Q: 退款图片最多几张？**
A: 最多 3 张，支持 base64 格式上传。
