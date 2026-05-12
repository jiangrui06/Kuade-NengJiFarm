# 订单管理 API 文档

> 基础地址：`http://127.0.0.1:5001`
>
> 统一响应格式：
> ```json
> { "code": 0, "message": "success", "data": { ... } }
> ```

---

## 一、菜品订单 API（`/api/dish/order`）

### 1.1 获取菜品订单列表

`GET /api/dish/order/list`

**Query 参数：**

| 参数 | 类型 | 必填 | 默认值 | 说明 |
|------|------|------|--------|------|
| pageNum | int | 是 | 1 | 页码，从 1 开始 |
| pageSize | int | 是 | 15 | 每页条数 |
| keyword | string | 否 | - | 搜索订单号、餐桌号 |

**搜索逻辑：** keyword 模糊匹配 `dish_orders.order_no` 或 `dining_table.table_no`，不传返回全部。

**成功响应 data 结构：**

```json
{
  "records": [
    {
      "orderId": "DISH20260512133755379173",
      "customerWechat": "微信用户",
      "contactPhone": "13800138000",
      "tableNo": "2号桌",
      "dishCount": 3,
      "actualAmount": 86.00,
      "paymentMethod": "微信支付",
      "paymentStatus": "已支付",
      "orderStatus": "备餐中",
      "kitchenStatus": "待出餐",
      "orderTime": "2026-04-01 12:34"
    }
  ],
  "total": 8,
  "pageNum": 1,
  "pageSize": 15,
  "pages": 1
}
```

**字段映射说明：**

| 字段 | 数据来源 |
|------|---------|
| orderId | dish_orders.order_no |
| customerWechat | user.wx_nickname |
| contactPhone | user.phone_number |
| tableNo | dining_table.table_no |
| dishCount | dish_orders.total_quantity |
| actualAmount | dish_orders.total_amount |
| paymentMethod | 固定值 `微信支付` |
| paymentStatus | 根据 order_status_id 映射 |
| orderStatus | 根据 order_status_id 映射 |
| kitchenStatus | 从 dish_order_details.status_id 聚合 |
| orderTime | dish_orders.create_time |

**状态映射规则：**

| order_status_id | orderStatus | paymentStatus |
|----------------|-------------|---------------|
| 1（已下单） | 备餐中 | 已支付 |
| 2（已出餐） | 备餐中 | 已支付 |
| 3（已完成） | 已完成 | 已支付 |
| 4（已取消） | 已取消 | 已退款 |

**kitchenStatus 聚合规则（从 dish_order_details.status_id）：**

| 条件 | kitchenStatus |
|------|--------------|
| 所有明细 status_id = 3（已取消） | 已取消 |
| 所有明细 status_id = 2（已出餐） | 已出餐 |
| 任一明细 status_id = 1（待出餐） | 待出餐 |

---

### 1.2 获取菜品订单详情

`GET /api/dish/order/detail`

**Query 参数：**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| orderNo | string | 是 | 订单号 |
| phone | string | 否 | 预留字段，后端忽略 |

**成功响应 data 结构：**

```json
{
  "orderInfo": {
    "orderNo": "DISH20260512133755379173",
    "orderType": "现场菜品点餐",
    "createTime": "2026-04-01 12:34",
    "orderStatus": "备餐中",
    "paymentStatus": "已支付",
    "kitchenStatus": "待出餐",
    "tableNo": "A01",
    "totalAmount": 86.00,
    "paymentMethod": "微信支付"
  },
  "orderItems": [
    {
      "image": "/images/dish/gongbao.jpg",
      "name": "宫保鸡丁",
      "description": "农场散养鸡肉搭配花生与时蔬，口味偏川香。",
      "remark": "少花生",
      "cookingNote": "",
      "quantity": 1,
      "price": 28.50,
      "subtotal": 28.50
    }
  ],
  "buyerInfo": {
    "nickname": "微信用户",
    "name": "张三",
    "customerWechat": "微信用户",
    "phone": "13800138000",
    "memberLevel": "普通会员",
    "orderSource": "微信扫码点餐",
    "dinerCount": 0,
    "seatArea": "",
    "remark": "先上热菜，微辣即可。"
  }
}
```

