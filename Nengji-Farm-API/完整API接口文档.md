# 能记农场 — 完整 API 接口文档

> 生成日期：2026-05-28
> 基础地址：`http://<host>:<port>/api`
> 响应格式（大部分）：`{ code: 200, message: "success", data: ... }`

---

## 目录

1. [用户端接口](#一用户端接口)
   - [Auth 认证](#11-auth-认证-api-auth)
   - [Home 首页](#12-home-首页-api-home)
   - [Goods 商品](#13-goods-商品-api-goods)
   - [Farm Goods 农场商品](#14-farm-goods-农场商品-api-farm-goods)
   - [Cart 购物车](#15-cart-购物车-api-cart)
   - [Order 点餐/下单](#16-order-点餐下单-api-order)
   - [Orders 订单管理](#17-orders-订单管理-api-orders)
   - [OrderDetails 统一订单](#18-orderdetails-统一订单-api-orderdetails)
   - [Activity 活动](#19-activity-活动-api-activity)
   - [Acres 认购一亩田](#110-acres-认购一亩田-api-acres)
   - [Points 积分](#111-points-积分-api-points)
   - [User 用户](#112-user-用户-api-user)
   - [Farm 农场介绍](#113-farm-农场介绍-api-farm)
   - [Refund 退款](#114-refund-退款-api-refund)
   - [Common 通用](#115-common-通用-api-common)
   - [Pay 支付](#116-pay-支付-api-pay)
   - [Logistics 物流](#117-logistics-物流-api-logistics)
   - [DiningTable 桌台](#118-diningtable-桌台-api-dining-table)
   - [Staff 员工核销](#119-staff-员工核销-api-staff)
   - [StaffVerify 核销验证](#120-staffverify-核销验证-api-staff-verify)
   - [Kitchen 后厨](#121-kitchen-后厨-api-kitchen)

2. [管理后台接口](#二管理后台接口)
   - [Activity Manage 活动管理](#21-activity-manage-活动管理-api-activity-manage)
   - [Back User 管理员用户](#22-back-user-管理员用户-api-back-user)
   - [Product 商品管理](#23-product-商品管理-api-product)
   - [Dish 菜品管理](#24-dish-菜品管理-api-dish)
   - [Table 餐桌管理](#25-table-餐桌管理-api-table)
   - [Activity Order 活动订单管理](#26-activity-order-活动订单管理-api-activity-order)
   - [Product Order 商品订单管理](#27-product-order-商品订单管理-api-product-order)
   - [Dish Order 菜品订单管理](#28-dish-order-菜品订单管理-api-dish-order)
   - [Points Manage 积分管理](#29-points-manage-积分管理-api-back-points)
   - [File 文件服务](#210-file-文件服务-api-file)

---

# 一、用户端接口

## 1.1 Auth 认证 (`/api/auth`)

### POST `/api/auth/wx-login` — 微信登录

使用微信 code 换取 openId 进行登录/注册。

**Body (JSON):**
| 参数 | 类型 | 必填 | 说明 |
|---|---|---|---|
| code | string | 是 | 微信登录 code |
| nickname | string | 否 | 用户昵称 |
| avatar | string | 否 | 用户头像 URL |

**响应 data:**
```json
{
  "token": "jwt_token_string",
  "expireMinutes": 43200,
  "user": { "id": 1, "userNo": "wx_xxx", "nickname": "昵称", "avatar": "url", "phone": "" },
  "userInfo": { ... }
}
```

### POST `/api/auth/wx-phone-login` — 微信手机号登录

**Body (JSON):**
| 参数 | 类型 | 必填 | 说明 |
|---|---|---|---|
| code | string | 是 | 微信登录 code |
| phoneCode | string | 是 | 手机号 code |

### POST `/api/auth/phone-login` — 手机号登录

**Body (JSON):**
| 参数 | 类型 | 必填 | 说明 |
|---|---|---|---|
| phone | string | 是 | 手机号 |

### GET `/api/auth/get-phone` — 获取微信手机号

需登录。

**Body (JSON):**
| 参数 | 类型 | 必填 | 说明 |
|---|---|---|---|
| code | string | 是 | 手机号 code |

### GET `/api/auth/check` — 校验登录态

需登录。返回 200 表示 Token 有效。

### GET `/api/auth/diagnostics` — 健康检查

无需登录。返回服务器运行状态。

---

## 1.2 Home 首页 (`/api/home`)

### GET `/api/home` — 首页数据

无需登录。

**响应 data:**
```json
{
  "swiperList": [{ "id": 1, "image": "url" }],
  "functionButtons": [{ "id": 1, "name": "认购一亩田", "color": "#...", "path": "/pages/..." }],
  "farmGoods": [{ "id": 1, "name": "...", "image": "...", "price": 12.8, "originalPrice": 12.8, "stock": 50, "tags": ["新鲜"] }],
  "hotDishes": [/* 同 farmGoods 结构 */]
}
```

### GET `/api/home/search` — 全局搜索

| 参数 | 类型 | 必填 | 说明 |
|---|---|---|---|
| keyword | string | 是 | 搜索关键词 |
| page | int | 否 | 页码，默认 1 |
| pageSize | int | 否 | 每页条数，默认 20 |

跨表搜索商品(commodity)、菜品(dish)、活动(activity)。

---

## 1.3 Goods 商品 (`/api/goods`)

### GET `/api/goods` — 商品/菜品列表

| 参数 | 类型 | 必填 | 说明 |
|---|---|---|---|
| type | string | 否 | `goods`(默认) 或 `food` |
| categoryId | int | 否 | 分类 ID 筛选 |
| page | int | 否 | 默认 1 |
| pageSize | int | 否 | 默认 50 |

type=food 返回 dishes，否则返回 commodities。

### GET `/api/goods/categories` — 获取分类

| 参数 | 类型 | 必填 | 说明 |
|---|---|---|---|
| type | string | 否 | `goods` 或 `food` |

### GET `/api/goods/{id}` — 商品详情

| 参数 | 类型 | 必填 | 说明 |
|---|---|---|---|
| type | string | 否 | `goods` 或 `food` |

优先查 commodity，未命中则查 dish。

### GET `/api/goods/detail` — 商品详情（别名）

| 参数 | 类型 | 必填 | 说明 |
|---|---|---|---|
| goodsId / goods_id | int | 是 | 商品 ID |
| type | string | 否 | `goods` 或 `food` |

### GET `/api/goods/search` — 商品搜索

| 参数 | 类型 | 必填 | 说明 |
|---|---|---|---|
| keyword | string | 否 | 关键词 |
| page | int | 否 | 默认 1 |
| pageSize | int | 否 | 默认 20 |

返回含分类信息的商品列表。

---

## 1.4 Farm Goods 农场商品 (`/api/farm-goods`)

### GET `/api/farm-goods` — 农场商品首页

分类列表 + 推荐商品。

### GET `/api/farm-goods/list` — 商品列表

| 参数 | 类型 | 必填 | 说明 |
|---|---|---|---|
| categoryId | int | 否 | 分类 ID，传空或0返回全部 |
| page | int | 否 | 默认 1 |
| pageSize | int | 否 | 默认 20 |

### GET `/api/farm-goods/detail/{goodsId}` — 商品详情

### GET `/api/farm-goods/search` — 搜索

---

## 1.5 Cart 购物车 (`/api/cart`)

需登录。

### GET `/api/cart/list` — 购物车列表

### POST `/api/cart/sync` / `items` — 全量同步购物车（替换式）

**Body (JSON):**
```json
{
  "items": [{ "goodsId": "1", "count": 2, "spec": "500g" }]
}
```

### POST `/api/cart/add` — 添加

| 参数 | 说明 |
|---|---|
| goodsId / goods_id / commodityId | 商品 ID |
| count / quantity / num | 数量 |

### POST `/api/cart/update` — 更新数量

| 参数 | 说明 |
|---|---|
| id / cartId | 购物车项 ID |
| count / quantity / num | 新数量 |

### POST `/api/cart/delete` — 删除

| 参数 | 说明 |
|---|---|
| id / goodsId | 购物车项 ID |

### POST `/api/cart/clear` — 清空

---

## 1.6 Order 点餐/下单 (`/api/order`)

### GET `/api/order/tables` — 可用桌台列表

无需登录。

**响应：**
```json
[
  { "id": 1, "name": "包间1", "status": "free", "statusText": "空闲", "statusId": 1 }
]
```

> statusId: 1=空闲, 2=使用中（停用桌台已过滤不返回）

### GET `/api/order/table-qrcodes` — 所有桌台二维码（含 Base64）

无需登录。

### GET `/api/order/table-qrcode/{tableId}` — 单个桌台二维码 PNG

无需登录。

### GET `/api/order/getPageData` — 菜单页面数据

分类 + 菜品列表 + 桌台选项。

### GET `/api/order/getOrderData` — 按分类分组菜单

### POST `/api/order/create` — 创建订单

需登录。

| 参数 | 说明 |
|---|---|
| sourceType | `commodity`/`food`/`activity` |
| items | 商品数组 [{goodsId, quantity, ...}] |
| addressId | 收货地址 ID |
| diningTableId | 桌台 ID |
| remark | 备注 |

### GET `/api/order/list` — 订单列表

### GET `/api/order/detail` — 订单详情

### POST `/api/order/cancel` — 取消订单

### POST `/api/order/pay` — 模拟支付

### POST `/api/order/confirm` — 确认收货

---

## 1.7 Orders 订单管理 (`/api/orders`)

需登录。

### GET `/api/orders/list` — 聚合订单列表

| 参数 | 说明 |
|---|---|
| type | 筛选类型 |
| status | 筛选状态 |
| keyword | 搜索关键词 |
| page | 默认 1 |
| pageSize | 默认 10 |

### GET `/api/orders/search` — 订单关键词搜索

### GET `/api/orders/counts` — 各状态订单数

### GET `/api/orders/detail` — 订单详情

| 参数 | 说明 |
|---|---|
| orderNo | 订单号 |
| type | 订单类型 |

### POST `/api/orders/updateStatus` — 更新订单状态

| 参数 | 说明 |
|---|---|
| orderNo | 订单号 |
| type | 订单类型 |
| action | `cancel`/`ship`/`complete` |

### POST `/api/orders/mock-pay` — 模拟支付

### GET `/api/orders/qrcode` — 核销二维码

### POST `/api/orders/delete` — 删除已取消订单

---

## 1.8 OrderDetails 统一订单 (`/api/OrderDetails`)

需登录。

### GET `/api/OrderDetails/List` — 聚合订单列表

### GET `/api/OrderDetails/Detail` — 订单详情

### POST `/api/OrderDetails/Create` — 统一创建订单

根据 sourceType 自动路由到商品/点餐/活动订单创建。

### POST `/api/OrderDetails/Pay` — 支付

### POST `/api/OrderDetails/Cancel` — 取消

### POST `/api/OrderDetails/Confirm` — 确认收货

---

## 1.9 Activity 活动 (`/api/activity`)

### GET `/api/activity/list` — 活动列表

| 参数 | 说明 |
|---|---|
| type | 活动类型筛选 |
| page | 默认 1 |
| pageSize | 默认 10 |

### GET `/api/activity/category-list` — 按分类分组活动

### GET `/api/activity/detail/{id}` — 活动详情

### POST `/api/activity/register` — 活动报名

| 参数 | 说明 |
|---|---|
| activityId | 活动 ID |
| quantity | 数量 |
| name | 联系人 |
| phone | 联系电话 |

---

## 1.10 Acres 认购一亩田 (`/api/acres`)

### GET `/api/acres/page-data` — 认购首页数据

轮播图 + 认购项目列表。

### GET `/api/acres/list` — 认购项目列表

| 参数 | 说明 |
|---|---|
| page | 默认 1 |
| pageSize | 默认 10 |

### GET `/api/acres/detail/{id}` — 认购项目详情

### POST `/api/acres/adopt` — 认购

需登录。

### GET `/api/acres/logs` — 认购记录

需登录。

---

## 1.11 Points 积分 (`/api/points`)

所有接口需登录。

### GET `/api/points/summary` — 积分总览

```json
{
  "totalPoints": 100,
  "earnedPoints": 200,
  "spentPoints": 100,
  "todayEarned": 10
}
```

### GET `/api/points/goods` — 积分商品列表

| 参数 | 说明 |
|---|---|
| page | 默认 1 |
| pageSize | 默认 20 |

### GET `/api/points/goods/{id}` — 积分商品详情

### POST `/api/points/exchange` — 兑换商品

| 参数 | 说明 |
|---|---|
| commodityId | 积分商品 ID |
| quantity | 数量 |

### GET `/api/points/exchange-detail/{orderNo}` — 兑换详情（含二维码）

### GET `/api/points/records` — 积分流水

| 参数 | 说明 |
|---|---|
| type | `earn`(收入) 或 `spend`(支出) |
| page | 默认 1 |
| pageSize | 默认 20 |

### GET `/api/points/exchange-records` — 兑换记录

### POST `/api/points/exchange-cancel` — 取消兑换（仅待核销可取消，退回积分）

### POST `/api/points/earn` — 手动积分入账（管理员）

| 参数 | 说明 |
|---|---|
| amount | 消费金额 |
| orderNo | 订单号（可选，自动生成） |

### GET `/api/points/rule` — 积分规则

---

## 1.12 User 用户 (`/api/user`)

需登录。

### GET `/api/user/profile` — 获取用户信息

### POST `/api/user/update` — 更新用户信息

| 参数 | 说明 |
|---|---|
| nickname | 昵称 |
| avatar | 头像 URL |
| gender | 性别 |
| phone | 手机号 |

### GET `/api/user/addresses` — 地址列表

### POST `/api/user/address-add` — 新增地址

| 参数 | 说明 |
|---|---|
| name | 联系人 |
| phone | 联系电话 |
| province | 省 |
| city | 市 |
| district | 区 |
| address | 详细地址 |

### POST `/api/user/address-update` — 更新地址

### POST `/api/user/address-delete` — 删除地址

| 参数 | 说明 |
|---|---|
| id | 地址 ID |

---

## 1.13 Farm 农场介绍 (`/api/farm`)

### GET `/api/farm/intro` — 农场介绍页

轮播图 + 特色视频 + 农场介绍/理念/联系方式。

---

## 1.14 Refund 退款 (`/api/refund`)

需登录。

### POST `/api/refund/apply` — 申请退款

| 参数 | 说明 |
|---|---|
| orderNo | 订单号 |
| orderType | `goods`/`food`/`activity` |
| reason | 退款原因 |
| images | 凭证图片数组（最多3张） |

### GET `/api/refund/detail` — 退款详情

### GET `/api/refund/list` — 退款记录列表

### POST `/api/refund/cancel` — 取消退款申请

---

## 1.15 Common 通用 (`/api/common`)

### POST `/api/common/upload` — 文件上传

**Content-Type:** `multipart/form-data`

| 参数 | 类型 | 说明 |
|---|---|---|
| file | File | 支持 jpg/png/gif/webp/mp4/mov/avi，最大 50MB |

---

## 1.16 Pay 支付 (`/api/pay`)

### POST `/api/pay/create-jsapi` — 微信 JSAPI 支付

| 参数 | 说明 |
|---|---|
| orderNo | 订单号 |
| type | 订单类型（goods/food/activity） |
| openId | 用户 openId |

### GET `/api/pay/status` — 查询支付状态

| 参数 | 说明 |
|---|---|
| orderNo | 订单号 |

### POST `/api/pay/notify` — 微信支付回调

### GET `/api/pay/info` — 支付信息

---

## 1.17 Logistics 物流 (`/api/logistics`)

### GET `/api/logistics/{orderId}` — 物流详情

### GET `/api/logistics/{orderId}/trace` — 物流轨迹

### POST `/api/logistics/track` — 实时物流查询

| 参数 | 说明 |
|---|---|
| companyType | `sf`/`jd` 等 |
| trackingNumber | 运单号 |

支持顺丰(SF)和京东物流接口。

### GET `/api/logistics/delivery-list` — 微信物流公司列表

### POST `/api/logistics/waybill-token` — 微信运单 Token

---

## 1.18 DiningTable 桌台 (`/api/dining-table`)

### GET `/api/dining-table/list` — 桌台列表（不含停用）

| 参数 | 说明 |
|---|---|
| pageNum | 默认 1 |
| pageSize | 默认 10 |
| keyword | 桌号搜索 |

### POST `/api/dining-table/create` — 新增桌台

### POST `/api/dining-table/delete` — 软删除（改为停用状态）

---

## 1.19 Staff 员工核销 (`/api/staff`)

需登录。

### GET `/api/staff/today-stats` — 今日核销统计

### POST `/api/staff/verify` — 核销

| 参数 | 说明 |
|---|---|
| code | 核销码/二维码内容 |

自动识别商品自取(PK_)、活动券(ACT_)、积分兑换(EXC_)。

### GET `/api/staff/vouchers` — 券列表

| 参数 | 说明 |
|---|---|
| page | 默认 1 |
| status | `used`/`unused`/`expired` |

### GET `/api/staff/verify-history` — 核销记录

---

## 1.20 StaffVerify 核销验证 (`/api/staff-verify`)

需登录。

### GET `/api/staff-verify/permission` — 校验员工权限

### GET `/api/staff-verify/voucher-info` — 查询券信息（不核销）

| 参数 | 说明 |
|---|---|
| code | 核销码 |

### POST `/api/staff-verify/verify` — 核销

| 参数 | 说明 |
|---|---|
| code | 核销码 |
| quantity | 核销数量 |

### GET `/api/staff-verify/history` — 核销历史

---

## 1.21 Kitchen 后厨 (`/api/Kitchen`)

### POST `/api/Kitchen/login` — 后厨登录

| 参数 | 说明 |
|---|---|
| phone | 手机号 |
| password | 密码 |

### GET `/api/Kitchen/orders` — 今日订单列表

| 参数 | 说明 |
|---|---|
| type | 2=待出餐, 3=已完成 |

### GET `/api/Kitchen/detail/{orderId}` — 订单详情

### POST `/api/Kitchen/finish` — 标记菜品已出餐

| 参数 | 说明 |
|---|---|
| dishOrderDetailsId | 菜品明细 ID |

### POST `/api/Kitchen/cancel-dish` — 取消出餐

### GET `/api/Kitchen/statistics` — 今日统计

### POST `/api/Kitchen/logout` — 退出登录

---

# 二、管理后台接口

## 2.1 Activity Manage 活动管理 (`/api/activity-manage`)

支持 `multipart/form-data` 和 `application/json`。

### GET `/api/activity-manage/statuses` — 活动状态列表

### GET `/api/activity-manage/types` — 活动类型列表

### GET `/api/activity-manage/list` — 活动列表

| 参数 | 说明 |
|---|---|
| pageNum | 默认 1 |
| pageSize | 默认 15 |
| keyword | 标题搜索 |

### GET `/api/activity-manage/detail` — 活动详情

| 参数 | 说明 |
|---|---|
| id | 活动 ID |

### POST `/api/activity-manage/add` — 新增活动

| 参数 | 说明 |
|---|---|
| name | 活动名称 |
| type | 活动类型 |
| price | 价格 |
| status | 状态 |
| image | 封面图（文件或 URL） |
| description | 描述 |
| location | 地点 |
| people | 人数限制 |
| content | 内容 |
| duration | 时长 |
| startDate / endDate | 活动日期 |
| carouselMedia | 轮播媒体 |
| specImages | 规格图 |

### PUT/POST `/api/activity-manage/edit` — 编辑活动

### POST `/api/activity-manage/delete` — 删除

### POST `/api/activity-manage/deleteBatch` — 批量删除

---

## 2.2 Back User 管理员用户 (`/api/back-user`)

### POST `/api/back-user/login` — 管理员登录

| 参数 | 说明 |
|---|---|
| user_no | 管理员账号 |
| user_password | 密码 |

### GET `/api/back-user/list` — 用户列表

| 参数 | 说明 |
|---|---|
| pageNum | 默认 1 |
| pageSize | 默认 10 |
| keyword | 搜索关键词 |

### POST `/api/back-user/add` — 新增用户

| 参数 | 说明 |
|---|---|
| phone | 手机号 |
| password | 密码（默认 33668899aA@） |
| nickname | 昵称 |
| realName | 真实姓名 |
| gender | 性别 |
| roleId | 角色 ID |

### POST `/api/back-user/edit` — 编辑用户

### GET `/api/back-user/detail` — 用户详情

### POST `/api/back-user/delete` — 删除用户

### GET `/api/back-user/roles` — 角色列表

### POST `/api/back-user/logout` — 退出登录

---

## 2.3 Product 商品管理 (`/api/product`)

### GET `/api/product/statuses` — 商品状态列表

### GET `/api/product/categories` — 分类列表

### GET `/api/product/units` — 单位列表

### POST `/api/product/add-unit` — 新增单位

### PUT `/api/product/update-unit` — 编辑单位

### DELETE `/api/product/delete-unit` — 删除单位

### GET `/api/product/list` — 商品列表

| 参数 | 说明 |
|---|---|
| pageNum | 默认 1 |
| pageSize | 默认 10 |
| keyword | 搜索 |

### GET `/api/product/detail/{id}` — 商品详情

### POST `/api/product/add` — 新增商品（支持 multipart/form-data）

### PUT `/api/product/edit` / POST edit — 编辑商品

### POST `/api/product/delete` — 删除

### POST `/api/product/deleteBatch` — 批量删除

### GET `/api/product/stats` — 商品统计数据

### GET `/api/product/weight-tags` — 重量标签选项

### GET `/api/product/weight-tags` — 物流类型列表

---

## 2.4 Dish 菜品管理 (`/api/dish`)

### GET `/api/dish/statuses` — 菜品状态列表

### GET `/api/dish/categories` — 菜品分类列表

### GET `/api/dish/list` — 菜品列表

### GET `/api/dish/detail/{id}` — 菜品详情

### POST `/api/dish/add` — 新增菜品（支持 multipart/form-data）

### PUT/POST `/api/dish/edit` — 编辑菜品

### POST `/api/dish/delete` — 删除

### POST `/api/dish/deleteBatch` — 批量删除

---

## 2.5 Table 餐桌管理 (`/api/table`)

### GET `/api/table/list` — 餐桌列表（含全部状态）

| 参数 | 说明 |
|---|---|
| pageNum | 默认 1 |
| pageSize | 默认 10 |
| keyword | 桌号搜索 |
| status | 状态筛选（停用桌台也可查） |

### GET `/api/table/detail` — 餐桌详情

### POST `/api/table/add` — 新增餐桌（含二维码生成）

### POST `/api/table/edit` — 编辑餐桌（桌号变更时重新生成二维码）

### POST `/api/table/delete` — 删除（改为停用状态）

### POST `/api/table/updateStatus` — 更新餐桌状态

### POST `/api/table/regenerate-qrcode` — 重新生成所有二维码

---

## 2.6 Activity Order 活动订单管理 (`/api/activity-order`)

### GET `/api/activity-order/statuses` — 订单状态列表

### GET `/api/activity-order/list` — 订单列表

| 参数 | 说明 |
|---|---|
| pageNum | 默认 1 |
| pageSize | 默认 10 |
| keyword | 订单号/活动名搜索 |
| statusId | 状态筛选 |

### GET `/api/activity-order/detail` — 订单详情

### POST `/api/activity-order/verify` — 核销

### POST `/api/activity-order/verify-by-order` — 按订单核销

### POST `/api/activity-order/refund` — 退款（调用微信退款）

### POST `/api/activity-order/reject-refund` — 驳回退款

---

## 2.7 Product Order 商品订单管理 (`/api/product/order`)

### GET `/api/product/order/statuses` — 订单状态列表

### GET `/api/product/order/list` — 订单列表

| 参数 | 说明 |
|---|---|
| pageNum | 默认 1 |
| pageSize | 默认 15 |
| keyword | 搜索 |
| statusId | 状态筛选 |
| logisticsType | 物流类型筛选 |

### GET `/api/product/order/detail` — 订单详情

### POST `/api/product/order/updateStatus` — 更新状态

| 参数 | 说明 |
|---|---|
| orderNo | 订单号 |
| action | `cancel-pending-payment`/`cancel-pending-shipment`/`ship`/`refund-request`/`refund-process`/`refund-reject`/`subscription-complete` |
| logisticsType | 物流类型（发货必填） |
| logisticsNo | 运单号（发货必填） |

### POST `/api/product/order/refund` — 退款

---

## 2.8 Dish Order 菜品订单管理 (`/api/dish/order`)

### GET `/api/dish/order/statuses` — 订单状态列表

### GET `/api/dish/order/list` — 订单列表

| 参数 | 说明 |
|---|---|
| pageNum | 默认 1 |
| pageSize | 默认 15 |
| keyword | 搜索 |
| statusId | 状态筛选 |

### GET `/api/dish/order/detail` — 订单详情

### POST `/api/dish/order/updateStatus` — 更新状态

| 参数 | 说明 |
|---|---|
| orderNo | 订单号 |
| action | `cancel`/`complete`/`refund-reject` |

### POST `/api/dish/order/refund-request` — 申请退款

### POST `/api/dish/order/refund-process` — 确认退款

### POST `/api/dish/order/refund-reject` — 驳回退款

---

## 2.9 Points Manage 积分管理 (`/api/back-points`)

### GET `/api/back-points/goods-statuses` — 积分商品状态列表

### GET `/api/back-points/order-statuses` — 兑换订单状态列表

### GET `/api/back-points/goods-list` — 积分商品列表

### GET `/api/back-points/goods-detail/{id}` — 积分商品详情

### POST `/api/back-points/goods-add` — 新增积分商品

### PUT/POST `/api/back-points/goods-edit` — 编辑积分商品

### POST `/api/back-points/goods-delete` — 删除积分商品

### GET `/api/back-points/order-list` — 兑换订单列表

### GET `/api/back-points/order-detail` — 兑换订单详情

### POST `/api/back-points/verify` — 核销兑换订单

### POST `/api/back-points/cancel` — 取消兑换（积分退回+库存恢复）

### GET `/api/back-points/rules` — 积分规则列表

### POST `/api/back-points/rule-add` — 新增积分规则

### PUT/POST `/api/back-points/rule-edit` — 编辑积分规则

### POST `/api/back-points/rule-delete` — 删除积分规则

---

## 2.10 File 文件服务 (`/api/file`)

### GET `/api/file/image/{fileName}` — 获取图片

### GET `/api/file/video/{fileName}` — 获取视频

### POST `/api/file/upload` — 上传文件

**Content-Type:** `multipart/form-data`

| 参数 | 类型 | 说明 |
|---|---|---|
| file | File | 图片文件，最大 5MB |
| path | string | 保存路径（可选） |

---

# 附录：通用说明

## 认证方式

- **用户端**：JWT Token，通过 `Authorization: Bearer <token>` 请求头传递
- **管理后台**部分接口：Token 认证（`api/activity-order`/`api/product/order`）
- **后厨**：独立登录，返回 JWT Token

## 分页参数约定

| 参数 | 说明 |
|---|---|
| page / pageNum | 页码，默认 1 |
| pageSize | 每页条数 |

分页响应通用字段：`total`, `page`, `pageSize`, `pages`

## 响应格式

**成功：** `{ code: 200, message: "success", data: { ... } }`

**失败：** `{ code: 400/404/500, message: "错误描述", data: null }`

**BusinessException：** 业务异常返回 `{ code: 业务码, message: "错误描述" }`

## 订单状态映射（数据库驱动）

所有订单状态（商品/菜品/活动）均从数据库表动态读取，不硬编码。

| 类型 | 状态表 | 状态 ID 示例 |
|---|---|---|
| 商品订单 | `commodity_order_status` | 1=待付款, 2=待发货, 3=运输中, 4=已完成, 5=已取消, 6=退款中, 7=已退款, 8=待核销, 9=已核销 |
| 菜品订单 | `dish_order_status` | 1=待付款, 2=待出餐, 3=已完成, 4=已取消 |
| 活动订单 | `activity_order_status` | 1=待付款, 2=待核销, 3=已核销, 4=已取消, 5=退款中, 6=已退款 |

## 媒体素材 material_type 规范

| type | 含义 | 适用 |
|---|---|---|
| 0 | 轮播图 | 商品/菜品/活动 |
| 1 | 详情图/规格图 | 商品/菜品/活动 |
| 2 | 视频/主页图片 | 商品/活动 |
| 3 | 规格图 | 活动专用 |
