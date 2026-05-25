# 能记农场 — 后台管理 API 接口文档

> 生成日期：2026-05-21
> 基础地址：`http://<host>:<port>/api`
> 响应格式：统一 `{ code: 0/200, message: "success", data: ... }`（除 BackUser 部分接口使用 `{ Code: 200, Message: "...", Data: ... }` 外）

---

## 目录

1. [活动管理（Activity Manage）](#1-活动管理activity-manage)
2. [管理员用户管理（Back User）](#2-管理员用户管理back-user)
3. [商品管理（Product）](#3-商品管理product)
4. [菜品管理（Dish）](#4-菜品管理dish)
5. [餐桌管理（Dining Table / Table）](#5-餐桌管理dining-table--table)
6. [订单管理（Activity Order）](#6-活动订单管理activity-order)
7. [商品订单管理（Product Order）](#7-商品订单管理product-order)
8. [菜品订单管理（Dish Order）](#8-菜品订单管理dish-order)
9. [通用接口（Common）](#9-通用接口common)
10. [文件服务（File）](#10-文件服务file)
11. [员工核销（Staff）](#11-员工核销staff)
12. [厨房管理（Kitchen）](#12-厨房管理kitchen)
13. [管理员角色（Admin Roles）](#13-管理员角色admin-roles)

---

## 1. 活动管理（Activity Manage）

**Controller:** `ActivityManageController`
**路由前缀：** `[Route("api/activity-manage")]`
**请求 Content-Type：** 支持 `multipart/form-data` 和 `application/json`

### 1.1 活动列表

```
GET /api/activity-manage/list
```

**Query 参数：**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| pageNum | int | 否 | 页码，默认 1 |
| pageSize | int | 否 | 每页条数，默认 10 |

**响应 data：**
```json
{
  "records": [ /* 活动数组 */ ],
  "total": 10,
  "pages": 1,
  "pageNum": 1
}
```

### 1.2 活动详情

```
GET /api/activity-manage/detail
```

**Query 参数：**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| id | int | 是 | 活动 ID |

### 1.3 新增活动

```
POST /api/activity-manage/add
```

支持 `application/json` 和 `multipart/form-data` 两种方式。

**JSON Body 参数：**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| typeId | int | 是 | 活动类型 ID |
| title | string | 是 | 活动标题 |
| price | decimal | 是 | 价格 |
| description | string | 否 | 活动描述 |
| imageUrl | string | 否 | 图片 URL |

**multipart/form-data 字段：**

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| typeId | string | 是 | 活动类型 ID |
| title | string | 是 | 活动标题 |
| price | string | 是 | 价格 |
| description | string | 否 | 活动描述 |
| file | file | 否 | 上传图片文件 |

### 1.4 编辑活动

```
POST /api/activity-manage/edit
```

支持 `application/json` 和 `multipart/form-data`。

**参数：** 同新增活动，额外包含 `id`（活动 ID）

### 1.5 删除活动

```
POST /api/activity-manage/delete
```

**Body 参数：**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| id | int | 是 | 活动 ID |

### 1.6 批量删除

```
POST /api/activity-manage/deleteBatch
```

**Body 参数：**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| ids | int[] | 是 | 活动 ID 数组 |

---

## 2. 管理员用户管理（Back User）

**Controller:** `BackUserController`
**路由前缀：** `[Route("api/back-user")]`
**响应格式：** `{ Code: 200, Message: "success", Data: ... }`（大写驼峰）

### 2.1 管理员登录

```
POST /api/back-user/login
```

**Body 参数：**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| userName | string | 是 | 用户名 |
| password | string | 是 | 密码 |
| code | string | 否 | 验证码 |
| uuid | string | 否 | 验证码 UUID |

**响应 data：** 包含 Token 和用户信息

### 2.2 获取用户列表

```
GET /api/back-user/list
```

**Query 参数：**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| pageNum | int | 否 | 页码，默认 1 |
| pageSize | int | 否 | 每页条数，默认 10 |
| userName | string | 否 | 搜索用户名 |
| status | string | 否 | 状态筛选 |

### 2.3 新增管理员

```
POST /api/back-user/add
```

**Body 参数：**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| userName | string | 是 | 用户名 |
| password | string | 是 | 密码 |
| nickName | string | 否 | 昵称 |
| phonenumber | string | 否 | 手机号 |
| email | string | 否 | 邮箱 |
| role | string | 否 | 角色 |
| status | string | 否 | 状态 |

### 2.4 编辑管理员

```
POST /api/back-user/edit
```

**Body 参数：** 同新增，额外包含 `userId`（用户 ID）

### 2.5 管理员详情

```
GET /api/back-user/detail
```

**Query 参数：**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| userId | int | 否 | 用户 ID（与 userGuid 二选一） |
| userGuid | string | 否 | 用户 GUID（与 userId 二选一） |

### 2.6 按 ID 获取详情

```
GET /api/back-user/{userId:int}
```

### 2.7 按 GUID 获取详情

```
GET /api/back-user/detail/guid
```

**Query 参数：**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| userGuid | string | 是 | 用户 GUID |

### 2.8 删除管理员

```
POST /api/back-user/delete
```

**Body 参数：**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| userId | int | 是 | 用户 ID |

### 2.9 退出登录

```
POST /api/back-user/logout
```

**Header：** `Authorization: Bearer <token>`

---

## 3. 商品管理（Product）

**Controller:** `ProductController`
**路由前缀：** `[Route("api/product")]`
**请求 Content-Type：** 支持 `multipart/form-data` 和 `application/json`

### 3.1 商品列表

```
GET /api/product/list
```

**Query 参数：**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| pageNum | int | 否 | 页码，默认 1 |
| pageSize | int | 否 | 每页条数，默认 10 |
| keyword | string | 否 | 搜索关键字 |
| status | string | 否 | 状态筛选 |
| categoryId | int | 否 | 分类 ID |

### 3.2 商品详情

```
GET /api/product/detail
```

**Query 参数：**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| id | int | 是 | 商品 ID |

### 3.3 新增商品

```
POST /api/product/add
```

支持 `application/json` 和 `multipart/form-data`。

**JSON 参数：**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| productName | string | 是 | 商品名称 |
| categoryId | int | 是 | 分类 ID |
| unitPrice | decimal | 是 | 单价 |
| originalPrice | decimal | 否 | 原价 |
| description | string | 否 | 商品描述 |
| imageUrl | string | 否 | 图片 URL |
| unitId | int | 否 | 单位 ID |
| inStock | int | 否 | 库存 |
| specDescription | string | 否 | 规格描述 |
| weightText | string | 否 | 重量文本 |
| productStatus | int | 否 | 状态：0=下架, 1=上架 |

**multipart/form-data：** 将上述参数作为字段传入，额外支持 `file`（图片文件）

### 3.4 编辑商品

```
POST /api/product/edit
```

**参数：** 同新增，额外包含 `id`（商品 ID）

### 3.5 删除商品

```
POST /api/product/delete
```

**Body 参数：**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| id | int | 是 | 商品 ID |

### 3.6 批量删除

```
POST /api/product/deleteBatch
```

**Body 参数：**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| ids | int[] | 是 | 商品 ID 数组 |

### 3.7 获取商品分类

```
GET /api/product/categories
```

### 3.8 获取商品单位

```
GET /api/product/units
```

### 3.9 获取商品统计

```
GET /api/product/stats
```

---

## 4. 菜品管理（Dish）

**Controller:** `DishController`
**路由前缀：** `[Route("api/dish")]`
**请求 Content-Type：** 支持 `multipart/form-data` 和 `application/json`

### 4.1 菜品列表

```
GET /api/dish/list
```

**Query 参数：**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| pageNum | int | 否 | 页码，默认 1 |
| pageSize | int | 否 | 每页条数，默认 10 |
| keyword | string | 否 | 搜索关键字 |
| status | string | 否 | 状态筛选 |
| categoryId | int | 否 | 分类 ID |

### 4.2 菜品详情

```
GET /api/dish/detail
```

**Query 参数：**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| id | int | 是 | 菜品 ID |

### 4.3 新增菜品

```
POST /api/dish/add
```

支持 `application/json` 和 `multipart/form-data`。

**参数：**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| dishName | string | 是 | 菜品名称 |
| categoryId | int | 是 | 分类 ID |
| dishPrice | decimal | 是 | 价格 |
| originalPrice | decimal | 否 | 原价 |
| description | string | 否 | 描述 |
| imageUrl | string | 否 | 图片 URL |
| status | int | 否 | 状态：0=下架, 1=上架 |
| recommend | int | 否 | 是否推荐：0=否, 1=是 |
| sales | int | 否 | 销量 |
| stock | int | 否 | 库存 |
| serveTime | string | 否 | 供应时间 |
| taste | string | 否 | 口味 |
| weightText | string | 否 | 重量文本 |
| specDescription | string | 否 | 规格描述 |

### 4.4 编辑菜品

```
POST /api/dish/edit
```

**参数：** 同新增，额外包含 `id`（菜品 ID）

### 4.5 删除菜品

```
POST /api/dish/delete
```

**Body 参数：**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| id | int | 是 | 菜品 ID |

### 4.6 批量删除

```
POST /api/dish/deleteBatch
```

**Body 参数：**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| ids | int[] | 是 | 菜品 ID 数组 |

---

## 5. 餐桌管理（Dining Table / Table）

有两组餐桌管理接口：

### 5.1 精简版餐桌管理

**Controller:** `DiningTableController`
**路由前缀：** `[Route("api/dining-table")]`

#### 5.1.1 餐桌列表

```
GET /api/dining-table/list
```

**Query 参数：**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| pageNum | int | 否 | 页码，默认 1 |
| pageSize | int | 否 | 每页条数，默认 10 |

#### 5.1.2 新增餐桌

```
POST /api/dining-table/add
```

**Body 参数：**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| tableNo | string | 是 | 餐桌号 |
| capacity | int | 是 | 容纳人数 |
| description | string | 否 | 描述 |
| qrcode | string | 否 | 二维码图片 URL |
| status | string | 否 | 状态 |

#### 5.1.3 删除餐桌

```
POST /api/dining-table/delete
```

**Body 参数：**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| id | int | 是 | 餐桌 ID |

### 5.2 完整版餐桌管理

**Controller:** `TableController`
**路由前缀：** `[Route("api/table")]`

#### 5.2.1 餐桌列表

```
GET /api/table/list
```

**Query 参数：**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| pageNum | int | 否 | 页码，默认 1 |
| pageSize | int | 否 | 每页条数，默认 10 |
| keyword | string | 否 | 搜索关键字（桌号） |
| status | string | 否 | 状态筛选 |

**响应格式：**
```json
{
  "code": 200,
  "message": "success",
  "data": {
    "records": [],
    "total": 0,
    "pages": 0,
    "pageNum": 1
  }
}
```

#### 5.2.2 餐桌详情

```
GET /api/table/detail/{id}
```

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| id | string | 是 | 餐桌 ID（路由参数） |

#### 5.2.3 新增餐桌

```
POST /api/table/add
```

**Body 参数：**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| tableno | string | 是 | 餐桌号 |
| capacity | int | 是 | 容纳人数（1-30） |
| description | string | 否 | 描述 |
| images | string[] | 否 | 图片 URL 数组 |
| qrcode | string | 否 | 二维码 |

#### 5.2.4 编辑餐桌

```
POST /api/table/edit
```

**Body 参数：**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| id | string | 是 | 餐桌 ID |
| tableno | string | 否 | 餐桌号 |
| capacity | int | 否 | 容纳人数 |
| description | string | 否 | 描述 |
| images | string[] | 否 | 图片 |
| qrcode | string | 否 | 二维码 |

#### 5.2.5 删除餐桌（停用）

```
POST /api/table/delete
```

**Body 参数：**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| id | string | 是 | 餐桌 ID |

#### 5.2.6 更新餐桌状态

```
POST /api/table/status
```

**Body 参数：**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| tableno | string | 是 | 餐桌号 |
| status | int | 是 | 状态：1=空闲, 2=使用中, 3=停用 |

---

## 6. 活动订单管理（Activity Order）

**Controller:** `ActivityOrderController`
**路由前缀：** `[Route("api/activity-order")]`

### 6.1 活动订单列表

```
GET /api/activity-order/list
```

**Query 参数：**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| page | int | 否 | 页码，默认 1 |
| pageSize | int | 否 | 每页条数，默认 10 |
| keyword | string | 否 | 搜索关键字 |
| status | string | 否 | 状态筛选 |
| startDate | string | 否 | 开始日期 |
| endDate | string | 否 | 结束日期 |

### 6.2 活动订单详情

```
GET /api/activity-order/detail
```

**Query 参数：**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| orderId | long | 是 | 订单 ID |

### 6.3 核销活动券

```
POST /api/activity-order/verify
```

**Body 参数：**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| orderId | long | 是 | 订单 ID |
| verifyCode | string | 是 | 核销码 |

### 6.4 退款

```
POST /api/activity-order/refund
```

**Header：** `Authorization: Bearer <token>`

**Body 参数：**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| orderId | long | 是 | 订单 ID |

---

## 7. 商品订单管理（Product Order）

**Controller:** `ProductOrderController`
**路由前缀：** `[Route("api/product/order")]`

### 7.1 商品订单列表

```
GET /api/product/order/list
```

**Query 参数：**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| page | int | 否 | 页码，默认 1 |
| pageSize | int | 否 | 每页条数，默认 10 |
| keyword | string | 否 | 搜索关键字 |
| status | string | 否 | 状态筛选 |

### 7.2 商品订单详情

```
GET /api/product/order/detail
```

**Query 参数：**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| orderId | long | 是 | 订单 ID |

### 7.3 更新订单状态

```
PUT /api/product/order/updateStatus
```

**Body 参数：**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| orderId | long | 是 | 订单 ID |
| status | int | 是 | 新状态 ID |

---

## 8. 菜品订单管理（Dish Order）

**Controller:** `DishOrderController`
**路由前缀：** `[Route("api/dish/order")]`

### 8.1 菜品订单列表

```
GET /api/dish/order/list
```

**Query 参数：**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| page | int | 否 | 页码，默认 1 |
| pageSize | int | 否 | 每页条数，默认 10 |
| keyword | string | 否 | 搜索关键字 |
| status | string | 否 | 状态筛选 |

### 8.2 菜品订单详情

```
GET /api/dish/order/detail
```

**Query 参数：**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| orderId | long | 是 | 订单 ID |

---

## 9. 通用接口（Common）

**Controller:** `CommonController`
**路由前缀：** `[Route("api/common")]`

### 9.1 文件上传

```
POST /api/common/upload
```

**Content-Type：** `multipart/form-data`

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| file | file | 是 | 上传的文件 |

**响应 data：** 文件访问路径字符串（直接返回字符串，非对象包裹）
```
"http://host/api/file/images/farm/filename.jpg"
```

---

## 10. 文件服务（File）

**Controller:** `FileController`
**路由前缀：** `[Route("api/file")]`

### 10.1 获取图片

```
GET /api/file/images/{filename}
```

返回图片文件。从以下目录依次查找：
- `wwwroot/images/farm/`
- `wwwroot/images/`
- `wwwroot/uploads/`
- `wwwroot/icons/`
- `wwwroot/farm/`

### 10.2 获取视频

```
GET /api/file/video/{filename}
```

### 10.3 文件上传

```
POST /api/file/upload
```

**Content-Type：** `multipart/form-data`

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| file | IFormFile | 是 | 上传文件 |

上传到 `wwwroot/uploads/` 目录，返回 URL。

---

## 11. 员工核销（Staff）

**Controller:** `StaffController`
**路由：** `[Route("api/staff")]`
**认证：** 需要 JWT Bearer Token，需具有 `staff` 或 `admin` 角色

### 11.1 今日统计

```
GET /api/staff/today-stats
```

返回当日核销统计数据。

### 11.2 核销活动券

```
POST /api/staff/verify
```

**Body 参数：**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| orderNo | string | 是 | 订单号 |
| verifyCode | string | 是 | 核销码 |

### 11.3 券列表

```
GET /api/staff/vouchers
```

**Query 参数：**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| keyword | string | 否 | 搜索订单号/手机号 |
| status | string | 否 | 状态筛选 |
| page | int | 否 | 页码 |
| pageSize | int | 否 | 每页条数 |

### 11.4 核销记录

```
GET /api/staff/verify-history
```

**Query 参数：**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| page | int | 否 | 页码 |
| pageSize | int | 否 | 每页条数 |

---

## 12. 厨房管理（Kitchen）

**Controller:** `KitchenController`
**路由前缀：** `[Route("api/kitchen")]`

### 12.1 厨房登录

```
POST /api/kitchen/login
```

**Body 参数：**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| username | string | 是 | 用户名 |
| password | string | 是 | 密码 |

### 12.2 厨房订单列表

```
GET /api/kitchen/order/list
```

**Query 参数：**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| status | string | 否 | 状态筛选 |
| page | int | 否 | 页码 |
| pageSize | int | 否 | 每页条数 |

### 12.3 厨房订单详情

```
GET /api/kitchen/order/detail
```

**Query 参数：**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| id | long | 是 | 订单 ID |

### 12.4 完成菜品

```
POST /api/kitchen/dish/finish
```

**Body 参数：**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| detailId | long | 是 | 订单明细 ID |

### 12.5 取消菜品

```
POST /api/kitchen/dish/cancel
```

**Body 参数：**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| detailId | long | 是 | 订单明细 ID |

### 12.6 今日统计

```
GET /api/kitchen/today-statistics
```

### 12.7 退出登录

```
POST /api/kitchen/logout
```

---

## 13. 管理员角色（Admin Roles）

**Controller:** `AdminApiControllers`
**路由：** `[Route("api/back-user/roles/list")]`

### 13.1 角色列表

```
GET /api/back-user/roles/list
```

返回角色列表数据。

---

## 附录

### 通用响应格式

大多数接口使用 `ApiResult`：
```json
{
  "code": 0,
  "message": "success",
  "data": { ... }
}
```

成功时 `code = 0`，失败时 `code = 错误码`。

BackUserController 使用 `ApiResponses<T>`：
```json
{
  "Code": 200,
  "Message": "success",
  "Data": { ... }
}
```

### 文件上传说明

1. 通过 `POST /api/common/upload` 上传文件
2. 返回的 data 是图片 URL 字符串，直接用于对应字段（如 `imageUrl`、`avatar`）
3. 管理端新增/编辑接口支持 `multipart/form-data`，可通过 `file` 字段同时上传图片

### 认证方式

- 管理端接口通过 `Authorization: Bearer <token>` 传递 JWT Token
- 登录接口 `POST /api/back-user/login` 获取 Token
- 核销接口需要具有 `staff` 或 `admin` 角色权限
