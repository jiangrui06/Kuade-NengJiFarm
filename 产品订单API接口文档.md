# 产品订单 API 接口文档

**基础路径：** `/api/product/order`

**通用响应格式：**
```json
{
  "code": 200,
  "message": "操作成功",
  "data": { ... }
}
```

---

## 核心设计原则：数据全量来自数据库

本接口所有**状态数据（订单状态、商品信息、物流类型、退款记录等）均从数据库表动态读取**，后端代码无任何硬编码状态映射。

| 数据来源 | 数据库表 | 说明 |
|---|---|---|
| 订单状态 | `commodity_order_status` | 通过 `order_status_id` 关联，`status_name` 动态展示 |
| 退款记录 | `refund_record` | 按 `order_no` 关联查询最晚一条记录 |
| 商品明细 | `commodity_order_detail` + `commodities` | 商品名称、图片、价格等 |
| 商品主图 | `commodity_materials` | 取 `material_type=0` 的首张图片 |
| 用户信息 | `users` | 微信号、手机号、姓名等 |
| 物流类型 | `tracking_types` | 快递公司类型名称 |
| 收货地址 | `shipping_address` | 省市区详细地址 |

> **对前端的影响：** 数据库 `commodity_order_status` 表增删改状态名称后，API 自动生效，前端无需配合后端修改代码。但前端的状态筛选器文案需与数据库保持一致。

---

## 一、数据库订单状态表（commodity_order_status）

### 1.1 表结构

| 字段 | 类型 | 说明 |
|---|---|---|
| `order_status_id` | int (PK) | 状态ID |
| `status_name` | varchar(30) | 状态名称（唯一索引） |

### 1.2 当前数据库数据

| order_status_id | status_name | 业务含义 |
|---|---|---|
| 1 | 待付款 | 等待客户支付 |
| 2 | 待发货 | 已支付，等待仓库发货 |
| 3 | 运输中 | 已发货，等待客户签收 |
| 4 | 已完成 | 订单已完成 |
| 5 | 已取消 | 订单已取消 |
| 6 | 退款中 | 客户申请退款，等待处理 |
| 7 | 已退款 | 退款已完成 |
| 8 | 待核销 | 等待核销 |
| 9 | 已核销 | 已完成核销 |

> **API 自动适配：** 以上数据以数据库实际内容为准。如需新增/修改状态，直接在 `commodity_order_status` 表操作即可，后端**无需更改任何代码**。

### 1.3 订单类型判断

| 类型 | 判断条件 | orderSource 字段值 |
|---|---|---|
| 零售产品订单 | 订单号**不以** `S` 开头 | `商城下单` |
| 认购一亩田订单 | 订单号**以** `S` 开头 | `认购一亩田` |

### 1.4 paymentStatus 计算规则

`paymentStatus`（支付状态）由后端根据 `order_status_id` 动态计算：

| order_status_id | paymentStatus | 覆盖的订单状态 |
|---|---|---|
| 1 | 待支付 | 待付款 |
| 2, 3, 4, 6, 8, 9 | 已支付 | 待发货、运输中、已完成、退款中、待核销、已核销 |
| 5, 7 | 已退款 | 已取消、已退款 |

---

## 二、获取产品订单列表

### 2.1 接口说明

从数据库动态读取订单列表，包含订单状态（来自 `commodity_order_status`）、退款信息（来自 `refund_record`）、收件地址（来自 `shipping_address`）、商品摘要（来自 `commodity_order_detail`）。

```
GET /api/product/order/list?pageNum=1&pageSize=15&keyword=xxx
```

### 2.2 查询参数

| 参数 | 类型 | 必填 | 默认值 | 说明 |
|---|---|---|---|---|
| pageNum | int | 否 | 1 | 页码 |
| pageSize | int | 否 | 15 | 每页条数 |
| keyword | string | 否 | - | 模糊搜索（订单号/微信号/收货人/手机号） |

### 2.3 响应结构

```json
{
  "code": 200,
  "message": "获取成功",
  "data": {
    "records": [ ... ],
    "total": 100,
    "pageNum": 1,
    "pageSize": 15,
    "pages": 7
  }
}
```

### 2.4 records 字段说明

#### 公共字段（零售 + 认购）

