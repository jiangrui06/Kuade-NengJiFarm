# 产品订单 API 接口文档

本文档基于以下前端页面整理，用于后端接口开发和前后端联调：

- [order-product.html](/e:/Kuade-NengJiFarm/web_management/order-product.html)
- [order-product-detail.html](/e:/Kuade-NengJiFarm/web_management/order-product-detail.html)
- [order-subscription.html](/e:/Kuade-NengJiFarm/web_management/order-subscription.html)

> **说明**：`order-subscription.html` 当前已跳转到 `order-product.html`，认购一亩田订单在产品订单中统一管理（以 `orderCategory='subscription'` 或订单ID以 `S` 开头区分）。

## 一、公共约定

### 1.1 请求头

| 字段 | 是否必传 | 类型 | 说明 |
|---|---|---|---|
| `Content-Type` | 是 | String | 固定为 `application/json` |
| `token` | 建议 | String | 后台登录成功后返回的认证令牌 |

### 1.2 统一响应结构

| 字段 | 类型 | 说明 |
|---|---|---|
| `code` | Number | `200` 表示成功 |
| `message` | String | 提示文案 |
| `data` | Object / Array / Null | 业务数据 |

```json
{
  "code": 200,
  "message": "操作成功",
  "data": {}
}
```

### 1.3 时间格式

页面统一按 `yyyy-MM-dd HH:mm` 格式展示时间。

示例：`2026-04-01 12:00`

### 1.4 订单ID格式

| 订单类型 | 前缀格式 | 示例 |
|---|---|---|
| 产品订单（零售） | `P` + 年月日时分秒 | `P202604010001` |
| 认购订单 | `S` + 年月日时分秒 | `S202604240001` |
| 预约/认养订单 | `G` + 年月日时分秒+随机码 | `G00052026e519144006214167` |

## 二、产品订单

产品订单包含两类：

- **零售产品订单**：普通商品购买，配送方式为快递配送或到店自提
- **认购一亩田订单**：农田认购，有签约、认购履约等特殊流程

### 2.1 字段说明

**零售产品订单（order-product.html）列表字段**：

| 字段 | 类型 | 是否必传 | 示例 | 说明 |
|---|---|---|---|---|
| `commodityOrderId` | Number | 是 | `1` | 数字主键ID（用于退款等操作） |
| `orderId` | String | 是 | `P202604010001` | 订单唯一ID |
| `orderSource` | String | 是 | `商城下单` | 订单来源 |
| `customerWechat` | String | 是 | `wx_orchard_229` | 客户微信号 |
| `contactPhone` | String | 是 | `13723533044` | 联系电话 |
| `receiverName` | String | 是 | `李四` | 收货人姓名 |
| `productSummary` | String | 是 | `有机草莓礼盒、农家土鸡蛋` | 商品摘要 |
| `itemCount` | Number | 是 | `3` | 商品种类数 |
| `actualAmount` | Number | 是 | `158.00` | 实付金额 |
| `deliveryMethod` | String | 是 | `快递配送` | 配送方式：`快递配送` / `到店自提` |
| `logisticsType` | String | 否 | `顺丰` | 物流类型（如：顺丰、邮政） |
| `logisticsNo` | String | 否 | `SF202604010188` | 物流单号 |
| `deliveryNote` | String | 否 | `待仓库发货` | 履约说明 |
| `paymentMethod` | String | 是 | `微信支付` | 支付方式 |
| `paymentStatus` | String | 是 | `已支付` | 支付状态 |
| `orderStatus` | String | 是 | `待发货` | 订单状态 |
| `orderTime` | String | 是 | `2026-04-01 09:20` | 下单时间 |

**零售产品订单状态值**：

| 状态值 | 说明 |
|---|---|
| `待支付` | 等待客户支付 |
| `待发货` | 已支付，等待仓库发货 |
| `待收货` | 已发货，等待客户签收 |
| `退款中` | 客户申请退款，等待处理 |
| `已退款` | 退款已完成 |
| `已完成` | 订单已完成（含取消） |

