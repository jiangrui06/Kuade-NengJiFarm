# 订单管理 API 接口文档

本文档基于以下前端页面整理，用于后端接口开发和前后端联调：

- [order-coupon.html](/e:/Kuade-NengJiFarm/web_management/order-coupon.html)
- [order-dish.html](/e:/Kuade-NengJiFarm/web_management/order-dish.html)
- [order-dish-detail.html](/e:/Kuade-NengJiFarm/web_management/order-dish-detail.html)
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
| 菜品订单 | `D` + 年月日时分秒 | `D202604010001` |
| 产品订单（零售） | `P` + 年月日时分秒 | `P202604010001` |
| 认购订单 | `S` + 年月日时分秒 | `S202604240001` |
| 券类订单 | `C` + 年月日时分秒 | `C202604240001` |

## 二、菜品订单

### 2.1 字段说明

**列表页（order-dish.html）依赖字段**：

| 字段 | 类型 | 是否必传 | 示例 | 说明 |
|---|---|---|---|---|
| `orderId` | String | 是 | `D202604010001` | 订单唯一ID |
| `customerWechat` | String | 是 | `wx_farmer_001` | 客户微信号 |
| `contactPhone` | String | 是 | `13778493212` | 联系电话 |
| `tableNo` | String | 是 | `A01` | 餐桌号 |
| `dishCount` | Number | 是 | `3` | 菜品数量（份数） |
| `actualAmount` | Number | 是 | `86.00` | 实付金额 |
| `paymentMethod` | String | 是 | `微信支付` | 支付方式 |
| `paymentStatus` | String | 是 | `已支付` | 支付状态：`已支付` / `待支付` / `已退款` |
| `orderStatus` | String | 是 | `备餐中` | 订单状态：`待支付` / `备餐中` / `已完成` / `已取消` |
| `kitchenStatus` | String | 是 | `待出餐` | 后厨状态：`待接单` / `待出餐` / `已出餐` / `已取消` |
| `orderTime` | String | 是 | `2026-04-01 12:34` | 下单时间 |

**详情页（order-dish-detail.html）附加字段**：

| 字段 | 类型 | 说明 |
|---|---|---|
| `orderType` | String | 固定为 `现场菜品点餐` |
| `totalAmount` | Number | 订单总金额 |

**orderItems（菜品明细）结构**：

| 字段 | 类型 | 说明 |
|---|---|---|
| `image` | String | 菜品图片URL |
| `name` | String | 菜品名称 |
| `description` | String | 菜品描述 |
| `remark` | String | 菜品备注（如：少花生） |
| `cookingNote` | String | 烹饪要求（如：优先出餐） |
| `quantity` | Number | 购买数量 |
| `price` | Number | 单价 |
| `subtotal` | Number | 小计金额 |

**buyerInfo（点餐用户信息）结构**：

| 字段 | 类型 | 说明 |
|---|---|---|
| `nickname` | String | 用户昵称 |
| `name` | String | 用户姓名 |
| `customerWechat` | String | 客户微信号 |
| `phone` | String | 联系电话 |
| `memberLevel` | String | 会员等级 |
| `orderSource` | String | 下单来源（如：微信扫码点餐） |
| `dinerCount` | Number | 用餐人数 |
| `seatArea` | String | 就餐区域 |
| `remark` | String | 用户备注 |

### 2.2 获取菜品订单列表

| 项目 | 内容 |
|---|---|
| 接口名称 | 获取菜品订单列表 |
| 请求路径 | `/api/dish/order/list` |
| 请求方式 | `GET` |
| 接口说明 | 用于 `order-dish.html` 列表页 |

#### Query 参数

| 字段 | 是否必传 | 类型 | 示例 | 说明 |
|---|---|---|---|---|
| `pageNum` | 是 | Number | `1` | 页码 |
| `pageSize` | 是 | Number | `15` | 每页条数 |
| `keyword` | 否 | String | `A01` | 按订单ID、微信、餐桌号搜索 |

#### 成功响应示例