| 字段 | 类型 | 数据来源 | 说明 |
|---|---|---|---|
| orderId | string | `commodity_orders.order_no` | 订单号 |
| orderCategory | string/null | 后端判断 | 零售为 `null`，认购为 `"subscription"` |
| orderSource | string | 后端根据订单号前缀判断 | `商城下单` / `认购一亩田` |
| customerWechat | string | `users.wx_name` | 客户微信号 |
| contactPhone | string | `commodity_orders.receiver_phone` 或 `users.phone_number` | 联系电话 |
| receiverName | string | `shipping_address.contact_name` | 收货人姓名 |
| productSummary | string | `commodity_order_detail.goods_name` | 商品摘要（取前2个商品名，用"、"拼接） |
| itemCount | int | `commodity_order_detail` 条数 | 商品种类数 |
| actualAmount | decimal | `commodity_orders.total_amount` | 实付金额 |
| deliveryMethod | string | 见下方说明 | 配送方式 |
| logisticsType | string | `tracking_types.tracking_type_name` | 物流类型（如：顺丰、邮政） |
| logisticsNo | string | `commodity_orders.tracking_number` | 物流单号 |
| deliveryNote | string | 后端根据 `order_status_id` 计算 | 履约说明 |
| paymentMethod | string | 固定值 | `微信支付` |
| paymentStatus | string | 后端根据 `order_status_id` 计算 | `待支付` / `已支付` / `已退款` |
| orderStatus | string | **`commodity_order_status.status_name`** | **从数据库动态读取** |
| orderTime | string | `commodity_orders.create_time` | 下单时间 `yyyy-MM-dd HH:mm` |
| refundReason | string/null | `refund_record.reason` | 退款原因 |
| refundApplyTime | string/null | `refund_record.create_time` | 退款申请时间 |
| refundProofImages | string[]/null | `refund_record.images` | 退款图片证明（JSON数组） |

> **`orderStatus` 数据流：** `commodity_orders.order_status_id` → `commodity_order_status.status_name` → API 响应 → 前端展示。数据库该表新增/修改状态名称后，API 自动生效。

> **`deliveryMethod` 计算逻辑：** 零售订单同时有地址ID和物流类型ID → `"快递配送"`，否则 → `"到店自提"`。认购订单 → `"一亩田认购"`。

#### 认购订单独有字段

| 字段 | 类型 | 数据来源 | 说明 |
|---|---|---|---|
| orderCategory | string | 后端判断 | 固定为 `"subscription"` |
| userName | string | `users.real_name` 或 `shipping_address.contact_name` | 认购用户姓名 |
| fieldName | string/null | - | 认购田块名称 |
| plotNo | string/null | - | 地块编号 |
| period | string/null | - | 认购周期 |
| signTime | string/null | - | 签约时间 |
| completeTime | string/null | - | 完成时间 |
| remark | string/null | - | 订单备注 |

### 2.5 响应示例（零售订单）

```json
{
  "code": 200,
  "message": "获取成功",
  "data": {
    "records": [
      {
        "orderId": "P202605010001",
        "orderCategory": null,
        "orderSource": "商城下单",
        "customerWechat": "wx_orchard_229",
        "contactPhone": "13723533044",
        "receiverName": "李四",
        "productSummary": "有机草莓礼盒、农家土鸡蛋",
        "itemCount": 3,
        "actualAmount": 158.00,
        "deliveryMethod": "快递配送",
        "logisticsType": "顺丰",
        "logisticsNo": "SF202605010001",
        "deliveryNote": "待仓库发货",
        "paymentMethod": "微信支付",
        "paymentStatus": "已支付",
        "orderStatus": "待发货",
        "orderTime": "2026-05-21 09:20",
        "refundReason": null,
        "refundApplyTime": null,
        "refundProofImages": null
      },
      {
        "orderId": "P202605210002",
        "orderCategory": null,
        "orderSource": "商城下单",
        "customerWechat": "wx_farmer_001",
        "contactPhone": "13800138001",
        "receiverName": "王五",
        "productSummary": "新鲜西红柿",
        "itemCount": 1,
        "actualAmount": 36.00,
        "deliveryMethod": "到店自提",
        "logisticsType": "",
        "logisticsNo": "",
        "deliveryNote": "客户已申请退款，等待平台处理",
        "paymentMethod": "微信支付",
        "paymentStatus": "已支付",
        "orderStatus": "退款中",
        "orderTime": "2026-05-21 10:00",
        "refundReason": "包装破损",
        "refundApplyTime": "2026-05-21 14:00",
        "refundProofImages": ["https://example.com/proof1.jpg"]
      }
    ],
    "total": 45,
    "pageNum": 1,
    "pageSize": 15,
    "pages": 3
  }
}
```

