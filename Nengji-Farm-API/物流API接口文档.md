# 物流 API 接口文档

> 版本：v1.1
> 日期：2026-06-02
> 变更：修复最简调用（仅传 orderId）实际不支持的问题

---

## 1. 获取物流详情

| 项目 | 内容 |
|------|------|
| 接口名称 | 获取物流详情 |
| 请求路径 | `GET /api/logistics/{orderId}` |
| 请求方式 | `GET` |
| 鉴权 | 需要登录 Token（Bearer） |
| 适用范围 | 微信小程序端 |

### 路径参数

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `orderId` | string | 是 | 订单号（数字 ID 或字符串编号，如 `GOODS202605290001`） |

### 请求头

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `Authorization` | string | 是 | `Bearer {token}` |

### 响应格式

```json
{
  "code": 200,
  "message": "success",
  "data": {
    "orderId": 123,
    "orderNumber": "GOODS202605290001",
    "companyName": "顺丰速运",
    "companyCode": "SF",
    "companyPhone": "95338",
    "waybillNo": "SF1234567890",
    "status": "shipping",
    "statusText": "运输中",
    "shippingAddress": {
      "name": "张三",
      "phone": "138****1234",
      "address": "广东省广州市天河区xxx路xxx号"
    },
    "items": [
      {
        "id": 1,
        "name": "五常大米",
        "image": "/api/file/image/xxx.jpg",
        "price": 49.50,
        "quantity": 2,
        "subtotal": 99.00
      }
    ],
    "shipTime": "2026-06-01 19:30:00",
    "estimatedArrival": "2026-06-03 18:00:00"
  }
}
```

### data 字段说明

| 字段 | 类型 | 说明 |
|------|------|------|
| `orderId` | Number | 订单 ID |
| `orderNumber` | String | 订单号 |
| `companyName` | String | 物流公司名称 |
| `companyCode` | String | 物流公司编码 |
| `companyPhone` | String | 物流公司电话 |
| `waybillNo` | String | 运单号 |
| `status` | String | 物流状态：`shipping` 运输中 / `completed` 已完成 |
| `statusText` | String | 物流状态中文 |
| `shippingAddress` | Object | 收货地址 |
| `items` | Array | 商品列表（详见下方） |
| `shipTime` | String | 发货时间 |
| `estimatedArrival` | String | 预计到达时间 |

### items 字段说明

| 字段 | 类型 | 说明 |
|------|------|------|
| `id` | Number | 商品 ID |
| `name` | String | **商品名称（下单时的快照）** |
| `image` | String | **商品图片（下单时的快照，相对路径）** |
| `price` | Number | 单价 |
| `quantity` | Number | 数量 |
| `subtotal` | Number | 小计 |

> 注：`name` 和 `image` 来自下单时保存的快照，商品后续被删除或修改不会影响历史订单的展示。

---

## 2. 获取物流轨迹

| 项目 | 内容 |
|------|------|
| 接口名称 | 获取物流轨迹 |
| 请求路径 | `GET /api/logistics/{orderId}/trace` |
| 请求方式 | `GET` |
| 鉴权 | 需要登录 Token（Bearer） |
| 适用范围 | 微信小程序端 |

### 路径参数

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `orderId` | string | 是 | 订单号 |

### 响应格式

```json
{
  "code": 200,
  "message": "success",
  "data": [
    {
      "time": "2026-06-03 10:00:00",
      "desc": "快件已签收，感谢使用",
      "location": "广州市",
      "status": "delivered"
    },
    {
      "time": "2026-06-03 08:00:00",
      "desc": "快件已到达【广州转运中心】",
      "location": "广州市",
      "status": "arrived"
    },
    {
      "time": "2026-06-02 18:00:00",
      "desc": "快件已从【广州转运中心】发出",
      "location": "广州市",
      "status": "departed"
    }
  ]
}
```

### data 字段说明

| 字段 | 类型 | 说明 |
|------|------|------|
| `time` | String | 操作时间 |
| `desc` | String | 物流描述 |
| `location` | String | 所在城市 |
| `status` | String | 状态：`picked` 已揽收 / `departed` 已发出 / `arrived` 已到达 / `delivering` 派送中 / `delivered` 已签收 |

---

## 3. 获取物流查询 token（微信物流详情页凭证）

| 项目 | 内容 |
|------|------|
| 接口名称 | 获取物流查询 token |
| 请求路径 | `POST /api/logistics/waybill-token` |
| 请求方式 | `POST` |
| 鉴权 | 需要登录 Token（Bearer） |
| Content-Type | `application/json` |
| 适用范围 | 微信小程序端 |

