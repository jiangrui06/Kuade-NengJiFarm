# DemoAPI 接口文档

更新时间：2026-04-28

## 通用约定

- 基础地址：`http://127.0.0.1:5000`
- 数据格式：除文件上传和支付回调外，请求体默认使用 `application/json`。
- 认证方式：需要登录的接口在请求头携带 `Authorization: Bearer <token>`。
- 通用响应：大多数业务接口返回 `ApiResult`。

```json
{
  "code": 200,
  "message": "success",
  "data": {}
}
```

> 说明：项目内存在一批兼容旧前端的别名接口，例如 `/api/order/getOrderData/list`、`/api/address/list`。本文按当前源码实际路由全部列出。

## 认证与登录 Auth

| 方法 | 地址 | 鉴权 | 用途 | 参数/请求体 | 返回 data |
| --- | --- | --- | --- | --- | --- |
| POST | `/api/Auth/wxlogin` | 否 | 微信登录，按 `code` 换取 openid 并创建/更新用户 | Body：`code` 必填，`nickname`、`avatar`、`encryptedData`、`iv` 可选 | `token`、`isNewUser`、`user_id`、`user_guid`、`register_time`、`openid`、`phone_number`、`role` |
| POST | `/api/Auth/wx-phone-login` | 否 | 微信手机号快捷登录，登录同时绑定手机号 | Body：`code` 必填，`phoneCode` 必填，`nickname`、`avatar` 可选 | `token`、`isNewUser`、`user_id`、`user_guid`、`register_time`、`openid`、`phone_number`、`purePhoneNumber`、`role` |
| GET | `/api/Auth/check` | 是 | 检查当前 token 登录状态 | 无 | `isLogin`、`isLoggedIn`、`user_id`、`user_guid`、`register_time`、`openid`、`phone_number` |
| POST | `/api/Auth/phone` | 是 | 当前登录用户绑定/更新微信手机号 | Body：`code` 必填，来自微信 `getPhoneNumber` | `user_id`、`user_guid`、`purePhoneNumber` |
| POST | `/api/GetPhone/phone_number` | 否 | 独立获取微信手机号 | Body：`code` 必填 | 微信手机号信息 |

## 用户 User

| 方法 | 地址 | 鉴权 | 用途 | 参数/请求体 | 返回 data |
| --- | --- | --- | --- | --- | --- |
| GET | `/api/user/profile-preview` | 否 | 未登录时展示游客资料 | 无 | `id`、`nickname`、`avatar`、`gender`、`phone`、`email` |
| GET | `/api/user/profile` | 是 | 获取当前用户资料 | 无 | `id`、`nickname`、`avatar`、`gender`、`phone`、`email`、`balance`、`reward`、`role` |
| PUT | `/api/user/profile` | 是 | 更新当前用户资料 | Body：`nickname`、`avatar`、`gender`、`phone`、`email` | 空对象 |
| GET | `/api/user/address` | 是 | 获取当前用户收货地址列表 | 无 | 地址数组 |
| GET | `/api/address/list` | 是 | 获取当前用户收货地址列表，兼容路径 | 无 | 地址数组 |
| GET | `/api/address/{id}` | 是 | 获取单个收货地址 | Path：`id` | 地址详情 |
| POST | `/api/user/address` | 是 | 新增收货地址 | Body：`name`、`phone`、`province`、`city`、`district`、`address`、`isDefault` | `id` |
| POST | `/api/address` | 是 | 新增收货地址，兼容路径 | Body 同上 | `id` |
| PUT | `/api/user/address` | 是 | 更新收货地址 | Body：`id`、`name`、`phone`、`province`、`city`、`district`、`address`、`isDefault` | 空对象 |
| PUT | `/api/user/address/{id}` | 是 | 更新收货地址，id 放在路径 | Path：`id`；Body 同上，可不传 `id` | 空对象 |
| PUT | `/api/address/{id}` | 是 | 更新收货地址，兼容路径 | Path：`id`；Body 同上，可不传 `id` | 空对象 |
| DELETE | `/api/user/address` | 是 | 删除收货地址 | Body：`id` | 空对象 |
| DELETE | `/api/user/address/{id}` | 是 | 删除收货地址，id 放在路径 | Path：`id` | 空对象 |
| DELETE | `/api/address/{id}` | 是 | 删除收货地址，兼容路径 | Path：`id` | 空对象 |

地址对象字段：`id`、`name`、`phone`、`province`、`city`、`district`、`address`、`isDefault`。

## 员工 Staff