### 2.6 响应示例（认购订单）

```json
{
  "code": 200,
  "message": "获取成功",
  "data": {
    "records": [
      {
        "orderId": "S202605010001",
        "orderCategory": "subscription",
        "orderSource": "认购一亩田",
        "customerWechat": "wx_field_001",
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
        "logisticsType": "",
        "logisticsNo": "",
        "deliveryNote": "支付完成，待后台确认签约",
        "paymentMethod": "微信支付",
        "paymentStatus": "已支付",
        "orderStatus": "待签约",
        "orderTime": "2026-05-21 09:26",
        "signTime": "",
        "completeTime": "",
        "remark": "客户希望五一前完成签约",
        "refundReason": null,
        "refundApplyTime": null,
        "refundProofImages": null
      }
    ],
    "total": 6,
    "pageNum": 1,
    "pageSize": 15,
    "pages": 1
  }
}
```

---

## 三、获取产品订单详情

### 3.1 接口说明

从数据库查询单笔订单的完整信息，包含订单基本信息、商品明细、物流记录、买家信息、履约信息、退款信息（如有），所有数据均从数据库动态读取。

```
GET /api/product/order/detail?orderNo=P202605010001
```

### 3.2 查询参数

| 参数 | 类型 | 必填 | 说明 |
|---|---|---|---|
| orderNo | string | 是 | 订单号 |

### 3.3 响应结构

```json
{
  "code": 200,
  "message": "获取成功",
  "data": {
    "orderInfo": { ... },
    "orderItems": [ ... ],
    "logisticsRecords": [ ... ],
    "buyerInfo": { ... },
    "fulfillmentInfo": { ... },
    "refundInfo": { ... }
  }
}
```

### 3.4 orderInfo（订单基本信息）

| 字段 | 类型 | 数据来源 | 说明 |
|---|---|---|---|
| orderNo | string | `commodity_orders.order_no` | 订单号 |
| orderType | string | 后端判断 | `农产品购买` / `认购一亩田` |
| createTime | string | `commodity_orders.create_time` | 下单时间 |
| orderStatus | string | **`commodity_order_status.status_name`** | **从数据库动态读取** |
| paymentStatus | string | 后端根据 `order_status_id` 计算 | 支付状态 |
| deliveryMethod | string | 见列表接口说明 | 配送方式 |
| logisticsType | string | `tracking_types.tracking_type_name` | 物流类型 |
| deliveryNote | string | 后端根据 `order_status_id` 计算 | 履约说明 |
| totalAmount | decimal | `commodity_orders.total_amount` | 订单总金额 |
| paymentMethod | string | 固定值 | `微信支付` |

### 3.5 orderItems（商品明细）

| 字段 | 类型 | 数据来源 | 说明 |
|---|---|---|---|
| productId | string | `commodity_order_detail.commodity_id` | 商品ID |
| image | string | `commodity_materials.material_url` 或 `commodities.image_url` | 商品图片URL |
| name | string | `commodity_order_detail.goods_name` | 商品名称 |
| description | string | `commodities.spec_description` 或 `commodities.product_name` | 商品描述 |
| spec | string | `commodities.spec_description` | 规格说明 |
| netWeight | string | `commodities.weight_text` | 净含量（如 `2kg/箱`） |
| quantity | int | `commodity_order_detail.quantity` | 购买数量 |
| price | decimal | `commodity_order_detail.unit_price` | 单价 |
| subtotal | decimal | `commodity_order_detail.subtotal_amount` | 小计金额 |

> **`image` 字段优先级：** `commodity_materials`（物料表，`material_type=0` 的首图）→ `commodity_order_detail.image_url` → `commodities.image_url` → 空字符串

### 3.6 logisticsRecords（物流记录）