**认购一亩田订单（order-product.html）附加/特殊字段**：

| 字段 | 类型 | 是否必传 | 示例 | 说明 |
|---|---|---|---|---|
| `orderCategory` | String | 是 | `subscription` | 订单分类标识 |
| `orderSource` | String | 是 | `认购一亩田` | 固定为认购一亩田 |
| `fieldName` | String | 是 | `东区一亩田-01` | 认购田块名称 |
| `plotNo` | String | 是 | `DT-01` | 地块编号 |
| `period` | String | 是 | `12个月` | 认购周期 |
| `userName` | String | 是 | `林子涵` | 认购用户姓名 |
| `signTime` | String | 否 | `2026-04-24 11:20` | 签约时间 |
| `completeTime` | String | 否 | `2026-04-24 16:40` | 完成时间 |
| `remark` | String | 否 | `客户希望五一前完成签约` | 订单备注 |

**认购一亩田订单状态值**：

| 状态值 | 说明 |
|---|---|
| `待支付` | 等待客户完成认购支付 |
| `待签约` | 支付完成，等待后台确认签约 |
| `认购中` | 已签约，认购履约中 |
| `已退款` | 认购订单已退款 |
| `已完成` | 认购订单已完成 |

**配送方式说明**：

| 配送方式 | 说明 |
|---|---|
| `快递配送` | 零售产品快递配送 |
| `到店自提` | 零售产品到店自提 |
| `一亩田认购` | 认购一亩田配送 |

**详情页（order-product-detail.html）附加结构**：

| 结构 | 字段 | 说明 |
|---|---|---|
| `orderInfo` | `orderNo`, `orderType`, `createTime`, `orderStatus`, `paymentStatus`, `deliveryMethod`, `logisticsType`, `deliveryNote`, `totalAmount`, `paymentMethod` | 订单基本信息 |
| `orderItems` | `productId`, `image`, `name`, `description`, `spec`, `netWeight`, `quantity`, `price`, `subtotal` | 商品明细 |
| `logisticsRecords` | `logisticsType`, `nodeName`, `handler`, `status`, `updateTime`, `remark` | 物流明细 |
| `buyerInfo` | `nickname`, `name`, `customerWechat`, `phone`, `memberLevel`, `orderSource`, `remark` | 买家信息 |
| `fulfillmentInfo` | `receiverName`, `phone`, `address`, `schedule`, `trackingNo`, `remark` | 履约信息（收货地址/自提信息） |

**orderItems（商品明细）结构**：

| 字段 | 类型 | 说明 |
|---|---|---|
| `productId` | String | 商品ID |
| `image` | String | 商品图片 |
| `name` | String | 商品名称 |
| `description` | String | 商品描述 |
| `spec` | String | 规格说明 |
| `netWeight` | String | 净含量 |
| `quantity` | Number | 购买数量 |
| `price` | Number | 单价 |
| `subtotal` | Number | 小计金额 |

**logisticsRecords（物流明细）结构**：

| 字段 | 类型 | 说明 |
|---|---|---|
| `logisticsType` | String | 物流类型 |
| `nodeName` | String | 处理节点 |
| `handler` | String | 承运/处理信息 |
| `status` | String | 状态 |
| `updateTime` | String | 更新时间 |
| `remark` | String | 备注 |

**fulfillmentInfo（履约信息）结构**：

| 字段 | 类型 | 说明 |
|---|---|---|
| `receiverName` | String | 收货人姓名 |
| `phone` | String | 联系电话 |
| `address` | String | 收货地址 / 自提门店地址 |
| `schedule` | String | 预约时间 / 物流公司 |
| `trackingNo` | String | 物流单号 / 提货码 |
| `remark` | String | 补充说明 |

### 2.2 获取产品订单列表

| 项目 | 内容 |
|---|---|
| 接口名称 | 获取产品订单列表 |
| 请求路径 | `/api/product/order/list` |
| 请求方式 | `GET` |
| 接口说明 | 用于 `order-product.html` 列表页，支持零售订单和认购订单 |

#### Query 参数