| 方法 | 地址 | 鉴权 | 用途 | 参数/请求体 | 返回 data |
| --- | --- | --- | --- | --- | --- |
| GET | `/api/staff/today-stats` | 是，员工 | 员工首页今日核销统计 | 无 | `todayVerified`、`pendingCount`、`activityVerified`、`pickingVerified`、`today_verify_count`、`last_verify_time` |
| POST | `/api/staff/verify` | 是，员工 | 核销活动券/认购券 | Body：`code` 必填，支持订单号、二维码 URL、`ACT-`/`PICK-` 前缀码 | 核销结果、券类型、用户信息、核销时间 |
| GET | `/api/staff/vouchers` | 是，员工 | 查询待核销/已核销/过期券列表 | Query：`type` 可选 `activity`/`picking`/`pick`，`status` 可选 `unused`/`used`/`expired`/`all`，`page`，`pageSize` | `total`、`page`、`pageSize`、`list` |
| GET | `/api/staff/verify-history` | 是，员工 | 查询核销历史 | Query：`today` 默认 `true`，`page`，`pageSize`，`startDate`，`endDate` | `total`、`page`、`pageSize`、`list` |

## 首页 Home

| 方法 | 地址 | 鉴权 | 用途 | 参数/请求体 | 返回 data |
| --- | --- | --- | --- | --- | --- |
| GET | `/api/home` | 否 | 首页数据 | Query：`page` 默认 1，`pageSize` 默认 6 | `swiperList`、`functionButtons`、`farmGoods`、`hotDishes` 等 |
| GET | `/api/home/index` | 否 | 首页数据，兼容路径 | Query：`page`，`pageSize` | 同 `/api/home` |
| GET | `/api/home/video` | 否 | 首页视频列表 | 无 | `items` |

## 农场介绍 Farm

| 方法 | 地址 | 鉴权 | 用途 | 参数/请求体 | 返回 data |
| --- | --- | --- | --- | --- | --- |
| GET | `/api/farm/intro` | 否 | 获取农场介绍内容 | 无 | 农场介绍、媒体、联系方式等 |

## 农场商品 FarmGoods

| 方法 | 地址 | 鉴权 | 用途 | 参数/请求体 | 返回 data |
| --- | --- | --- | --- | --- | --- |
| GET | `/api/farm-goods` | 否 | 农场商品分页页数据 | Query：`category` 默认 `all`，`page` 默认 1，`pageSize` 默认 6 | `swiperList`、`categories`、`goodsList`、分页信息 |
| GET | `/api/farm-goods/index` | 否 | 农场商品首页数据 | 无 | `swiperList`、`categories`、`todayGoods`、`hotGoods` |
| GET | `/api/farm-goods/category` | 否 | 按分类获取商品 | Query：`categoryId`/`category`/`id`，`page`，`pageSize` | `goodsList`、`total`、`page`、`pageSize` |
| GET | `/api/farm-goods/categories` | 否 | 获取商品分类 | 无 | `categories` |
| GET | `/api/farm-goods/search` | 否 | 搜索农场商品 | Query：`keyword`，`page`，`pageSize` | `goodsList`、`total`、`page`、`pageSize` |

商品列表项常用字段：`id`、`name`、`image`、`price`、`originalPrice`、`stock`、`tags`。

## 商品 Goods

| 方法 | 地址 | 鉴权 | 用途 | 参数/请求体 | 返回 data |
| --- | --- | --- | --- | --- | --- |
| GET | `/api/goods/{id}` | 否 | 商品详情，id 放在路径 | Path：`id` | 商品详情 |
| GET | `/api/goods/detail` | 否 | 商品详情，id 放在查询参数 | Query：`goodsId` 或 `goods_id` | 商品详情 |
| GET | `/api/goods/search` | 否 | 商品搜索 | Query：`keyword` 必填，`page` 默认 1，`pageSize` 默认 20 | `goodsList`、`total`、`page`、`pageSize` |

商品详情常用字段：`id`、`name`、`price`、`image`、`detailImage`、`description`、`weight`、`storage`、`stock`、`tags`。

## 亩地认购 Acres