### 请求参数

#### 请求体

```json
{
  "orderId": "GOODS202605290001",
  "waybillId": "SF1234567890",
  "deliveryId": "SF",
  "receiverPhone": "13812341234",
  "openId": "oPq3T5xxxxxxxxxx",
  "transId": "wx_pay_transaction_id"
}
```

#### 请求体字段说明

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `orderId` | string | 是 | 订单号 |
| `waybillId` | string | 否 | 运单号，不传则自动从订单获取 |
| `deliveryId` | string | 否 | 快递公司编码，不传则自动从运单号前缀识别 |
| `receiverPhone` | string | 否 | 收件人手机号，不传则自动从收货地址获取 |
| `openId` | string | 否 | 用户 openId，不传则自动从用户表获取 |
| `transId` | string | 否 | 微信支付交易单号，不传则自动从订单获取 |

> 支持**最简调用**：前端只传 `orderId`，程序自动补全所有字段。

### 响应格式

```json
{
  "code": 200,
  "message": "success",
  "data": {
    "waybillToken": "wechat_waybill_token_string",
    "waybillId": "SF1234567890",
    "deliveryId": "SF"
  }
}
```

### 商品信息传递（自动）

调用微信 API 时会自动附带商品信息（`goods_info`），包含每个商品的：

| 字段 | 来源 | 说明 |
|------|------|------|
| `goods_name` | `commodity_order_detail.GoodsName` | **下单时保存的商品名** |
| `goods_img_url` | `commodity_order_detail.ImageUrl` + 域名拼接 | **下单时保存的商品图片（完整 https URL）** |

> 微信物流详情页会展示这些商品信息。商品后续被删除不影响已生成订单的展示。

---

## 4. 查询物流轨迹（管理端）

| 项目 | 内容 |
|------|------|
| 接口名称 | 按运单号查询物流轨迹 |
| 请求路径 | `POST /api/logistics/track` |
| 请求方式 | `POST` |
| 鉴权 | 需要登录 Token（Bearer） |
| Content-Type | `application/json` |
| 适用范围 | 后台管理端 |

### 请求参数

```json
{
  "platformType": "SF",
  "waybillNo": "SF1234567890"
}
```

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `platformType` | string | 是 | 物流平台：`SF` 顺丰 / `EMS` 邮政 |
| `waybillNo` | string | 是 | 运单号 |

### 响应格式

```json
{
  "code": 200,
  "message": "success",
  "data": {
    "platformType": "SF",
    "waybillNo": "SF1234567890",
    "orderNo": "SF1234567890",
    "currentProgress": {
      "operationTime": "2026-06-03 10:00:00",
      "remark": "快件已签收",
      "routeAddress": "广州市"
    },
    "list": [
      {
        "operationTime": "2026-06-03 10:00:00",
        "remark": "快件已签收",
        "routeAddress": "广州市"
      }
    ],
    "total": 1
  }
}
```

---

## 5. 获取微信运力列表

| 项目 | 内容 |
|------|------|
| 接口名称 | 获取快递公司列表 |
| 请求路径 | `GET /api/logistics/delivery-list` |
| 请求方式 | `GET` |
| 鉴权 | 不需要 |
| 适用范围 | 微信小程序端 |

### 响应格式

```json
{
  "code": 200,
  "message": "success",
  "data": {
    "list": [
      { "deliveryId": "SF", "deliveryName": "顺丰速运" },
      { "deliveryId": "EMS", "deliveryName": "邮政快递" }
    ],
    "total": 2
  }
}
```

---

## 状态码说明

| 状态码 | message | 说明 |
|--------|---------|------|
| 200 | success | 成功 |
| 400 | 运单号不能为空 | 请求参数缺失 |
| 400 | 快递公司编码不能为空 | deliveryId 缺失 |
| 400 | 用户 openId 不能为空 | 无法获取用户 openId |
| 400 | 收件人手机号不能为空 | 无法获取收件人手机号 |
| 401 | Unauthorized | Token 过期或未登录 |
| 403 | 无权查询该订单 | 非本人订单 |
| 404 | 订单不存在 | 未找到订单 |
| 502 | 获取物流 token 失败：xxx | 微信 API 调用失败 |

---

## 数据库依赖

| 表 | 操作 | 说明 |
|----|------|------|
| `commodity_orders` | 查询 | 订单信息、物流类型、运单号 |
| `commodity_order_detail` | 查询 | **商品名称、商品图片（下单快照）** |
| `commodity` | 查询 | 单价兜底 |
| `shipping_address` | 查询 | 收货地址 |
| `user` | 查询 | 用户 openId |