**图片来源优先级：** `dish_order_details.image_url` → `dish.image_url`

---

## 二、产品订单 API（`/api/product/order`）

### 2.1 获取产品订单列表

`GET /api/product/order/list`

**Query 参数：**

| 参数 | 类型 | 必填 | 默认值 | 说明 |
|------|------|------|--------|------|
| pageNum | int | 是 | 1 | 页码，从 1 开始 |
| pageSize | int | 是 | 15 | 每页条数 |
| keyword | string | 否 | - | 搜索订单号、微信昵称、收货人、电话 |

**搜索逻辑：** keyword 模糊匹配 `commodity_orders.order_no`、`user.wx_nickname`、`shipping_address.contact_name`、`user.phone_number`、`commodity_orders.receiver_phone`

**订单类型区分：** `order_no` 以 `S` 开头为认购订单，其余为零售订单。

**成功响应 data 结构（零售）：**

```json
{
  "records": [
    {
      "orderId": "P202604010001",
      "orderSource": "商城下单",
      "customerWechat": "微信用户",
      "contactPhone": "13723533044",
      "receiverName": "李四",
      "productSummary": "有机草莓礼盒、农家土鸡蛋",
      "itemCount": 3,
      "actualAmount": 158.00,
      "deliveryMethod": "快递配送",
      "logisticsType": "顺丰速运",
      "logisticsNo": "SF202604010188",
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
```

**成功响应 data 结构（认购）：**

```json
{
  "records": [
    {
      "orderId": "S202604240001",
      "orderCategory": "subscription",
      "orderSource": "认购一亩田",
      "customerWechat": "微信用户",
      "contactPhone": "13900139001",
      "receiverName": "林子涵",
      "userName": "林子涵",
      "fieldName": "",
      "plotNo": "",
      "period": "",
      "productSummary": "春季时令一亩田认购",
      "itemCount": 1,
      "actualAmount": 3980.00,
      "deliveryMethod": "一亩田认购",
      "logisticsType": "",
      "logisticsNo": "",
      "deliveryNote": "支付完成，待后台确认签约",
      "paymentMethod": "微信支付",
      "paymentStatus": "已支付",
      "orderStatus": "待签约",
      "orderTime": "2026-04-24 09:26",
      "signTime": "",
      "completeTime": "",
      "remark": ""
    }
  ]
}
```

**零售订单状态映射：**

| order_status_id | orderStatus | paymentStatus | deliveryNote |
|----------------|-------------|---------------|--------------|
| 1（待付款） | 待支付 | 待支付 | 等待客户支付 |
| 2（待发货） | 待发货 | 已支付 | 待仓库发货 |
| 3（运输中） | 待收货 | 已支付 | 已发货，等待客户签收 |
| 4（已完成） | 已完成 | 已支付 | 订单已完成 |
| 5（已取消） | 已取消 | 已退款 | 订单已取消 |
| 6（退款中） | 退款中 | 已支付 | 客户已申请退款，等待平台处理 |
| 7（已退款） | 已退款 | 已退款 | 退款已处理完成 |

**认购订单状态映射：**

| order_status_id | orderStatus | paymentStatus | deliveryNote |
|----------------|-------------|---------------|--------------|
| 1（待付款） | 待支付 | 待支付 | 等待客户支付 |
| 2（待发货） | 待签约 | 已支付 | 支付完成，待后台确认签约 |
| 4（已完成） | 已完成 | 已支付 | 认购已完成 |
| 7（已退款） | 已退款 | 已退款 | 退款已处理完成 |

**配送方式判断：** `address_id > 0` 且 `tracking_type_id` 不为空 → `快递配送`，否则 → `到店自提`

---

### 2.2 获取产品订单详情

`GET /api/product/order/detail`

**Query 参数：**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| orderNo | string | 是 | 订单号 |

**成功响应 data 结构（零售）：**