| 字段 | 是否必传 | 类型 | 示例 | 说明 |
|---|---|---|---|---|
| `pageNum` | 是 | Number | `1` | 页码 |
| `pageSize` | 是 | Number | `15` | 每页条数 |
| `keyword` | 否 | String | `李四` | 按订单ID、客户微信、收货人、用户姓名、电话、田块名、地块编号搜索 |
| `statusId` | 否 | Number | `2` | 按订单状态ID筛选（从 statuses 接口获取） |

#### 成功响应示例（零售订单）

```json
{
  "code": 200,
  "message": "获取成功",
  "data": {
    "records": [
      {
        "commodityOrderId": 1,
        "orderId": "P202604010001",
        "orderSource": "商城下单",
        "customerWechat": "wx_orchard_229",
        "contactPhone": "13723533044",
        "receiverName": "李四",
        "productSummary": "有机草莓礼盒、农家土鸡蛋",
        "itemCount": 3,
        "actualAmount": 158.00,
        "deliveryMethod": "快递配送",
        "logisticsType": "顺丰",
        "logisticsNo": "",
        "deliveryNote": "待仓库发货",
        "paymentMethod": "微信支付",
        "paymentStatus": "已支付",
        "orderStatus": "待发货",
        "orderTime": "2026-04-01 09:20"
      }
    ],
    "total": 7,
    "pageNum": 1,
    "pageSize": 15,
    "pages": 1
  }
}
```

#### 成功响应示例（认购订单）

```json
{
  "code": 200,
  "message": "获取成功",
  "data": {
    "records": [
      {
        "commodityOrderId": 2,
        "orderId": "S202604240001",
        "orderCategory": "subscription",
        "orderSource": "认购一亩田",
        "customerWechat": "wx_field_family_001",
        "contactPhone": "13900139001",
        "receiverName": "林子涵",
        "userName": "林子涵",
        "fieldName": "东区一亩田-01",
        "plotNo": "DT-01",
        "period": "12个月",
        "productSummary": "春季时令一亩田认购",
        "itemCount": 1,
        "actualAmount": 3980.00,
        "deliveryMethod": "一亩田认购",
        "logisticsType": "东区一亩田-01",
        "logisticsNo": "DT-01",
        "deliveryNote": "支付完成，待后台确认签约",
        "paymentMethod": "微信支付",
        "paymentStatus": "已支付",
        "orderStatus": "待签约",
        "orderTime": "2026-04-24 09:26",
        "signTime": "",
        "completeTime": "",
        "remark": "客户希望五一前完成签约"
      }
    ],
    "total": 6,
    "pageNum": 1,
    "pageSize": 15,
    "pages": 1
  }
}
```

### 2.3 获取产品订单详情

| 项目 | 内容 |
|---|---|
| 接口名称 | 获取产品订单详情 |
| 请求路径 | `/api/product/order/detail` |
| 请求方式 | `GET` |
| 接口说明 | 用于 `order-product-detail.html` 详情页 |

#### Query 参数

| 字段 | 是否必传 | 类型 | 示例 | 说明 |
|---|---|---|---|---|
| `orderNo` | 是 | String | `P202604010001` | 订单ID |
| `phone` | 否 | String | `13723533044` | 客户电话（辅助查询） |

#### 成功响应示例（零售订单）