| 方法 | 地址 | 鉴权 | 用途 | 参数/请求体 | 返回 data |
| --- | --- | --- | --- | --- | --- |
| GET | `/api/acres/index` | 否 | 亩地认购首页数据 | 无 | `swiperList`、`items` |
| GET | `/api/acres` | 否 | 亩地项目列表 | Query：`status`，`pageIndex` 默认 1，`pageSize` 默认 10 | `pageIndex`、`pageSize`、`total`、`items` |
| GET | `/api/acres/{id}` | 否 | 亩地项目详情 | Path：`id` | 亩地详情、价格、状态、图片、说明等 |
| POST | `/api/acres/{id}/adopt` | 否 | 创建亩地认购订单/预约 | Path：`id`；Body 当前源码未固定字段 | `orderId`、`acreId`、`status`、`message` |
| GET | `/api/acres/{id}/logs` | 否 | 获取亩地种植日志 | Path：`id` | `logs`，每项含 `time`、`action` |

## 活动 Activity

| 方法 | 地址 | 鉴权 | 用途 | 参数/请求体 | 返回 data |
| --- | --- | --- | --- | --- | --- |
| GET | `/api/activity` | 否 | 活动列表 | 无 | 活动数组 |
| GET | `/api/activity/list` | 否 | 活动列表，按分类包装 | 无 | `activities` |
| GET | `/api/activity/detail` | 否 | 活动详情 | Query：`id` 必填 | 活动详情 |
| POST | `/api/activity/{id}/register` | 否 | 活动报名/创建活动券订单 | Path：`id` | `orderId`、`activityId`、`status`、`message` |

活动详情常用字段：`id`、`title`、`price`、`date`、`image`、`categoryName`、`description`、`location`、`people`、`content`、`participants`、`remainingSlots`、`images`。

## 购物车 Cart

| 方法 | 地址 | 鉴权 | 用途 | 参数/请求体 | 返回 data |
| --- | --- | --- | --- | --- | --- |
| GET | `/api/cart` | 是 | 获取购物车列表 | 无 | `cartList` |
| GET | `/api/cart/list` | 是 | 获取购物车列表，兼容路径 | 无 | `cartList` |
| POST | `/api/cart/items` | 是 | 同步购物车列表 | Body：`cartList`，每项含 `id`、`goodsId`/`goods_id`、`count` | `cartList` |
| POST | `/api/cart/sync` | 是 | 同步购物车列表，兼容路径 | Body 同上 | `cartList` |
| POST | `/api/cart/add` | 是 | 添加商品到购物车 | Body：`goodsId`/`goods_id`/`commodityId`，`count`/`quantity`/`num` | `cartList` 或新增项信息 |
| POST | `/api/cart/addToCart` | 是 | 添加商品到购物车，兼容路径 | Body 同上 | 同 `/api/cart/add` |
| PUT | `/api/cart/items/{id}` | 是 | 更新购物车项数量 | Path：`id`；Body：`count` | `cartList` |
| PUT | `/api/cart/update` | 是 | 更新购物车项数量，兼容路径 | Body：`cartId`、`count` | `cartList` |
| DELETE | `/api/cart/items/{id}` | 是 | 删除购物车项 | Path：`id` | `cartList` |
| DELETE | `/api/cart/delete` | 是 | 删除购物车项，兼容路径 | Body：`cartId` | `cartList` |
| DELETE | `/api/cart` | 是 | 清空购物车 | 无 | 空对象 |
| DELETE | `/api/cart/clear` | 是 | 清空购物车，兼容路径 | 无 | 空对象 |

购物车项字段：`id`、`goodsId`、`name`、`image`、`tag`、`price`、`count`、`checked`。

## 商城订单 Order

