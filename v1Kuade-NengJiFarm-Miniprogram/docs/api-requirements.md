# 能记农场小程序 — 完整 API 需求文档

> **版本**：v1.0  
> **更新日期**：2026-04-28  
> **基础地址（BASE_URL）**：`http://192.168.203.56`  
> **文档作者**：根据小程序源码自动整理  
> **关联文档**：`docs/staff-api-requirements.md`（员工模块详细文档）

---

## 目录

1. [通用约定](#1-通用约定)
2. [认证模块（Auth）](#2-认证模块auth)
3. [首页模块（Home）](#3-首页模块home)
4. [文件/图片模块（File）](#4-文件图片模块file)
5. [活动模块（Activity）](#5-活动模块activity)
6. [商品模块（Goods）](#6-商品模块goods)
7. [农场优选模块（FarmGoods）](#7-农场优选模块farmgoods)
8. [认购一亩田模块（Acre）](#8-认购一亩田模块acre)
9. [购物车模块（Cart）](#9-购物车模块cart)
10. [订单模块（Order / OrderDetails）](#10-订单模块order--orderdetails)
11. [用户模块（User）](#11-用户模块user)
12. [地址模块（Address）](#12-地址模块address)
13. [支付模块（Pay）](#13-支付模块pay)
14. [物流模块（Logistics）](#14-物流模块logistics)
15. [员工核销模块（Staff）](#15-员工核销模块staff)
16. [点餐模块（Order Food）](#16-点餐模块order-food)
17. [错误码规范](#17-错误码规范)
18. [Storage 字段说明](#18-storage-字段说明)

---

## 1. 通用约定

### 1.1 响应格式

所有接口统一返回以下 JSON 结构：

```json
{
  "code": 0,
  "message": "success",
  "data": { ... }
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| `code` | number | `0` = 成功，其他值 = 失败 |
| `message` | string | 提示信息，失败时作为 Toast 显示 |
| `data` | any | 业务数据，`code=0` 时 resolve |

### 1.2 鉴权方式

- 需要登录的接口，在请求头中携带：
  ```
  Authorization: Bearer {token}
  ```
- 小程序端判断需要鉴权的路径前缀：
  `/api/user`、`/api/orders`、`/api/cart`、`/api/OrderDetails`、`/api/pay`、`/api/acres`、`/api/address`、`/api/logistics`、`/api/staff`
- 未携带 token 时，小程序自动跳转登录页：`/pages/login/login`

### 1.3 图片地址处理

- 后端返回的图片路径若为相对路径，前端拼接 BASE_URL：`http://192.168.203.56` + `/path/to/image`
- 兼容旧地址：`127.0.0.1:5000` → `192.168.203.56`

---

## 2. 认证模块（Auth）

### 2.1 微信手机号一键登录

**接口功能**：通过微信 `wx.login()` 获取的 `code` 和 `getPhoneNumber` 获取的 `phoneCode`，完成登录/注册，返回 token 和用户信息。

```
POST /api/Auth/wx-phone-login
```

**请求体（Body - JSON）**：

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `code` | string | ✅ | 微信 `wx.login()` 返回的临时登录凭证 |
| `phoneCode` | string | ✅ | 微信 `getPhoneNumber` 回调返回的 `code`，用于获取手机号 |

**请求示例**：
```json
{
  "code": "081xxx",
  "phoneCode": "0E3xxx"
}
```

**响应 `data` 字段**：

| 字段 | 类型 | 说明 |
|------|------|------|
| `token` | string | JWT 令牌，存入 Storage `token` |
| `user_id` | number/string | 用户ID，存入 Storage `user_id` |
| `user_guid` | string | 用户 GUID，存入 Storage `user_guid` |
| `openid` | string | 微信 OpenID，存入 Storage `openid` |
| `phone_number` | string | 用户手机号，存入 Storage `phone_number` |
| `register_time` | string | 注册时间 |
| `role` | string | 用户角色：`user`（普通用户）或 `staff`（员工）|

**响应示例**：
```json
{
  "code": 0,
  "message": "success",
  "data": {
    "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "user_id": 1001,
    "user_guid": "a1b2c3d4-...",
    "openid": "oLfXx...",
    "phone_number": "13800138000",
    "register_time": "2024-01-01T00:00:00Z",
    "role": "user"
  }
}
```

**登录后前端行为**：
- `role === 'staff'` → 跳转 `/staff-pages/staff-home/staff-home`
- `role === 'user'` → 跳转 `/pages/index/index`（TabBar 首页）
- 登录完成后自动预取用户信息写入 `user_profile_cache`

**错误码**：
- `409` — 该手机号已绑定其他账号

---

## 3. 首页模块（Home）

### 3.1 获取首页数据

```
GET /api/home
```

**查询参数**：无（可扩展）

**响应 `data` 字段**（建议包含）：

| 字段 | 类型 | 说明 |
|------|------|------|
| `banners` | array | 轮播图列表 |
| `banners[].image` | string | 轮播图图片路径 |
| `banners[].link` | string | 点击跳转路径（可为空） |
| `categories` | array | 商品分类列表 |
| `categories[].id` | number | 分类ID |
| `categories[].name` | string | 分类名称 |
| `categories[].icon` | string | 分类图标路径 |
| `featuredGoods` | array | 推荐商品列表（同商品列表字段） |
| `activities` | array | 首页活动入口列表 |

---

## 4. 文件/图片模块（File）

### 4.1 获取图片列表

```
GET /api/file/images
```

**响应 `data`**：string[] — 图片路径列表

---

### 4.2 获取单张图片

```
GET /api/file/image/{name}
```

**路径参数**：

| 参数 | 类型 | 说明 |
|------|------|------|
| `name` | string | 图片文件名，例：`farm_0000000000012.jpg` |

**响应 `data`**：string — 图片完整 URL 或路径

---

### 4.3 上传文件

```
POST /api/upload
Content-Type: multipart/form-data
```

**上传字段**：

| 字段 | 说明 |
|------|------|
| `file` | 文件内容（`wx.uploadFile` 的 `name` 字段为 `"file"`） |

**响应 `data`**：

| 字段 | 类型 | 说明 |
|------|------|------|
| `url` | string | 上传后的文件访问路径 |

---

## 5. 活动模块（Activity）

### 5.1 获取活动列表

```
GET /api/activity/list
```

**查询参数**：无

**响应 `data`**：array

| 字段 | 类型 | 说明 |
|------|------|------|
| `id` | number | 活动ID |
| `title` | string | 活动标题 |
| `cover` | string | 封面图路径 |
| `endTime` | string | 结束时间（ISO 8601） |
| `price` | number | 活动价格（元） |
| `status` | string | 活动状态：`upcoming`/`ongoing`/`ended` |
| `description` | string | 活动简介 |

---

### 5.2 获取活动详情

```
GET /api/activity/detail?id={id}
```

**查询参数**：

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `id` | number | ✅ | 活动ID |

**响应 `data`**：在列表基础上增加：

| 字段 | 类型 | 说明 |
|------|------|------|
| `content` | string | 活动详情富文本内容 |
| `maxParticipants` | number | 最大参与人数 |
| `currentParticipants` | number | 当前已报名人数 |
| `hasRegistered` | boolean | 当前用户是否已报名 |

---

### 5.3 报名参加活动

```
POST /api/activity/{id}/register
Authorization: Bearer {token}（可选，按业务决定）
```

**路径参数**：

| 参数 | 类型 | 说明 |
|------|------|------|
| `id` | number | 活动ID |

**请求体（Body - JSON）**：

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `name` | string | 可选 | 报名人姓名 |
| `phone` | string | 可选 | 联系电话 |
| `count` | number | 可选 | 报名人数 |

**响应 `data`**：

| 字段 | 类型 | 说明 |
|------|------|------|
| `orderId` | string/number | 生成的订单ID，用于跳转支付页 |
| `totalPrice` | number | 应付金额 |

---

## 6. 商品模块（Goods）

### 6.1 获取商品列表

```
GET /api/goods
```

**查询参数**：

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `category` | string | 可选 | 分类ID/名称 |
| `page` | number | 可选 | 页码，默认 1 |
| `pageSize` | number | 可选 | 每页数量，默认 10 |
| `keyword` | string | 可选 | 搜索关键词 |

**响应 `data`**：array

| 字段 | 类型 | 说明 |
|------|------|------|
| `id` | number | 商品ID |
| `name` | string | 商品名称 |
| `price` | number | 商品价格（元） |
| `image` | string | 商品主图路径 |
| `category` | string | 所属分类 |
| `description` | string | 商品简介 |
| `stock` | number | 库存数量 |
| `unit` | string | 计量单位（如：斤、个） |

---

### 6.2 获取商品详情

```
GET /api/goods/{id}
```

**路径参数**：

| 参数 | 类型 | 说明 |
|------|------|------|
| `id` | number | 商品ID |

**响应 `data`**：在列表基础上增加：

| 字段 | 类型 | 说明 |
|------|------|------|
| `images` | string[] | 商品图片列表 |
| `detail` | string | 商品详情富文本 |
| `specs` | array | 规格列表（可选） |
| `specs[].name` | string | 规格名 |
| `specs[].options` | string[] | 规格选项 |

---

### 6.3 搜索商品

```
GET /api/goods/search
```

**查询参数**：

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `keyword` | string | ✅ | 搜索关键词 |
| `page` | number | 可选 | 页码 |
| `pageSize` | number | 可选 | 每页数量 |
| `category` | string | 可选 | 分类筛选 |

**响应 `data`**：同商品列表

---

## 7. 农场优选模块（FarmGoods）

### 7.1 获取农场优选列表

```
GET /api/farm-goods
```

**查询参数**：

| 参数 | 类型 | 说明 |
|------|------|------|
| `category` | string | 分类ID |
| `page` | number | 页码 |
| `pageSize` | number | 每页数量 |

**响应 `data`**：同商品列表格式

---

### 7.2 获取农场优选分类

```
GET /api/farm-goods/category
```

**响应 `data`**：

| 字段 | 类型 | 说明 |
|------|------|------|
| `id` | number | 分类ID |
| `name` | string | 分类名称 |
| `icon` | string | 分类图标路径 |
| `count` | number | 该分类商品数量 |

---

## 8. 认购一亩田模块（Acre）

### 8.1 获取认购列表（首页入口）

```
GET /api/acres/index
```

**响应 `data`**：array

| 字段 | 类型 | 说明 |
|------|------|------|
| `id` | number | 地块ID |
| `title` | string | 地块名称/标题 |
| `cover` | string | 封面图路径 |
| `area` | number | 地块面积（亩） |
| `price` | number | 认购价格（元/亩） |
| `status` | string | 状态：`available`/`adopted`/`soldout` |
| `location` | string | 地块位置描述 |

---

### 8.2 获取认购列表（全量）

```
GET /api/acres
Authorization: Bearer {token}
```

**查询参数**（可选）：page、pageSize

**响应 `data`**：同 8.1

---

### 8.3 获取认购详情

```
GET /api/acres/{id}
Authorization: Bearer {token}
```

**路径参数**：

| 参数 | 类型 | 说明 |
|------|------|------|
| `id` | number | 地块ID |

**响应 `data`**：在列表基础上增加：

| 字段 | 类型 | 说明 |
|------|------|------|
| `description` | string | 地块详细描述 |
| `images` | string[] | 地块图片列表 |
| `cropType` | string | 种植作物类型 |
| `startDate` | string | 认购开始日期 |
| `endDate` | string | 认购结束日期 |
| `adoptedBy` | string | 认购人信息（已认购则显示） |

---

### 8.4 认购地块

```
POST /api/acres/{id}/adopt
Authorization: Bearer {token}
```

**路径参数**：

| 参数 | 类型 | 说明 |
|------|------|------|
| `id` | number | 地块ID |

**请求体（Body - JSON）**：

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `name` | string | 可选 | 认购人姓名 |
| `phone` | string | 可选 | 联系电话 |
| `duration` | number | 可选 | 认购期限（月） |
| `remark` | string | 可选 | 备注 |

**响应 `data`**：

| 字段 | 类型 | 说明 |
|------|------|------|
| `orderId` | string/number | 生成的订单ID，用于跳转支付页 |
| `totalPrice` | number | 应付金额 |

---

## 9. 购物车模块（Cart）

> **前端设计说明**：购物车分两种数据来源，均存储在 `wx.setStorageSync`：
> - `cartList`：商品购物车（goods 类型），带 `_cartKey` 防止 id 冲突
> - `orderCart`：点餐购物车（food 类型），格式为 `{ [id]: { id, name, price, quantity, image } }`

### 9.1 获取购物车列表

```
GET /api/cart
Authorization: Bearer {token}
```

**响应 `data`**：array

| 字段 | 类型 | 说明 |
|------|------|------|
| `id` | number | 购物车记录ID |
| `goodsId` | number | 商品ID |
| `name` | string | 商品名称 |
| `price` | number | 商品单价 |
| `image` | string | 商品图片路径 |
| `count` | number | 数量 |
| `checked` | boolean | 是否选中（前端维护，后端可选支持） |

---

### 9.2 添加商品到购物车

```
POST /api/cart/add
Authorization: Bearer {token}
```

**请求体（Body - JSON）**：

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `goodsId` | number | ✅ | 商品ID |
| `count` | number | ✅ | 添加数量 |
| `name` | string | 可选 | 商品名称（冗余存储） |
| `price` | number | 可选 | 商品价格（冗余存储） |
| `image` | string | 可选 | 商品图片（冗余存储） |

**响应 `data`**：

| 字段 | 类型 | 说明 |
|------|------|------|
| `id` | number | 购物车记录ID |
| `count` | number | 购物车中该商品当前数量（累加后） |

---

### 9.3 更新购物车商品数量

```
PUT /api/cart/{id}
Authorization: Bearer {token}
```

**路径参数**：

| 参数 | 类型 | 说明 |
|------|------|------|
| `id` | number | 购物车记录ID |

**请求体（Body - JSON）**：

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `count` | number | ✅ | 更新后的数量，为 0 时等同删除 |

---

### 9.4 删除购物车商品

```
DELETE /api/cart/{id}
Authorization: Bearer {token}
```

**路径参数**：

| 参数 | 类型 | 说明 |
|------|------|------|
| `id` | number | 购物车记录ID |

---

### 9.5 清空购物车

```
DELETE /api/cart
Authorization: Bearer {token}
```

> 清空当前用户全部购物车数据

---

## 10. 订单模块（Order / OrderDetails）

### 10.1 创建订单

```
POST /api/OrderDetails/create
Authorization: Bearer {token}
```

**请求体（Body - JSON）**：

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `sourceType` | string | ✅ | 订单来源类型：`food`（点餐）/ `goods`（商品） |
| `sourceName` | string | ✅ | 来源名称：`"点餐"` / `"商品"` |
| `quantity` | number | ✅ | 总数量 |
| `totalPrice` | number | ✅ | 订单总价（元），保留两位小数 |
| `items` | array | ✅ | 订单商品列表 |
| `items[].id` | string | ✅ | 商品ID（字符串格式） |
| `items[].name` | string | ✅ | 商品名称 |
| `items[].price` | number | ✅ | 商品单价（纯数字，已去除 ¥ 符号） |
| `items[].quantity` | number | ✅ | 商品数量 |
| `items[].image` | string | 可选 | 商品图片路径 |
| `tableNumber` | number | 条件必填 | 桌台号（`sourceType=food` 时必填，为 0 则不选桌台） |
| `address` | object | 条件必填 | 收货地址对象（`sourceType=goods` 时必填） |
| `address.id` | string/number | 可选 | 地址ID |
| `address.name` | string | 可选 | 收件人姓名 |
| `address.phone` | string | 可选 | 收件人电话 |
| `address.address` | string | 可选 | 详细地址 |

**请求示例（商品订单）**：
```json
{
  "sourceType": "goods",
  "sourceName": "商品",
  "quantity": 2,
  "totalPrice": 58.00,
  "address": {
    "id": "1",
    "name": "张三",
    "phone": "13800138000",
    "address": "广东省广州市天河区xxx路"
  },
  "items": [
    {
      "id": "101",
      "name": "新鲜草莓",
      "price": 29.00,
      "quantity": 2,
      "image": "/images/strawberry.jpg"
    }
  ]
}
```

**请求示例（点餐订单）**：
```json
{
  "sourceType": "food",
  "sourceName": "点餐",
  "quantity": 3,
  "tableNumber": 2,
  "totalPrice": 45.50,
  "items": [
    {
      "id": "201",
      "name": "农家炒鸡蛋",
      "price": 18.00,
      "quantity": 2,
      "image": ""
    }
  ]
}
```

**响应 `data`**：

| 字段 | 类型 | 说明 |
|------|------|------|
| `orderId` | string/number | 订单ID，用于跳转支付页 |
| `orderNumber` | string | 订单编号（展示用） |
| `totalPrice` | number | 实付金额 |

---

### 10.2 获取订单列表

```
GET /api/orders
Authorization: Bearer {token}
```

**查询参数**：

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `type` | string | 可选 | 订单类型筛选：`food`/`goods`/`acre`/`activity`，空则全部 |
| `status` | string | 可选 | 订单状态筛选：`pending`/`paid`/`shipping`/`cancelled` |
| `page` | number | 可选 | 页码，默认 1 |
| `pageSize` | number | 可选 | 每页数量，默认 10 |
| `sortBy` | string | 可选 | 排序字段，默认 `createTime` |
| `sortOrder` | string | 可选 | 排序方向：`desc`/`asc`，默认 `desc` |

**响应 `data`**：

| 字段 | 类型 | 说明 |
|------|------|------|
| `orders` | array | 订单列表 |
| `total` | number | 总数量 |
| `page` | number | 当前页码 |
| `pageSize` | number | 每页数量 |

**订单对象字段**：

| 字段 | 类型 | 说明 |
|------|------|------|
| `id` | number/string | 订单ID |
| `orderNumber` | string | 订单编号 |
| `type` | string | 订单类型：`food`/`goods`/`acre`/`activity` |
| `typeText` | string | 订单类型中文描述 |
| `status` | string | 订单状态：`pending`/`paid`/`shipping`/`delivered`/`cancelled` |
| `statusText` | string | 状态中文描述 |
| `totalPrice` | number | 订单总价 |
| `createTime` | string | 创建时间（ISO 8601），用于超时倒计时计算 |
| `items` | array | 商品列表 |
| `items[].id` | number | 商品ID |
| `items[].name` | string | 商品名称 |
| `items[].price` | number | 商品价格 |
| `items[].quantity` | number | 商品数量 |
| `items[].image` | string | 商品图片路径 |

---

### 10.3 获取订单详情

```
GET /api/orders/{id}
Authorization: Bearer {token}
```

**路径参数**：

| 参数 | 类型 | 说明 |
|------|------|------|
| `id` | number/string | 订单ID |

**响应 `data`**：在列表基础上增加：

| 字段 | 类型 | 说明 |
|------|------|------|
| `address` | object | 收货地址信息（goods 类型） |
| `tableNumber` | number | 桌台号（food 类型） |
| `paymentMethod` | string | 支付方式 |
| `payTime` | string | 支付时间 |
| `remark` | string | 备注 |
| `logistics` | object | 物流信息（可选） |

---

### 10.4 更新订单状态

```
PUT /api/orders/{id}/status
Authorization: Bearer {token}
```

**路径参数**：

| 参数 | 类型 | 说明 |
|------|------|------|
| `id` | number/string | 订单ID |

**请求体（Body - JSON）**：

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `status` | string | ✅ | 新状态值：`cancelled`/`shipped`/`delivered`/`completed` |

> **使用场景**：
> - 超时自动取消订单：`PUT .../status { "status": "cancelled" }`
> - 用户确认收货：`PUT .../status { "status": "delivered" }`

---

### 10.5 删除订单

```
DELETE /api/orders/{id}
Authorization: Bearer {token}
```

**路径参数**：

| 参数 | 类型 | 说明 |
|------|------|------|
| `id` | number/string | 订单ID |

> 仅允许删除已取消（`status=cancelled`）或已完成（`status=completed`）的订单

---

### 10.6 获取订单统计数量

```
GET /api/orders/counts
Authorization: Bearer {token}
```

**响应 `data`**：

| 字段 | 类型 | 说明 |
|------|------|------|
| `pending` | number | 待付款订单数 |
| `paid` | number | 待发货订单数 |
| `shipping` | number | 待收货订单数 |
| `total` | number | 总订单数 |

---

### 10.7 获取订单核销二维码

```
GET /api/orders/{id}/qrcode
Authorization: Bearer {token}
```

**路径参数**：

| 参数 | 类型 | 说明 |
|------|------|------|
| `id` | number/string | 订单ID |

**响应 `data`**：

| 字段 | 类型 | 说明 |
|------|------|------|
| `qrcode` | string | 二维码图片 Base64 或 URL |
| `code` | string | 核销码（明文，供员工扫码） |
| `expireTime` | string | 二维码过期时间 |

---

### 10.8 模拟支付（测试用）

```
POST /api/orders/{id}/mock-pay
Authorization: Bearer {token}
```

> **仅用于测试环境**，正式上线应使用 `/api/pay/jsapi`

**请求体**：空或 `{}`

**响应 `data`**：

| 字段 | 类型 | 说明 |
|------|------|------|
| `success` | boolean | 支付是否成功 |
| `orderId` | string/number | 订单ID |

---

## 11. 用户模块（User）

### 11.1 获取用户信息

```
GET /api/user/profile
Authorization: Bearer {token}
```

**响应 `data`**：

| 字段 | 类型 | 说明 |
|------|------|------|
| `nickname` | string | 昵称 |
| `avatar` | string | 头像路径 |
| `phone` | string | 手机号（脱敏） |
| `email` | string | 邮箱 |
| `balance` | number | 账户余额（元） |
| `reward` | number | 积分/奖励值 |
| `role` | string | 角色：`user`/`staff` |

---

### 11.2 更新用户信息

```
PUT /api/user/profile
Authorization: Bearer {token}
```

**请求体（Body - JSON）**：

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `nickname` | string | 可选 | 昵称 |
| `avatar` | string | 可选 | 头像路径（上传后的相对路径） |
| `email` | string | 可选 | 邮箱 |

---

### 11.3 用户登录（账号密码，兼容接口）

```
POST /api/user/login
```

**请求体（Body - JSON）**：

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `username` | string | ✅ | 用户名 |
| `password` | string | ✅ | 密码 |

> **注**：当前小程序主要使用微信手机号一键登录（`/api/Auth/wx-phone-login`），此接口为兼容保留

---

### 11.4 用户注册（兼容接口）

```
POST /api/user/register
```

**请求体（Body - JSON）**：

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `username` | string | ✅ | 用户名 |
| `password` | string | ✅ | 密码 |
| `phone` | string | 可选 | 手机号 |

---

## 12. 地址模块（Address）

### 12.1 获取地址列表

```
GET /api/address/list
Authorization: Bearer {token}
```

**查询参数**：无

**响应 `data`**：array

| 字段 | 类型 | 说明 |
|------|------|------|
| `id` | string/number | 地址ID |
| `name` | string | 收件人姓名 |
| `phone` | string | 收件人手机号 |
| `province` | string | 省份 |
| `city` | string | 城市 |
| `district` | string | 区/县 |
| `address` | string | 详细地址（街道+门牌号） |
| `isDefault` | boolean | 是否为默认地址 |

> **前端 UI 逻辑**：
> - 默认只展示默认地址（`isDefault=true`），无默认地址则展示第一条
> - 点击"更换地址"后展示全部地址列表（`showAllAddresses=true`）

---

### 12.2 获取地址列表（User 路径，兼容）

```
GET /api/user/address
Authorization: Bearer {token}
```

> 同 `/api/address/list`，字段格式一致

---

### 12.3 获取地址详情

```
GET /api/address/{id}
Authorization: Bearer {token}
```

**路径参数**：

| 参数 | 类型 | 说明 |
|------|------|------|
| `id` | string/number | 地址ID |

**响应 `data`**：同地址列表单条字段

---

### 12.4 新增地址

```
POST /api/user/address
Authorization: Bearer {token}
```

**请求体（Body - JSON）**：

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `name` | string | ✅ | 收件人姓名 |
| `phone` | string | ✅ | 手机号（格式：1[3-9]XXXXXXXXX） |
| `province` | string | ✅ | 省份 |
| `city` | string | ✅ | 城市 |
| `district` | string | 可选 | 区/县 |
| `address` | string | ✅ | 详细地址（街道+门牌号） |
| `isDefault` | boolean | 可选 | 是否设为默认地址，默认 `false` |

**响应 `data`**：

| 字段 | 类型 | 说明 |
|------|------|------|
| `id` | string/number | 新建地址ID |

---

### 12.5 修改地址

```
PUT /api/user/address
Authorization: Bearer {token}
```

**请求体（Body - JSON）**：

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `id` | string/number | ✅ | 地址ID（必须包含） |
| `name` | string | ✅ | 收件人姓名 |
| `phone` | string | ✅ | 手机号 |
| `province` | string | ✅ | 省份 |
| `city` | string | ✅ | 城市 |
| `district` | string | 可选 | 区/县 |
| `address` | string | ✅ | 详细地址 |
| `isDefault` | boolean | 可选 | 是否设为默认地址 |

> **注意**：新增和修改均调用 `/api/user/address`，通过请求方式区分（POST/PUT），修改时请求体中需包含 `id` 字段

---

### 12.6 删除地址

```
DELETE /api/user/address/{id}
Authorization: Bearer {token}
```

**路径参数**：

| 参数 | 类型 | 说明 |
|------|------|------|
| `id` | string/number | 地址ID |

---

## 13. 支付模块（Pay）

### 13.1 获取支付页展示信息

```
GET /api/pay/info?orderId={orderId}
Authorization: Bearer {token}
```

**查询参数**：

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `orderId` | string/number | ✅ | 订单ID |

**响应 `data`**：

| 字段 | 类型 | 说明 |
|------|------|------|
| `orderId` | string/number | 订单ID |
| `orderNumber` | string | 订单编号 |
| `totalPrice` | number | 应付金额 |
| `paymentStatus` | number | 支付状态：`0`=未支付，`1`=已支付 |
| `expireTime` | string | 支付超时时间 |

---

### 13.2 创建微信 JSAPI 支付

```
POST /api/pay/jsapi
Authorization: Bearer {token}
```

**请求体（Body - JSON）**：

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `orderId` | string/number | ✅ | 订单ID |
| `description` | string | 可选 | 支付描述，展示在微信支付收银台（如：`"能记农场订单 1234"`） |

**响应 `data`**：

| 字段 | 类型 | 说明 |
|------|------|------|
| `paymentStatus` | number | 当前支付状态，`1` 表示已支付（无需再次调起支付） |
| `payParams` | object/null | 微信支付参数，已支付时为 `null` |
| `payParams.timeStamp` | string | 时间戳 |
| `payParams.nonceStr` | string | 随机字符串 |
| `payParams.package` | string | 统一下单返回的 `prepay_id` 封装值 |
| `payParams.signType` | string | 签名类型，通常 `"RSA"` 或 `"MD5"` |
| `payParams.paySign` | string | 签名值 |

**前端调用流程**：
```
POST /api/pay/jsapi → 获取 payParams → wx.requestPayment(payParams) → POST /api/pay/query-payment-status
```

---

### 13.3 发起支付（兼容接口）

```
POST /api/pay/initiate-payment
Authorization: Bearer {token}
```

**请求体**：同 13.2

> 与 `/api/pay/jsapi` 功能相同，为旧版本兼容保留

---

### 13.4 查询本地订单支付状态

```
GET /api/pay/status?orderId={orderId}
Authorization: Bearer {token}
```

**查询参数**：

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `orderId` | string/number | ✅ | 订单ID |

**响应 `data`**：

| 字段 | 类型 | 说明 |
|------|------|------|
| `paid` | boolean | 是否已支付 |
| `status` | string | 支付状态 |

---

### 13.5 微信查单并同步本地状态

```
POST /api/pay/query-payment-status
Authorization: Bearer {token}
```

**请求体（Body - JSON）**：

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `orderId` | string/number | ✅ | 订单ID |

**响应 `data`**：

| 字段 | 类型 | 说明 |
|------|------|------|
| `paid` | boolean | 是否已支付（微信确认结果） |
| `transactionId` | string | 微信交易流水号 |
| `status` | string | 最新订单状态 |

---

### 13.6 获取可用支付方式

```
GET /api/pay/methods
Authorization: Bearer {token}
```

**响应 `data`**：array

| 字段 | 类型 | 说明 |
|------|------|------|
| `id` | string | 支付方式ID，如 `"wechat"` |
| `name` | string | 支付方式名称，如 `"微信支付"` |
| `enabled` | boolean | 是否启用 |

---

## 14. 物流模块（Logistics）

### 14.1 获取物流详情

```
GET /api/logistics/{orderId}
Authorization: Bearer {token}
```

**路径参数**：

| 参数 | 类型 | 说明 |
|------|------|------|
| `orderId` | string/number | 订单ID |

**响应 `data`**：

| 字段 | 类型 | 说明 |
|------|------|------|
| `company` | string | 快递公司名称 |
| `trackingNumber` | string | 快递单号 |
| `status` | string | 物流状态：`shipping`/`delivered` |
| `estimatedTime` | string | 预计到达时间 |

---

### 14.2 获取物流轨迹

```
GET /api/logistics/{orderId}/trace
Authorization: Bearer {token}
```

**路径参数**：

| 参数 | 类型 | 说明 |
|------|------|------|
| `orderId` | string/number | 订单ID |

**响应 `data`**：

| 字段 | 类型 | 说明 |
|------|------|------|
| `traces` | array | 轨迹列表（按时间倒序） |
| `traces[].time` | string | 轨迹时间（ISO 8601） |
| `traces[].content` | string | 轨迹描述 |
| `traces[].location` | string | 当前位置 |
| `traces[].status` | string | 节点状态 |

---

## 15. 员工核销模块（Staff）

> **详细文档**请参见：`docs/staff-api-requirements.md`

### 15.1 扫码核销券

```
POST /api/staff/verify
Authorization: Bearer {token}（role=staff）
```

**请求体（Body - JSON）**：

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `code` | string | ✅ | 扫码获取的核销码 |

**响应 `data`**：

| 字段 | 类型 | 说明 |
|------|------|------|
| `success` | boolean | 核销是否成功 |
| `voucherType` | string | 券类型：`activity`/`picking` |
| `userName` | string | 用户姓名 |
| `content` | string | 核销内容描述 |
| `verifyTime` | string | 核销时间 |

---

### 15.2 获取待核销券列表

```
GET /api/staff/vouchers
Authorization: Bearer {token}（role=staff）
```

**查询参数**：

| 参数 | 类型 | 说明 |
|------|------|------|
| `page` | number | 页码 |
| `pageSize` | number | 每页数量 |
| `type` | string | 券类型筛选 |

**响应 `data`**：array（待核销券列表）

---

### 15.3 获取核销历史记录

```
GET /api/staff/verify-history
Authorization: Bearer {token}（role=staff）
```

**查询参数**：

| 参数 | 类型 | 说明 |
|------|------|------|
| `page` | number | 页码 |
| `pageSize` | number | 每页数量 |
| `startDate` | string | 开始日期 |
| `endDate` | string | 结束日期 |

---

### 15.4 获取今日统计数据（新增需求）

```
GET /api/staff/today-stats
Authorization: Bearer {token}（role=staff）
```

**响应 `data`**：

| 字段 | 类型 | 说明 |
|------|------|------|
| `todayVerified` | number | 今日核销总数 |
| `pendingCount` | number | 待核销数量 |
| `activityVerified` | number | 今日活动券核销数 |
| `pickingVerified` | number | 今日采摘券核销数 |

---

## 16. 点餐模块（Order Food）

### 16.1 获取点餐菜单

```
GET /api/order
```

**查询参数**（可选）：

| 参数 | 类型 | 说明 |
|------|------|------|
| `category` | string | 菜品分类 |

**响应 `data`**：

| 字段 | 类型 | 说明 |
|------|------|------|
| `categories` | array | 菜品分类列表 |
| `categories[].id` | number | 分类ID |
| `categories[].name` | string | 分类名称 |
| `categories[].items` | array | 该分类下菜品列表 |
| `items[].id` | number | 菜品ID |
| `items[].name` | string | 菜品名称 |
| `items[].price` | number | 菜品价格（元） |
| `items[].image` | string | 菜品图片路径 |
| `items[].description` | string | 菜品描述 |
| `items[].available` | boolean | 是否可点餐 |

> **前端点餐购物车说明**：
> - 菜品加入购物车后，存储到 `orderCart` Storage：`{ [id]: { id, name, price, quantity, image } }`
> - 下单时通过 `POST /api/OrderDetails/create`（`sourceType=food`）创建订单

---

## 17. 错误码规范

| HTTP 状态码 / 业务 code | 说明 | 前端处理 |
|------------------------|------|---------|
| `0` | 成功 | resolve(data) |
| `400` | 请求参数错误 | Toast 显示 message |
| `401` | 未登录或 token 失效 | 跳转 `/pages/login/login` |
| `403` | 无权限（如非员工访问员工接口） | Toast 提示"无权限" |
| `404` | 资源不存在 | Toast 提示"数据不存在" |
| `409` | 冲突（如手机号已绑定其他账号） | Toast 提示具体冲突内容 |
| `500` | 服务器内部错误 | Toast 提示"服务异常，请稍后重试" |
| 网络失败 | 网络不可达 | Toast 提示"网络错误" |

---

## 18. Storage 字段说明

> 小程序端使用 `wx.setStorageSync` / `wx.getStorageSync` 存储的关键字段

| Storage Key | 类型 | 说明 | 来源 |
|-------------|------|------|------|
| `token` | string | JWT 登录令牌 | `POST /api/Auth/wx-phone-login` |
| `hasLogin` | boolean | 登录状态标志 | 登录成功后设为 true |
| `user_id` | number/string | 用户ID | 登录接口返回 |
| `user_guid` | string | 用户 GUID | 登录接口返回 |
| `openid` | string | 微信 OpenID | 登录接口返回 |
| `phone_number` | string | 用户手机号 | 登录接口返回 |
| `user_role` | string | 用户角色：`user`/`staff` | 登录接口返回的 `role` 字段 |
| `register_time` | string | 注册时间 | 登录接口返回 |
| `user_profile_cache` | object | 用户信息缓存（预取） | `GET /api/user/profile` |
| `cartList` | array | 商品购物车列表 | 本地维护，带 `_cartKey` |
| `orderCart` | object | 点餐购物车：`{ [id]: item }` | 本地维护 |
| `tableNumber` | string | 当前选择的桌台号 | 点餐页选择后写入 |

---

## 附录：接口清单速查

| 模块 | 方法 | 路径 | 需鉴权 |
|------|------|------|--------|
| Auth | POST | `/api/Auth/wx-phone-login` | ❌ |
| Home | GET | `/api/home` | ❌ |
| File | GET | `/api/file/images` | ❌ |
| File | GET | `/api/file/image/{name}` | ❌ |
| File | POST | `/api/upload` | ✅（可选） |
| Activity | GET | `/api/activity/list` | ❌ |
| Activity | GET | `/api/activity/detail` | ❌ |
| Activity | POST | `/api/activity/{id}/register` | 可选 |
| Goods | GET | `/api/goods` | ❌ |
| Goods | GET | `/api/goods/{id}` | ❌ |
| Goods | GET | `/api/goods/search` | ❌ |
| FarmGoods | GET | `/api/farm-goods` | ❌ |
| FarmGoods | GET | `/api/farm-goods/category` | ❌ |
| Acre | GET | `/api/acres/index` | ❌ |
| Acre | GET | `/api/acres` | ✅ |
| Acre | GET | `/api/acres/{id}` | ✅ |
| Acre | POST | `/api/acres/{id}/adopt` | ✅ |
| Cart | GET | `/api/cart` | ✅ |
| Cart | POST | `/api/cart/add` | ✅ |
| Cart | PUT | `/api/cart/{id}` | ✅ |
| Cart | DELETE | `/api/cart/{id}` | ✅ |
| Cart | DELETE | `/api/cart` | ✅ |
| Order | POST | `/api/OrderDetails/create` | ✅ |
| Order | GET | `/api/orders` | ✅ |
| Order | GET | `/api/orders/{id}` | ✅ |
| Order | PUT | `/api/orders/{id}/status` | ✅ |
| Order | DELETE | `/api/orders/{id}` | ✅ |
| Order | GET | `/api/orders/counts` | ✅ |
| Order | GET | `/api/orders/{id}/qrcode` | ✅ |
| Order | POST | `/api/orders/{id}/mock-pay` | ✅ |
| User | GET | `/api/user/profile` | ✅ |
| User | PUT | `/api/user/profile` | ✅ |
| User | POST | `/api/user/login` | ❌ |
| User | POST | `/api/user/register` | ❌ |
| Address | GET | `/api/address/list` | ✅ |
| Address | GET | `/api/address/{id}` | ✅ |
| Address | GET | `/api/user/address` | ✅ |
| Address | POST | `/api/user/address` | ✅ |
| Address | PUT | `/api/user/address` | ✅ |
| Address | DELETE | `/api/user/address/{id}` | ✅ |
| Pay | GET | `/api/pay/info` | ✅ |
| Pay | POST | `/api/pay/jsapi` | ✅ |
| Pay | POST | `/api/pay/initiate-payment` | ✅ |
| Pay | GET | `/api/pay/status` | ✅ |
| Pay | POST | `/api/pay/query-payment-status` | ✅ |
| Pay | GET | `/api/pay/methods` | ✅ |
| Logistics | GET | `/api/logistics/{orderId}` | ✅ |
| Logistics | GET | `/api/logistics/{orderId}/trace` | ✅ |
| Staff | POST | `/api/staff/verify` | ✅（staff） |
| Staff | GET | `/api/staff/vouchers` | ✅（staff） |
| Staff | GET | `/api/staff/verify-history` | ✅（staff） |
| Staff | GET | `/api/staff/today-stats` | ✅（staff） |
| Food | GET | `/api/order` | ❌ |

> **合计：52 个接口**

---

*文档由源码自动整理生成，如有接口变更请及时同步更新本文档。*