| 字段 | 类型 | 数据来源 | 说明 |
|---|---|---|---|
| logisticsType | string | `tracking_types.tracking_type_name` | 物流类型 |
| nodeName | string | 后端根据 `order_status_id` 计算 | 处理节点名称 |
| handler | string | 固定值 | `商城系统` |
| status | string | 后端判断 | `已完成` / `进行中` |
| updateTime | string | `commodity_orders.create_time` | 更新时间 |
| remark | string | deliveryNote 值 | 备注 |

> 认购订单无物流记录，返回空数组。

### 3.7 buyerInfo（买家信息）

| 字段 | 类型 | 数据来源 | 说明 |
|---|---|---|---|
| nickname | string | `users.wx_name` | 用户昵称 |
| name | string | `users.real_name` | 用户姓名 |
| customerWechat | string | `users.wx_name` | 客户微信号 |
| phone | string | `users.phone_number` | 联系电话 |
| memberLevel | string | 固定值 | `普通会员` |
| orderSource | string | 后端判断 | `微信商城下单` / `认购一亩田` |
| remark | string | 固定值 | 空字符串 |

### 3.8 fulfillmentInfo（履约信息）

| 字段 | 类型 | 数据来源 | 说明 |
|---|---|---|---|
| receiverName | string | `shipping_address.contact_name` | 收货人姓名 |
| phone | string | `shipping_address.contact_phone` 或 `commodity_orders.receiver_phone` | 联系电话 |
| address | string | `shipping_address` 省市区拼接 | 收货地址 / `到店自提` |
| schedule | string | `tracking_types.tracking_type_name` | 物流公司 |
| trackingNo | string | `commodity_orders.tracking_number` | 物流单号 |
| remark | string | 固定值 | 空字符串 |

### 3.9 refundInfo（退款信息）

**仅退款订单有值**，数据全部来自 `refund_record` 表，为 `null` 表示无退款记录。

| 字段 | 类型 | 数据来源 | 说明 |
|---|---|---|---|
| refundId | string | `refund_record.refund_id` | 退款记录ID |
| refundNo | string | `refund_record.refund_no` | 退款编号 |
| refundAmount | decimal | `refund_record.refund_amount` | 退款金额 |
| refundStatus | string | `refund_record.status` 映射 | `退款中`(pending) / `已退款`(completed) / `已驳回`(rejected) |
| refundReason | string | `refund_record.reason` | 退款原因 |
| refundApplyTime | string | `refund_record.create_time` | 申请时间 |
| refundProofImages | string[] | `refund_record.images` | 图片证明（JSON数组） |
| adminReply | string | `refund_record.admin_reply` | 管理员回复 |
| processNote | string | `refund_record.process_note` | 处理备注 |

### 3.10 响应示例（零售订单）

```json
{
  "code": 200,
  "message": "获取成功",
  "data": {
    "orderInfo": {
      "orderNo": "P202605010001",
      "orderType": "农产品购买",
      "createTime": "2026-05-21 09:20",
      "orderStatus": "待发货",
      "paymentStatus": "已支付",
      "deliveryMethod": "快递配送",
      "logisticsType": "顺丰",
      "deliveryNote": "待仓库发货",
      "totalAmount": 158.00,
      "paymentMethod": "微信支付"
    },
    "orderItems": [
      {
        "productId": "1",
        "image": "/images/product/strawberry.jpg",
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
        "logisticsType": "顺丰",
        "nodeName": "订单审核",
        "handler": "商城系统",
        "status": "已完成",
        "updateTime": "2026-05-21 09:22",
        "remark": "待仓库发货"
      }
    ],
    "buyerInfo": {
      "nickname": "果园慢生活",
      "name": "李四",
      "customerWechat": "wx_orchard_229",
      "phone": "13723533044",
      "memberLevel": "普通会员",
      "orderSource": "微信商城下单",
      "remark": ""
    },
    "fulfillmentInfo": {
      "receiverName": "李四",
      "phone": "13723533044",
      "address": "浙江省杭州市西湖区文三路88号",
      "schedule": "顺丰",
      "trackingNo": "SF202605010001",
      "remark": ""
    },
    "refundInfo": null
  }
}
```

---

## 四、更新产品订单状态

### 4.1 接口说明

修改订单状态流转，所有操作均写数据库（更新 `commodity_orders.order_status_id` 和 `refund_record`）。

```
PUT /api/product/order/updateStatus
Content-Type: application/json
```

### 4.2 请求体参数