```json
{
  "code": 200,
  "message": "获取成功",
  "data": {
    "records": [
      {
        "orderId": "D202604010001",
        "customerWechat": "wx_farmer_001",
        "contactPhone": "13778493212",
        "tableNo": "A01",
        "dishCount": 3,
        "actualAmount": 86.00,
        "paymentMethod": "微信支付",
        "paymentStatus": "已支付",
        "orderStatus": "备餐中",
        "kitchenStatus": "待出餐",
        "orderTime": "2026-04-01 12:34"
      },
      {
        "orderId": "D202604010002",
        "customerWechat": "wx_orchard_229",
        "contactPhone": "13723533044",
        "tableNo": "B03",
        "dishCount": 5,
        "actualAmount": 128.00,
        "paymentMethod": "微信支付",
        "paymentStatus": "待支付",
        "orderStatus": "待支付",
        "kitchenStatus": "待接单",
        "orderTime": "2026-04-01 12:40"
      }
    ],
    "total": 8,
    "pageNum": 1,
    "pageSize": 15,
    "pages": 1
  }
}
```

### 2.3 获取菜品订单详情

| 项目 | 内容 |
|---|---|
| 接口名称 | 获取菜品订单详情 |
| 请求路径 | `/api/dish/order/detail` |
| 请求方式 | `GET` |
| 接口说明 | 用于 `order-dish-detail.html` 详情页 |

#### Query 参数

| 字段 | 是否必传 | 类型 | 示例 | 说明 |
|---|---|---|---|---|
| `orderNo` | 是 | String | `D202604010001` | 订单ID |
| `phone` | 否 | String | `13778493212` | 客户电话（辅助查询） |

#### 成功响应示例

```json
{
  "code": 200,
  "message": "获取成功",
  "data": {
    "orderInfo": {
      "orderNo": "D202604010001",
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
        "image": "https://example.com/dish/gongbao.jpg",
        "name": "宫保鸡丁",
        "description": "农场散养鸡肉搭配花生与时蔬，口味偏川香。",
        "remark": "少花生",
        "cookingNote": "优先出餐",
        "quantity": 1,
        "price": 28.50,
        "subtotal": 28.50
      }
    ],
    "buyerInfo": {
      "nickname": "快乐农场主",
      "name": "张三",
      "customerWechat": "wx_farmer_001",
      "phone": "13778493212",
      "memberLevel": "VIP会员",
      "orderSource": "微信扫码点餐",
      "dinerCount": 3,
      "seatArea": "大厅A区",
      "remark": "先上热菜，微辣即可。"
    }
  }
}
```

## 三、产品订单

产品订单包含两类：

- **零售产品订单**：普通商品购买，配送方式为快递配送或到店自提
- **认购一亩田订单**：农田认购，有签约、认购履约等特殊流程

### 3.1 字段说明

**零售产品订单（order-product.html）列表字段**：

| 字段 | 类型 | 是否必传 | 示例 | 说明 |
|---|---|---|---|---|
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

### 3.2 获取产品订单列表

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

#### 成功响应示例（零售订单）

