# 订单 API 接口文档

> 适用项目：能记农场小程序后端  
> 基础地址：`http://192.168.203.56`  
> 统一返回格式：`code = 0` 表示成功，非 0 表示失败。  
> 鉴权说明：订单接口按当前登录用户过滤订单。前端应通过请求头传递 `Authorization: Bearer {token}`。

## 通用数据说明

### 订单类型

| type | 名称 | 说明 |
| --- | --- | --- |
| food | 点餐 | 农场餐厅点餐订单 |
| acre | 认购 | 土地或作物认购订单 |
| activity | 活动 | 农场活动报名订单 |
| cart | 商品 | 农场商品购买订单 |

### 订单状态

| status | 商品订单 | 活动/认购订单 | 点餐订单 |
| --- | --- | --- | --- |
| pending | 待支付 | 未完成 | 待支付 |
| paid | 待发货 | 已完成 | - |
| shipping | 待收货 | - | - |
| ordered | - | - | 已下单 |
| preparing | - | - | 制作中 |
| ready | - | - | 待取餐 |
| completed | 已完成 | 已完成 | 已完成 |
| cancelled | 已取消 | 已取消 | 已取消 |

---

## 1. 查询订单列表

<details open>
<summary><strong>GET /api/orders</strong> - 查询/搜索订单列表</summary>

### 中文注释
- 用于订单列表页、订单搜索页。
- 支持按订单类型、订单状态、关键词、分页、排序查询。
- `keyword` 支持订单号精确匹配，也支持商品名称/菜品名称模糊搜索。

### 是否鉴权
- 是

### 请求参数

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| type | string | 否 | 订单类型：`food`、`acre`、`activity`、`cart`、`all` |
| status | string | 否 | 订单状态：`pending`、`paid`、`shipping`、`completed`、`cancelled` 等 |
| keyword | string | 否 | 搜索关键词，支持订单号或商品/菜品名称 |
| page | number | 否 | 页码，默认 `1` |
| pageSize | number | 否 | 每页数量，默认 `10` |
| sortBy | string | 否 | 排序字段：`createTime`、`totalPrice`、`price`，默认 `createTime` |
| sortOrder | string | 否 | 排序方向：`desc`、`asc`，默认 `desc` |

### 请求示例

```http
GET /api/orders?type=cart&status=shipping&keyword=橘子&page=1&pageSize=10&sortBy=createTime&sortOrder=desc
Authorization: Bearer {token}
```

### 成功响应

```json
{
  "code": 0,
  "message": "success",
  "data": {
    "orders": [
      {
        "id": "80",
        "orderNumber": "202604151154180",
        "type": "cart",
        "typeText": "商品",
        "status": "shipping",
        "statusText": "待收货",
        "createTime": "2026-04-15 11:54:18",
        "paymentTime": "2026-04-15 11:54:21",
        "shippingTime": "2026-04-15 13:54:21",
        "completeTime": null,
        "totalPrice": 9.9,
        "shippingAddress": {
          "name": "张三",
          "phone": "13800138000",
          "address": "广东省广州市越秀区农科街道新河"
        },
        "items": [
          {
            "id": "1",
            "name": "农家橘子",
            "price": 9.9,
            "quantity": 4,
            "image": "http://192.168.203.56/api/file/image/farm_0000000000010.jpg"
          }
        ],
        "paymentMethod": "微信支付",
        "transactionId": "WX2026041511541880"
      }
    ],
    "total": 1,
    "page": 1,
    "pageSize": 10,
    "totalPages": 1
  }
}
```

### 失败说明

| code | 说明 |
| --- | --- |
| 500 | 获取订单列表失败 |

</details>

---

## 2. 获取订单详情

<details open>
<summary><strong>GET /api/orders/{orderId}</strong> - 获取订单详情</summary>

### 中文注释
- 用于订单详情页。
- 返回订单基础信息、收货地址、商品信息、支付信息。
- 商品订单在 `shipping` 或 `completed` 状态下会返回 `logistics` 物流时间线。
- 活动订单和认购订单不返回物流信息。

### 是否鉴权
- 是

### 路径参数

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| orderId | string | 是 | 订单 ID 或订单号 |

### 请求示例

```http
GET /api/orders/80
Authorization: Bearer {token}
```

### 成功响应