```json
{
  "code": 200,
  "message": "获取成功",
  "data": {
    "orderInfo": {
      "orderNo": "P202604010001",
      "orderType": "农产品购买",
      "createTime": "2026-04-01 09:20",
      "orderStatus": "待发货",
      "paymentStatus": "已支付",
      "deliveryMethod": "快递配送",
      "logisticsType": "冷链配送",
      "deliveryNote": "顺丰冷链配送",
      "totalAmount": 158.00,
      "paymentMethod": "微信支付"
    },
    "orderItems": [
      {
        "productId": "20260401090013",
        "image": "https://example.com/strawberry.jpg",
        "name": "有机草莓礼盒",
        "description": "有机草莓礼盒，新鲜采摘",
        "spec": "冷链礼盒装",
        "netWeight": "2kg/箱",
        "quantity": 1,
        "price": 66.00,
        "subtotal": 66.00
      }
    ],
    "logisticsRecords": [
      {
        "logisticsType": "冷链配送",
        "nodeName": "订单审核",
        "handler": "商城系统",
        "status": "已完成",
        "updateTime": "2026-04-01 09:22",
        "remark": "已确认付款与收货信息。"
      }
    ],
    "buyerInfo": {
      "nickname": "果园慢生活",
      "name": "李四",
      "customerWechat": "wx_orchard_229",
      "phone": "13723533044",
      "memberLevel": "普通会员",
      "orderSource": "微信商城下单",
      "remark": "希望优先发货，保鲜包装完整。"
    },
    "fulfillmentInfo": {
      "receiverName": "李四",
      "phone": "13723533044",
      "address": "浙江省杭州市西湖区文三路 88 号",
      "schedule": "顺丰冷链",
      "trackingNo": "SF202604010188",
      "remark": "工作日白天可收货"
    }
  }
}
```

#### 成功响应示例（认购一亩田订单）

```json
{
  "code": 200,
  "message": "获取成功",
  "data": {
    "orderInfo": {
      "orderNo": "S202604240001",
      "orderType": "认购一亩田",
      "createTime": "2026-04-24 09:26",
      "orderStatus": "待签约",
      "paymentStatus": "已支付",
      "deliveryMethod": "一亩田认购",
      "logisticsType": "东区一亩田-01",
      "deliveryNote": "支付完成，待后台确认签约",
      "totalAmount": 3980.00,
      "paymentMethod": "微信支付"
    },
    "orderItems": [
      {
        "productId": "subscription",
        "image": "",
        "name": "春季时令一亩田认购",
        "description": "春季专属田地 666㎡ | 春季蔬菜套餐配送 | 预计春季产 800kg 有机蔬菜",
        "spec": "一亩田认购：编号 YT-2026-0001",
        "netWeight": "1亩",
        "quantity": 1,
        "price": 3980.00,
        "subtotal": 3980.00
      }
    ],
    "logisticsRecords": [
      {
        "logisticsType": "一亩田认购",
        "nodeName": "认购确认",
        "handler": "商城系统",
        "status": "已完成",
        "updateTime": "2026-04-24 09:30",
        "remark": "认购一亩田已确认。"
      }
    ],
    "buyerInfo": {
      "nickname": "田园认购",
      "name": "林子涵",
      "customerWechat": "wx_field_family_001",
      "phone": "13900139001",
      "memberLevel": "金卡会员",
      "orderSource": "认购一亩田",
      "remark": "客户希望五一前完成签约"
    },
    "fulfillmentInfo": {
      "receiverName": "林子涵",
      "phone": "13900139001",
      "address": "农场服务中心",
      "schedule": "一亩田认购",
      "trackingNo": "DT-01",
      "remark": "认购一亩田客户"
    }
  }
}
```

### 2.4 更新产品订单状态

| 项目 | 内容 |
|---|---|
| 接口名称 | 更新产品订单状态 |
| 请求路径 | `/api/product/order/updateStatus` |
| 请求方式 | `PUT` |
| 接口说明 | 用于列表页对订单进行取消、发货、退款、签约、完成等操作 |

#### Body 参数

| 字段 | 是否必传 | 类型 | 示例 | 说明 |
|---|---|---|---|---|
| `orderId` | 是 | String | `P202604010001` | 订单ID |
| `action` | 是 | String | `ship` | 操作类型 |
| `logisticsType` | 条件必传 | String | `顺丰` | 发货时必传，物流类型 |
| `logisticsNo` | 条件必传 | String | `SF202604010188` | 发货时必传，物流单号 |
| `refundReason` | 条件必传 | String | `收到商品后发现包装破损` | 申请退款时必传 |
| `refundProofImages` | 否 | String[] | `["base64...", "base64..."]` | 退款图片证明（最多3张） |

**action 操作类型说明**：