| 字段 | 类型 | 必填 | 说明 |
|---|---|---|---|
| orderNo | string | 是 | 订单号 |
| action | string | 是 | 操作类型（见下方表格） |
| logisticsType | string | 条件必填 | 发货时必传 |
| logisticsNo | string | 条件必填 | 发货时必传 |
| refundReason | string | 条件必填 | 申请退款时必传 |
| refundImages | string[] | 否 | 申请退款时传，图片base64数组 |
| refundProofImages | string[] | 否 | 申请退款时传，与 `refundImages` 二选一 |
| adminReply | string | 否 | 驳回退款时填 |
| processNote | string | 否 | 驳回退款时填 |

> **`EffectiveRefundImages` 合并规则：** 优先使用 `refundImages`，若为 null 则使用 `refundProofImages`。

### 4.3 action 操作类型

| action 值 | 前置 order_status_id | 目标 order_status_id | 说明 | 数据库写入内容 |
|---|---|---|---|---|
| cancel-pending-payment | 1（待付款） | 5（已取消） | 取消待支付订单 | `commodity_orders.order_status_id=5`，恢复商品库存 |
| cancel-pending-shipment | 2（待发货） | 5（已取消） | 取消待发货订单 | `commodity_orders.order_status_id=5`，恢复商品库存 |
| cancel-pending-receipt | 3（运输中） | 5（已取消） | 取消待收货订单 | `commodity_orders.order_status_id=5`，恢复商品库存 |
| ship | 2（待发货） | 3（运输中） | 发货 | `commodity_orders.order_status_id=3`，更新物流信息 |
| refund-request | 2（待发货） | 6（退款中） | 申请退款 | `commodity_orders.order_status_id=6`，新增 `refund_record`（status=pending） |
| refund-process | 6（退款中） | 7（已退款） | 处理退款 | `commodity_orders.order_status_id=7`，更新 `refund_record.status=completed` |
| refund-reject | 6（退款中） | 2（待发货） | 驳回退款 | `commodity_orders.order_status_id=2`，更新 `refund_record.status=rejected` |
| subscription-sign | 任意 | 不变 | 认购签约 | 仅后端逻辑处理 |
| subscription-complete | 2（待发货） | 4（已完成） | 认购完成 | `commodity_orders.order_status_id=4` |

> **状态流转校验：** 后端先从数据库读取订单当前 `order_status_id`，校验是否匹配前置状态，不匹配则返回错误提示。目标 `order_status_id` 写库后，下次查询时 API 自动从 `commodity_order_status` 表读取最新状态名称。

### 4.4 请求示例

#### 发货
```json
{
  "orderNo": "P202605010001",
  "action": "ship",
  "logisticsType": "顺丰",
  "logisticsNo": "SF202605010001"
}
```

#### 申请退款
```json
{
  "orderNo": "P202605210002",
  "action": "refund-request",
  "refundReason": "包装破损",
  "refundProofImages": ["base64图片数据"]
}
```

#### 处理退款
```json
{
  "orderNo": "P202605210002",
  "action": "refund-process"
}
```

#### 驳回退款
```json
{
  "orderNo": "P202605210002",
  "action": "refund-reject",
  "adminReply": "不符合退款条件",
  "processNote": "已核实"
}
```

#### 取消待付款订单
```json
{
  "orderNo": "P202605010001",
  "action": "cancel-pending-payment"
}
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

## 五、错误码

| HTTP 状态码 | 说明 |
|---|---|
| 200 | 请求成功（业务错误在 `code`/`message` 中返回） |
| 400 | 参数错误 |
| 404 | 订单不存在（code=404） |
| 500 | 服务器内部错误 |

### 业务错误 message 示例

| 错误消息 | 触发条件 |
|---|---|
| `订单号不能为空` | 请求体未传 orderNo |
| `操作类型不能为空` | 请求体未传 action |
| `订单不存在` | 数据库查不到该 orderNo |
| `仅待发货订单可发货` | 订单状态不是 order_status_id=2 |
| `仅待发货订单可申请退款` | 订单状态不是 order_status_id=2 |
| `仅退款中订单可处理退款` | 订单状态不是 order_status_id=6 |
| `仅待付款订单可取消` | 订单状态不是 order_status_id=1 |
| `仅待发货订单可取消` | 订单状态不是 order_status_id=2 |
| `仅待收货订单可取消` | 订单状态不是 order_status_id=3 |
| `发货必须填写物流类型` | ship 操作未传 logisticsType |
| `发货必须填写物流单号` | ship 操作未传 logisticsNo |
| `物流类型「xxx」不存在` | 数据库 `tracking_types` 表查不到该物流名称 |
| `不支持的操作: xxx` | action 值不在允许列表中 |
| `认购订单状态不正确` | subscription-complete 时 order_status_id 不是 2 |

---

## 六、数据流全景图

```
┌─────────────────────────────────────────────────────────────────┐
│                        前端页面                                   │
│  orderStatus / paymentStatus / deliveryNote / 退款信息 / 商品等    │
└──────────────────────────┬──────────────────────────────────────┘
                           │ HTTP JSON
                           ▼