```json
{
  "code": 0,
  "message": "success",
  "data": {
    "id": "80",
    "orderNumber": "202604151154180",
    "type": "cart",
    "typeText": "商品",
    "status": "shipping",
    "statusText": "待收货",
    "createTime": "2026-04-15 11:54:18",
    "paymentTime": "2026-04-15 11:54:21",
    "totalPrice": 9.9,
    "items": [
      {
        "id": "1",
        "name": "农家橘子",
        "price": 9.9,
        "quantity": 4,
        "image": "http://192.168.203.56/api/file/image/farm_0000000000010.jpg"
      }
    ],
    "address": {
      "name": "张三",
      "phone": "13800138000",
      "province": "广东省",
      "city": "广州市",
      "district": "越秀区",
      "detail": "农科街道新河"
    },
    "payment": {
      "method": "微信支付",
      "status": "success",
      "amount": 9.9
    },
    "shippingAddress": {
      "name": "张三",
      "phone": "13800138000",
      "address": "广东省广州市越秀区农科街道新河"
    },
    "payTime": "2026-04-15 11:54:21",
    "shippingTime": "2026-04-15 13:54:21",
    "completeTime": null,
    "paymentMethod": "微信支付",
    "transactionId": "WX2026041511541880",
    "logistics": [
      {
        "time": "2026-04-16 07:54:21",
        "desc": "快递员正在派送中"
      },
      {
        "time": "2026-04-15 15:54:21",
        "desc": "商品已到达当地分拣中心"
      },
      {
        "time": "2026-04-15 13:54:21",
        "desc": "商品已发货"
      }
    ]
  }
}
```

### 失败说明

| code | 说明 |
| --- | --- |
| 404 | 订单不存在 |
| 500 | 获取订单详情失败 |

</details>

---

## 3. 删除订单

<details open>
<summary><strong>DELETE /api/orders/{orderId}</strong> - 删除订单</summary>

### 中文注释
- 用于订单列表或订单详情页的删除操作。
- 删除订单时会同步删除该订单下的商品明细、点餐主明细、点餐菜品明细。
- 只能删除当前用户自己的订单。

### 是否鉴权
- 是

### 路径参数

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| orderId | string | 是 | 订单 ID 或订单号 |

### 请求示例

```http
DELETE /api/orders/80
Authorization: Bearer {token}
```

### 成功响应

```json
{
  "code": 0,
  "message": "Order deleted",
  "data": {
    "orderId": "80",
    "deleted": true
  }
}
```

### 失败说明

| code | 说明 |
| --- | --- |
| 404 | 订单不存在 |
| 5003 | 删除订单失败 |

</details>

---

## 4. 更新订单状态

<details>
<summary><strong>PUT /api/orders/{orderId}/status</strong> - 更新订单状态</summary>

### 中文注释
- 用于取消订单、模拟发货、确认收货等操作。
- 常用目标状态：`cancelled`、`shipping`、`completed`。

### 是否鉴权
- 是

### 路径参数

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| orderId | string | 是 | 订单 ID 或订单号 |

### 请求参数

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| status | string | 是 | 目标状态 |

### 请求示例

```json
{
  "status": "shipping"
}
```

### 成功响应

```json
{
  "code": 0,
  "message": "success",
  "data": {
    "orderId": "80",
    "status": "shipping",
    "statusText": "待收货",
    "updateTime": "2026-04-16 09:30:00"
  }
}
```

### 失败说明

| code | 说明 |
| --- | --- |
| 400 | 目标状态不合法 |
| 404 | 订单不存在 |
| 5002 | 状态流转不允许或更新失败 |

</details>

---

## 5. 活动订单核销二维码

<details>
<summary><strong>GET /api/orders/{orderId}/qrcode</strong> - 获取活动订单核销二维码</summary>

### 中文注释
- 仅活动订单可用。
- 用于活动报名支付成功后展示核销二维码。

### 是否鉴权
- 是

### 路径参数

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| orderId | string | 是 | 活动订单 ID 或订单号 |

### 请求示例

```http
GET /api/orders/80/qrcode
Authorization: Bearer {token}
```

### 成功响应

```json
{
  "code": 0,
  "message": "success",
  "data": {
    "orderId": "80",
    "qrCodeUrl": "https://api.qrserver.com/v1/create-qr-code/?size=320x320&data=...",
    "verifyCode": "ACT-202604151154180-9"
  }
}
```

### 失败说明

| code | 说明 |
| --- | --- |
| 400 | 非活动订单不支持二维码 |
| 404 | 订单不存在 |
| 500 | 获取二维码失败 |

</details>

---

## 6. 活动详情图片说明

### 当前固定首图

活动详情页第一张图固定使用：

```text
http://192.168.203.56/api/file/image/farm 0000000000005.jpg
```

后端图片接口已兼容该空格文件名，会自动兜底读取实际文件：

```text
farm_0000000000005.jpg
```

### 图片请求示例

```http
GET /api/file/image/farm%200000000000005.jpg
```

> 注意：浏览器或小程序网络层可能会把空格编码为 `%20`，后端兼容文件名映射即可。

---

## 7. 前端调用示例

### 搜索订单

```javascript
api.order.getList({
  keyword: '橘子',
  type: 'cart',
  status: 'shipping',
  page: 1,
  pageSize: 10
}).then(data => {
  console.log(data.orders);
});
```

### 获取订单详情并读取物流

```javascript
api.order.getDetail(orderId).then(data => {
  if (data.logistics && data.logistics.length > 0) {
    console.log('物流信息', data.logistics);
  }
});
```

### 删除订单

```javascript
api.order.delete(orderId).then(() => {
  wx.showToast({
    title: '删除成功',
    icon: 'success'
  });
});
```