| action 值 | 说明 | 附加参数 |
|---|---|---|
| `cancel-pending-payment` | 取消待支付订单 | 无 |
| `cancel-pending-shipment` | 取消待发货订单 | 无（自动退款） |
| `cancel-pending-receipt` | 取消待收货订单 | 无（自动退款关闭物流） |
| `ship` | 发货 | `logisticsType`, `logisticsNo` |
| `refund-request` | 申请退款 | `refundReason`, `refundProofImages` |
| `refund-process` | 处理退款 | 无 |
| `subscription-sign` | 认购签约 | 无 |
| `subscription-complete` | 认购完成 | 无 |

#### 请求示例（发货）

```json
{
  "orderId": "P202604010001",
  "action": "ship",
  "logisticsType": "顺丰",
  "logisticsNo": "SF202604010188"
}
```

### 2.5 获取产品订单状态列表

| 项目 | 内容 |
|---|---|
| 接口名称 | 获取产品订单状态列表 |
| 请求路径 | `/api/product/order/statuses` |
| 请求方式 | `GET` |
| 接口说明 | 获取产品订单所有可用状态值，用于列表页状态筛选下拉框 |

无请求参数。

#### 成功响应示例

```json
{
  "code": 200,
  "message": "获取成功",
  "data": [
    { "statusId": 1, "statusName": "待支付" },
    { "statusId": 2, "statusName": "待发货" },
    { "statusId": 3, "statusName": "待收货" },
    { "statusId": 4, "statusName": "退款中" },
    { "statusId": 5, "statusName": "已退款" },
    { "statusId": 6, "statusName": "已完成" }
  ]
}
```

### 2.6 产品订单退款

| 项目 | 内容 |
|---|---|
| 接口名称 | 产品订单退款（后台管理员操作） |
| 请求路径 | `/api/product/order/refund` |
| 请求方式 | `POST` |
| 接口说明 | 后台管理员对已支付的产品订单进行退款操作（调用微信支付退款） |

#### Body 参数

| 字段 | 是否必传 | 类型 | 示例 | 说明 |
|---|---|---|---|---|
| `orderNo` | 条件必传 | String | `P202604010001` | 订单号（与 `orderId` 二选一，优先使用） |
| `orderId` | 条件必传 | Number | `1` | 数字主键ID（与 `orderNo` 二选一） |
| `refundReason` | 否 | String | `管理员退款` | 退款原因 |

#### 请求示例

```json
{
  "orderNo": "P202604010001",
  "refundReason": "管理员退款"
}
```

#### 成功响应

```json
{
  "code": 200,
  "message": "退款成功",
  "data": {
    "refundId": "500001",
    "orderId": "P202604010001"
  }
}
```

## 三、通用失败场景

| 场景 | 状态码 | 提示文案 |
|---|---|---|
| token 无效或过期 | `401` | 登录已过期，请重新登录 |
| 权限不足 | `403` | 权限不足，仅管理员可操作 |
| 参数错误 | `400` | 请求参数不完整或格式错误 |
| 订单不存在 | `404` | 订单不存在或已被删除 |
| 订单状态不允许该操作 | `422` | 当前订单状态不允许此操作 |
| 服务异常 | `500` | 服务器异常，请稍后重试 |

## 四、前端兼容注意事项

- 订单ID格式：零售产品订单以 `P` 开头，认购订单以 `S` 开头，预约/认养订单以 `G` 开头
- 认购一亩田订单在前端通过 `orderCategory === 'subscription'` 或订单ID以 `S` 开头判断
- 产品订单搜索：支持订单ID、客户微信、收货人、用户姓名、电话、田块名、地块编号
- 时间格式统一为 `yyyy-MM-dd HH:mm`
- 退款图片证明最多3张，建议 base64 格式或 URL 格式
- 产品订单发货时，物流类型仅支持：`顺丰`、`邮政`
- 产品订单列表支持 `statusId` 参数筛选状态，请先调用 `/api/product/order/statuses` 获取状态ID映射
- 退款接口支持 `orderNo`（订单号）和 `orderId`（数字主键）两种传参方式，优先使用 `orderNo`