```json
{
  "orderInfo": {
    "orderNo": "P202604010001",
    "orderType": "农产品购买",
    "createTime": "2026-04-01 09:20",
    "orderStatus": "待发货",
    "paymentStatus": "已支付",
    "deliveryMethod": "快递配送",
    "logisticsType": "顺丰速运",
    "deliveryNote": "待仓库发货",
    "totalAmount": 158.00,
    "paymentMethod": "微信支付"
  },
  "orderItems": [
    {
      "productId": "1",
      "image": "/images/farm/Farm_15.jpg",
      "name": "有机草莓礼盒",
      "description": "冷链礼盒装，新鲜采摘",
      "spec": "冷链礼盒装",
      "netWeight": "2kg/箱",
      "quantity": 1,
      "price": 66.00,
      "subtotal": 66.00
    }
  ],
  "logisticsRecords": [
    {
      "logisticsType": "顺丰速运",
      "nodeName": "订单审核",
      "handler": "商城系统",
      "status": "进行中",
      "updateTime": "2026-04-01 09:20",
      "remark": "待仓库发货"
    }
  ],
  "buyerInfo": {
    "nickname": "微信用户",
    "name": "李四",
    "customerWechat": "微信用户",
    "phone": "13723533044",
    "memberLevel": "普通会员",
    "orderSource": "商城下单",
    "remark": ""
  },
  "fulfillmentInfo": {
    "receiverName": "李四",
    "phone": "13723533044",
    "address": "浙江省杭州市西湖区",
    "schedule": "顺丰速运",
    "trackingNo": "SF202604010188",
    "remark": ""
  }
}
```

**图片来源优先级：** `commodity_material`（material_type='image'，按 sort_order 取第一张）→ `commodity_order_detail.image_url` → `commodity.image_url`

**logisticsRecords：** 数据库无物流节点表，后端按订单状态生成一条基础记录。认购订单返回空数组。

**到店自提：** fulfillmentInfo.address 返回 `"到店自提"`，schedule 返回空字符串。

**地址拼接：** `shipping_address.province + city + municipal_district + addres`

---

### 2.3 更新产品订单状态

`PUT /api/product/order/updateStatus`

**Body 参数：**

```json
{
  "orderNo": "P202604010001",
  "action": "ship",
  "logisticsType": "顺丰速运",
  "logisticsNo": "SF202604010188",
  "refundReason": "包装破损",
  "refundImages": ["https://example.com/proof1.jpg"]
}
```

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| orderNo | string | 是 | 订单号 |
| action | string | 是 | 操作类型（见下方） |
| logisticsType | string | 条件 | 发货时必填，物流类型名称 |
| logisticsNo | string | 条件 | 发货时必填，物流单号 |
| refundReason | string | 条件 | 退款时必填，退款原因 |
| refundImages | string[] | 否 | 退款图片 URL 数组 |

**action 操作类型：**

| action | 适用 | 说明 | 状态变更 |
|--------|------|------|---------|
| cancel-pending-payment | 零售 | 取消待支付订单 | 1 → 5 |
| cancel-pending-shipment | 零售 | 取消待发货订单（自动退款） | 2 → 5 |
| cancel-pending-receipt | 零售 | 取消待收货订单（自动退款） | 3 → 5 |
| ship | 零售 | 发货，记录物流信息 | 2 → 3，写入 tracking_number/tracking_type_id |
| refund-request | 零售 | 用户申请退款 | 2 → 6，写入 refund_record |
| refund-process | 零售 | 处理退款（同意退款） | 6 → 7，更新 refund_record.status |
| subscription-sign | 认购 | 认购签约 | 无 DB 变更 |
| subscription-complete | 认购 | 认购完成 | 2 → 4 |

**物流类型列表：** 顺丰速运、邮政快递、圆通速递、中通快递、申通快递、韵达快递、京东快递、极兔速递、德邦快递、百世快递、安能物流、跨越速运、宅急送、丹鸟物流

**成功响应：**

```json
{
  "code": 0,
  "message": "操作成功",
  "data": null
}
```

---

## 三、错误码说明

| code | 含义 |
|------|------|
| 0 | 成功 |
| -1 | 通用错误 |
| 404 | 资源不存在 |