| 方法 | 地址 | 鉴权 | 用途 | 参数/请求体 | 返回 data |
| --- | --- | --- | --- | --- | --- |
| GET | `/api/order` | 否 | 点餐/商城商品页数据 | Query：`categoryId` 默认 `vegetables`，`page`，`pageSize` | `categories`、`currentCategory`、`goodsList`、分页信息 |
| GET | `/api/order/getOrderData` | 否 | 获取点餐/商城全量分类商品数据 | 无 | `data.data.categories`、`data.data.goodsList` |
| POST | `/api/order/updateGoodsQuantity` | 否 | 前端同步商品数量占位接口 | Body：`updates`，键为商品 id，值为数量 | `updated` |
| GET | `/api/order/status-list` | 是 | 获取订单状态筛选列表 | 无 | 状态数组 |
| POST | `/api/order/create` | 是 | 创建商品订单 | Body：`addressId`/`address_id`，`cartIds`/`cart_ids` 或 `goodsId`/`goods_id` + `count`/`quantity`，`totalPrice`，`remark` | `id`、`orderId`、`orderNumber`、`orderStatus`、`paymentStatus`、`amount`、`createTime` |
| POST | `/api/order/getOrderData/create` | 是 | 创建商品订单，兼容路径 | Body 同上 | 同 `/api/order/create` |
| POST | `/api/order/create-payment-order` | 是 | 创建待支付商品订单 | Body 同上 | 同 `/api/order/create` |
| GET | `/api/order/list` | 是 | 当前用户商品订单列表 | Query：`status` 可选 `all`/`pending`/`pending_payment`/`paid`/`shipping`/`completed`/`cancelled`，`page`，`pageSize` | `orders`、`total`、`page`、`pageSize`、`hasMore` |
| GET | `/api/order/getOrderData/list` | 是 | 当前用户商品订单列表，兼容路径 | Query 同上 | 同 `/api/order/list` |
| GET | `/api/order/info` | 是 | 商品订单详情 | Query：`orderId` | `totalAmount`、`order` |
| GET | `/api/order/detail` | 是 | 商品订单详情，兼容路径 | Query：`orderId` | 同 `/api/order/info` |
| GET | `/api/order/getOrderData/detail` | 是 | 商品订单详情，兼容路径 | Query：`orderId` | 同 `/api/order/info` |
| GET | `/api/order/getOrderData/{orderId}` | 是 | 商品订单详情，id 放在路径 | Path：`orderId` | 同 `/api/order/info` |
| PUT | `/api/order/cancel` | 是 | 取消商品订单 | Body：`orderId` | 空对象 |
| POST | `/api/order/{id}/cancel` | 是 | 取消商品订单，id 放在路径 | Path：`id`；Body 可选 `orderId` | 空对象 |
| POST | `/api/order/getOrderData/cancel/{id}` | 是 | 取消商品订单，兼容路径 | Path：`id` | 空对象 |
| POST | `/api/order/{id}/pay` | 是 | 标记订单支付 | Path：`id`；Body：`paymentMethod`、`payAmount` | `orderId`、`status`、`statusText` |
| POST | `/api/order/getOrderData/pay/{id}` | 是 | 标记订单支付，兼容路径 | Path：`id`；Body 同上 | 同 `/api/order/{id}/pay` |
| POST | `/api/order/{id}/confirm` | 是 | 确认收货 | Path：`id` | `orderId`、`status`、`statusText` |
| POST | `/api/order/getOrderData/confirm/{id}` | 是 | 确认收货，兼容路径 | Path：`id` | 同 `/api/order/{id}/confirm` |

## 综合订单 Orders

| 方法 | 地址 | 鉴权 | 用途 | 参数/请求体 | 返回 data |
| --- | --- | --- | --- | --- | --- |
| GET | `/api/orders` | 否 | 查询综合订单列表 | Query：`type`、`status`、`page`、`pageSize` | `orders`、`total`、`page`、`pageSize`、`hasMore` |
| GET | `/api/orders/counts` | 否 | 查询订单数量统计 | Query：`type` 可选 | 各状态数量 |
| GET | `/api/orders/{id}` | 否 | 查询综合订单详情 | Path：`id` | 订单详情、商品/餐品/券信息 |
| POST | `/api/orders/{id}/pay` | 否 | 支付综合订单 | Path：`id`；Body：`paymentMethod`、`amount` | 支付结果、订单状态 |
| POST | `/api/orders/{id}/mock-pay` | 否 | 模拟支付综合订单 | Path：`id`；Body 同上 | 支付结果、订单状态 |
| PUT | `/api/orders/{id}/status` | 否 | 更新综合订单状态 | Path：`id`；Body：`status` | 更新后的状态 |
| GET | `/api/orders/{id}/qrcode` | 否 | 获取活动券/认购券二维码 | Path：`id` | `qrcode`、`code`、`expireTime` 等 |
| DELETE | `/api/orders/{id}` | 否 | 删除综合订单 | Path：`id` | 空对象 |

综合订单 `type` 常用值：`cart` 商品订单，`food` 点餐订单，`acre` 认购券，`activity` 活动券。状态常用值：`pending`、`paid`、`shipping`、`completed`、`cancelled`。

## 订单详情 OrderDetails