```json
{
  "code": 200,
  "message": "获取成功",
  "data": {
    "records": [
      {
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

### 3.3 获取产品订单详情

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

### 3.4 更新产品订单状态

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

## 四、券类订单

### 4.1 字段说明

**列表页（order-coupon.html）依赖字段**：

| 字段 | 类型 | 是否必传 | 示例 | 说明 |
|---|---|---|---|---|
| `orderId` | String | 是 | `C202604240001` | 订单唯一ID |
| `couponName` | String | 是 | `农场采摘体验券` | 券名称 |
| `userName` | String | 是 | `李春晓` | 用户姓名 |
| `phone` | String | 是 | `13800138001` | 联系电话 |
| `amount` | String | 是 | `98.00` | 实付金额 |
| `quantity` | Number | 是 | `2` | 购买数量（张数） |
| `paymentStatus` | String | 是 | `已支付` | 支付状态：`已支付` / `待支付` / `已退款` |
| `orderStatus` | String | 是 | `待核销` | 订单状态：`待付款` / `待核销` / `已核销` / `已退款` |
| `orderTime` | String | 是 | `2026-04-24 09:10` | 下单时间 |
| `writeOffTime` | String | 否 | `2026-04-24 14:18` | 核销时间 |
| `storeName` | String | 是 | `能记农场服务中心` | 适用门店 |
| `remark` | String | 否 | `周末到店使用` | 订单备注 |
| `verificationRecords` | Array | 否 | `[{writeOffTime, storeName, operator, remark}]` | 核销记录数组 |

**券类订单状态组合逻辑**：

| 支付状态 | 订单状态 | 综合展示 |
|---|---|---|
| `已退款` | 任意 | `已退款` |
| `待支付` | 任意 | `待付款` |
| 任意 | `已核销` | `已核销` |
| `已支付` | `待核销` | `待核销` |

**verificationRecords（核销记录）结构**：

| 字段 | 类型 | 说明 |
|---|---|---|
| `writeOffTime` | String | 核销时间 |
| `storeName` | String | 核销门店 |
| `operator` | String | 核销人员 |
| `remark` | String | 核销备注 |

### 4.2 获取券类订单列表

| 项目 | 内容 |
|---|---|
| 接口名称 | 获取券类订单列表 |
| 请求路径 | `/api/coupon/order/list` |
| 请求方式 | `GET` |
| 接口说明 | 用于 `order-coupon.html` 列表页 |

#### Query 参数

| 字段 | 是否必传 | 类型 | 示例 | 说明 |
|---|---|---|---|---|
| `pageNum` | 是 | Number | `1` | 页码 |
| `pageSize` | 是 | Number | `10` | 每页条数 |
| `keyword` | 否 | String | `C202604240001` | 按订单ID搜索 |

#### 成功响应示例

```json
{
  "code": 200,
  "message": "获取成功",
  "data": {
    "records": [
      {
        "orderId": "C202604240001",
        "couponName": "农场采摘体验券",
        "userName": "李春晓",
        "phone": "13800138001",
        "amount": "98.00",
        "quantity": 2,
        "paymentStatus": "已支付",
        "orderStatus": "待核销",
        "orderTime": "2026-04-24 09:10",
        "writeOffTime": "",
        "storeName": "能记农场服务中心",
        "remark": "周末到店使用",
        "verificationRecords": []
      },
      {
        "orderId": "C202604240002",
        "couponName": "蔬果礼盒抵扣券",
        "userName": "陈思雨",
        "phone": "13800138002",
        "amount": "50.00",
        "quantity": 1,
        "paymentStatus": "已支付",
        "orderStatus": "已核销",
        "orderTime": "2026-04-24 10:25",
        "writeOffTime": "2026-04-24 14:18",
        "storeName": "能记农场服务中心",
        "remark": "已完成到店核销",
        "verificationRecords": [
          {
            "writeOffTime": "2026-04-24 14:18",
            "storeName": "能记农场服务中心",
            "operator": "店员小王",
            "remark": "券类核销成功"
          }
        ]
      }
    ],
    "total": 6,
    "pageNum": 1,
    "pageSize": 10,
    "pages": 1
  }
}
```

### 4.3 券类订单核销

| 项目 | 内容 |
|---|---|
| 接口名称 | 券类订单核销 |
| 请求路径 | `/api/coupon/order/writeOff` |
| 请求方式 | `POST` |
| 接口说明 | 用于将已支付待核销的券类订单标记为已核销 |

#### Body 参数

| 字段 | 是否必传 | 类型 | 示例 | 说明 |
|---|---|---|---|---|
| `orderId` | 是 | String | `C202604240001` | 订单ID |

#### 请求示例

```json
{
  "orderId": "C202604240001"
}
```

## 五、通用失败场景

| 场景 | 状态码 | 提示文案 |
|---|---|---|
| token 无效或过期 | `401` | 登录已过期，请重新登录 |
| 权限不足 | `403` | 权限不足，仅管理员可操作 |
| 参数错误 | `400` | 请求参数不完整或格式错误 |
| 订单不存在 | `404` | 订单不存在或已被删除 |
| 订单状态不允许该操作 | `422` | 当前订单状态不允许此操作 |
| 服务异常 | `500` | 服务器异常，请稍后重试 |

## 六、前端兼容注意事项

- 订单ID格式：菜品订单以 `D` 开头，零售产品订单以 `P` 开头，认购订单以 `S` 开头，券类订单以 `C` 开头
- 认购一亩田订单在前端通过 `orderCategory === 'subscription'` 或订单ID以 `S` 开头判断
- 菜品订单搜索：支持订单ID、微信、餐桌号
- 产品订单搜索：支持订单ID、客户微信、收货人、用户姓名、电话、田块名、地块编号
- 券类订单搜索：仅支持订单ID
- 时间格式统一为 `yyyy-MM-dd HH:mm`
- 退款图片证明最多3张，建议 base64 格式或 URL 格式
- 产品订单发货时，物流类型仅支持：`顺丰`、`邮政`
- 券类二维码由前端根据订单ID生成，后端无需提供二维码接口