┌─────────────────────────────────────────────────────────────────┐
│                    API 后端 (ProductOrderService)                  │
│                                                                   │
│  GetOrderListAsync:                                               │
│    └─ commodity_orders ──→ users (微信号/手机号)                  │
│    └─ commodity_order_detail ──→ goods_name → productSummary      │
│    └─ shipping_address (收货地址)                                 │
│    └─ refund_record (退款信息)                                    │
│    └─ commodity_order_status ──→ orderStatus (动态读取)           │
│    └─ tracking_types → logisticsType                              │
│                                                                   │
│  GetOrderDetailAsync:                                             │
│    └─ commodity_orders + users + tracking_types                   │
│    └─ commodity_order_detail + commodities + commodity_materials  │
│    └─ refund_record (退款详情)                                    │
│    └─ shipping_address (完整地址)                                 │
│    └─ commodity_order_status ──→ orderStatus (动态读取)           │
│                                                                   │
│  UpdateOrderStatusAsync:                                          │
│    └─ 更新 commodity_orders.order_status_id                       │
│    └─ 写入 refund_record (退款时)                                 │
│    └─ 恢复 commodity.in_stock (取消时)                            │
└──────────────────────────┬──────────────────────────────────────┘
                           │ Entity Framework Core
                           ▼
┌─────────────────────────────────────────────────────────────────┐
│                        数据库 (MySQL)                             │
│                                                                   │
│  commodity_order_status ─── 订单状态表 (增删改后API自动适配)       │
│  commodity_orders ──────── 订单主表                                │
│  commodity_order_detail ── 订单商品明细表                          │
│  commodities ───────────── 商品表                                  │
│  commodity_materials ───── 商品物料/图片表                         │
│  refund_record ─────────── 退款记录表                              │
│  users ─────────────────── 用户表                                  │
│  tracking_types ────────── 物流类型表                              │
│  shipping_address ──────── 收货地址表                              │
└─────────────────────────────────────────────────────────────────┘
```

---

## 七、接口汇总

| 接口 | 方法 | 路径 | 说明 |
|---|---|---|---|
| 获取订单列表 | GET | `/api/product/order/list` | 分页查询，支持模糊搜索 |
| 获取订单详情 | GET | `/api/product/order/detail` | 单笔订单完整信息 |
| 更新订单状态 | PUT | `/api/product/order/updateStatus` | 取消/发货/退款/签约等 |

---

## 八、对前端的特别说明

### 8.1 状态数据动态性

`orderStatus` 字段值直接来自数据库 `commodity_order_status.status_name`：
- 后端不硬编码任何状态名称
- 数据库修改状态名称后，API 立即返回新名称
- **前端不应硬编码状态名称进行匹配判断**，应使用 API 返回的 `orderStatus` 值

### 8.2 推荐的前端状态匹配方式

由于状态名称可能随数据库变化，前端建议：
1. **使用 `orderId` 前缀判断订单类型：** 以 `S` 开头 = 认购订单，否则 = 零售订单
2. **使用 `orderStatus` 文本直接匹配：** 从 API 返回的值就是展示值
3. **状态筛选器文案应与数据库保持同步：** 前端筛选标签的值需与 `commodity_order_status` 表一致

### 8.3 退款信息的获取

- **列表页退款信息：** `GET /api/product/order/list` 返回的 `refundReason`、`refundApplyTime`、`refundProofImages` 字段
- **详情页退款信息：** `GET /api/product/order/detail` 返回的 `refundInfo` 对象
- 两个接口的退款数据均来自 `refund_record` 表，取该订单号下最新一条记录