| 方法 | 地址 | 鉴权 | 用途 | 参数/请求体 | 返回 data |
| --- | --- | --- | --- | --- | --- |
| GET | `/api/OrderDetails` | 否 | 查询订单详情列表 | Query：`type`、`status`、`page`、`pageSize` | `orders`、`total`、`page`、`pageSize` |
| GET | `/api/OrderDetails/{id}` | 否 | 查询单个订单详情 | Path：`id` | 订单详情 |
| POST | `/api/OrderDetails/create` | 否 | 创建综合订单 | Body：`userId`/`user_id`，`sourceType`/`source_type`，`sourceName`/`source_name`，`sourceId`/`source_id`，`quantity`，`tableNumber`/`table_number`，`totalPrice`/`total_price`，`remark`，`address`/`address_info`，`items`/`item_list` | `orderId`、`orderNo`、`status`、`totalPrice` 等 |
| POST | `/api/OrderDetails/{id}/pay` | 否 | 支付订单详情订单 | Path：`id`；Body：`paymentMethod`、`payAmount` | 支付结果 |
| POST | `/api/OrderDetails/{id}/cancel` | 否 | 取消订单详情订单 | Path：`id`；Body：`reason` 可选 | 取消结果 |
| POST | `/api/OrderDetails/{id}/confirm` | 否 | 确认订单完成 | Path：`id` | 确认结果 |

`address` 字段支持：`addressId`/`address_id`、`name`/`contact_name`、`phone`/`contact_phone`、`address`/`detail`。

`items` 字段支持：`id`/`item_id`、`name`/`item_name`、`price`/`unit_price`、`quantity`/`count`、`image`/`image_url`。

## 支付 Pay

| 方法 | 地址 | 鉴权 | 用途 | 参数/请求体 | 返回 data |
| --- | --- | --- | --- | --- | --- |
| GET | `/api/pay/methods` | 否 | 获取支付方式 | 无 | 支付方式数组 |
| POST | `/api/pay/jsapi` | 是 | 创建微信 JSAPI 支付参数 | Body：`orderId`，`description` 可选 | 微信支付参数 |
| POST | `/api/pay/initiate-payment` | 是 | 发起支付，兼容旧前端 | Body 同 `/api/pay/jsapi` | 微信支付参数 |
| GET | `/api/pay/status` | 是 | 查询支付状态 | Query：`orderId` | 订单支付状态 |
| POST | `/api/pay/query-payment-status` | 是 | 查询支付状态，兼容旧前端 | Body：`orderId` | 订单支付状态 |
| POST | `/api/pay/notify` | 否，微信回调 | 微信支付异步通知 | Body：微信支付 XML/通知内容 | XML：`SUCCESS` 或 `FAIL` |
| GET | `/api/pay/info` | 是 | 获取订单支付页信息 | Query：`orderId` | 订单金额、收货信息、支付状态等 |

## 物流 Logistics

| 方法 | 地址 | 鉴权 | 用途 | 参数/请求体 | 返回 data |
| --- | --- | --- | --- | --- | --- |
| GET | `/api/logistics/{orderId}` | 否 | 获取订单物流详情 | Path：`orderId` | 物流公司、单号、收货信息、轨迹等 |
| GET | `/api/logistics/{orderId}/trace` | 否 | 获取订单物流轨迹 | Path：`orderId` | 轨迹数组 |
| POST | `/api/logistics/track` | 否 | 按物流平台和单号查询轨迹 | Body：`platformType`、`waybillNo` | 物流轨迹 |

## 文件 File

| 方法 | 地址 | 鉴权 | 用途 | 参数/请求体 | 返回 |
| --- | --- | --- | --- | --- | --- |
| GET | `/api/file/images` | 否 | 获取内置图片列表 | 无 | 图片 URL 数组 |
| GET | `/api/file/image/{fileName}` | 否 | 获取内置图片文件 | Path：`fileName` | 图片二进制 |
| GET | `/api/file/videos` | 否 | 获取内置视频列表 | 无 | 视频 URL 数组 |
| GET | `/api/file/video/{fileName}` | 否 | 获取内置视频文件 | Path：`fileName` | 视频二进制 |
| POST | `/api/file/upload` | 否 | 上传文件 | `multipart/form-data`：`file` 必填，`path` 可选 | 文件 URL/路径信息 |
| POST | `/api/file/upload/avatar` | 否 | 上传头像 | `multipart/form-data`：`file` 必填 | 头像 URL/路径信息 |
| GET | `/api/file/uploads/{fileName}` | 否 | 获取上传目录文件 | Path：`fileName` | 文件二进制 |

## 状态码与错误

- 业务失败通常仍返回 HTTP 200，但 `ApiResult.code` 会使用业务错误码，例如 `400` 参数错误、`401` 登录无效、`403` 无权限、`404` 资源不存在、`409` 状态冲突。
- 前端判断成功时应以 `code === 200` 或项目封装的成功判断为准。

