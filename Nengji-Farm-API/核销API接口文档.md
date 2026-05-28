# 核销 API 接口文档

## 基础信息

- **Base URL**: `http://192.168.101.50`
- **Content-Type**: `application/json`
- **认证**: Bearer Token（员工账号登录后获取）

---

## 核销码前缀说明

| 前缀 | 类型 | 示例 |
|------|------|------|
| `PK_` | 商品自取 | `PK_4EQLFVQRV4TQ` |
| `ACT_` | 活动券（亲子研学 / 采摘体验） | `ACT_QGUC7ZCQFVZM` |
| `EXC_` | 积分兑换 | `EXC_4EQLFVQRV4TQ` |

> 兼容旧数据：无前缀的核销码按 商品自取 → 活动券 顺序自动匹配。

---

## 1. 查询券信息（不核销）

```
POST /api/staff-verify/voucher-info
```

扫码后调用，查询券的详细信息，**不执行核销操作**。

### Request

```json
{
  "code": "PK_4EQLFVQRV4TQ"
}
```

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `code` | string | 是 | 核销码（含前缀） |

### 商品自取响应（type=goods_pickup）

```json
{
  "code": 0,
  "message": "success",
  "data": {
    "type": "goods_pickup",
    "typeName": "商品自取",
    "canVerify": true,
    "verified": false,
    "alreadyVerified": false,
    "userName": "张三",
    "userPhone": "13800138000",
    "content": "到店自取商品",
    "title": "商品自取",
    "orderNo": "COM20260528120000123",
    "participantCount": 3,
    "items": [
      { "name": "农场散养土鸡蛋", "image": "/api/file/image/egg.jpg", "quantity": 2, "price": 29.90 },
      { "name": "有机蔬菜礼盒", "image": "/api/file/image/veggies.jpg", "quantity": 1, "price": 68.00 }
    ]
  }
}
```

### 活动券响应（type=activity / pick）

```json
{
  "code": 0,
  "message": "success",
  "data": {
    "type": "pick",
    "typeName": "采摘体验",
    "canVerify": true,
    "verified": false,
    "alreadyVerified": false,
    "userName": "李四",
    "userPhone": "13900139000",
    "content": "夏日采摘节",
    "participantCount": 2,
    "orderNo": "ACT20260528120000123"
  }
}
```

### 积分兑换响应（type=exchange）

```json
{
  "code": 0,
  "message": "success",
  "data": {
    "type": "exchange",
    "typeName": "积分兑换",
    "canVerify": true,
    "verified": false,
    "alreadyVerified": false,
    "userName": "王五",
    "userPhone": "13700137000",
    "content": "积分商品名称",
    "title": "积分商品名称",
    "orderNo": "EXC20260528120000123",
    "participantCount": 1
  }
}
```

### 通用字段说明

| 字段 | 类型 | 说明 |
|------|------|------|
| `type` | string | 券类型：`goods_pickup` / `activity` / `pick` / `exchange` |
| `typeName` | string | 类型中文名 |
| `canVerify` | bool | 当前是否可核销 |
| `verified` | bool | 是否已核销 |
| `alreadyVerified` | bool | 同 verified |
| `userName` | string | 持券人姓名 |
| `userPhone` | string | 持券人手机号 |
| `content` | string | 券内容描述 |
| `title` | string | 券标题（仅商品自取/积分兑换） |
| `orderNo` | string | 订单号 |
| `participantCount` | int | 参与人数/数量 |
| `items` | array | 商品明细（**仅 type=goods_pickup**） |

### items 字段说明

| 字段 | 类型 | 说明 |
|------|------|------|
| `name` | string | 商品名称 |
| `image` | string | 商品图片（已标准化路径） |
| `quantity` | int | 购买数量 |
| `price` | float | 单价 |

> **image 来源**：`image` 字段来源于创建订单时写入 `commodity_order_detail.image_url` 的商品图片。
>
> | 下单入口 | image_url 情况 |
> |---|---|
> | `POST /api/commodity-order`（农场优选） | ✅ 从商品表取图 |
> | `POST /api/order`（桌台点餐） | ✅ 前端传入 |
> | `POST /api/OrderDetails/create`（通用订单） | ✅ 从商品表取图 |
>
> 三种下单方式均已写入商品图片，正常情况下 `image` 不会为空。如个别历史订单图片缺失，前端应做缺省图兜底。

---

## 2. 核销券

```
POST /api/staff-verify/voucher
```

执行核销操作。根据核销码前缀自动识别券类型并核销。

### Request

```json
{
  "code": "PK_4EQLFVQRV4TQ"
}
```

### 商品自取核销成功

```json
{
  "code": 0,
  "message": "核销成功",
  "data": {
    "verified": true,
    "alreadyVerified": false,
    "voucherType": "goods_pickup",
    "typeName": "商品自取",
    "userName": "张三",
    "userPhone": "13800138000",
    "content": "到店自取商品",
    "title": "商品自取",
    "orderNo": "COM20260528120000123",
    "verifyTime": "2026-05-28 14:30:00",
    "participantCount": 3,
    "items": [
      { "name": "农场散养土鸡蛋", "image": "/api/file/image/egg.jpg", "quantity": 2, "price": 29.90 }
    ],
    "message": "核销成功"
  }
}
```

### 活动券核销成功

```json
{
  "code": 0,
  "message": "核销成功",
  "data": {
    "verified": true,
    "alreadyVerified": false,
    "voucherType": "pick",
    "typeName": "采摘体验",
    "userName": "李四",
    "userPhone": "13900139000",
    "content": "夏日采摘节",
    "participantCount": 2,
    "verifyTime": "2026-05-28 14:30:00"
  }
}
```

### 积分兑换核销成功

```json
{
  "code": 0,
  "message": "核销成功",
  "data": {
    "orderNo": "EXC20260528120000123",
    "goodsName": "积分商品名称",
    "userName": "王五",
    "userPhone": "13700137000",
    "verifyTime": "2026-05-28 14:30:00",
    "operatorName": "员工姓名"
  }
}
```

### 已核销（重复扫码）

```json
{
  "code": 0,
  "message": "该订单已核销",
  "data": {
    "verified": true,
    "alreadyVerified": true,
    "voucherType": "goods_pickup",
    "typeName": "商品自取",
    "userName": "张三",
    "userPhone": "13800138000",
    "content": "到店自取商品",
    "title": "商品自取",
    "orderNo": "COM20260528120000123",
    "participantCount": 3,
    "items": [
      { "name": "农场散养土鸡蛋", "image": "/api/file/image/egg.jpg", "quantity": 2, "price": 29.90 }
    ],
    "message": "该订单已核销"
  }
}
```

---

## 3. 核销历史记录

```
GET /api/staff-verify/history
```

分页查询核销历史记录（跨类型：商品自取 + 活动券 + 积分兑换），按核销时间倒序排列。

### Query 参数

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `page` | int | 1 | 页码 |
| `pageSize` | int | 10 | 每页条数（最大 200） |
| `voucherType` | string | `"all"` | 筛选类型：`all` 全部 / `goods_pickup` 商品自取 / `parent_child_study` 亲子研学 / `pick_experience` 采摘体验 / `points_exchange` 积分兑换 |
| `keyword` | string | - | 搜索关键词（模糊匹配订单号、用户名、商品名） |
| `activityName` | string | - | 按活动名称筛选（仅活动类） |
| `startDate` | string | - | 开始日期（`yyyy-MM-dd`，含当天） |
| `endDate` | string | - | 结束日期（`yyyy-MM-dd`，含当天） |

### 响应

```json
{
  "code": 0,
  "data": {
    "list": [
      {
        "id": "gpu_1",
        "voucherType": "goods_pickup",
        "typeName": "商品自取",
        "categoryName": null,
        "orderNo": "COM20260528120000123",
        "goodsName": "农场散养土鸡蛋、有机蔬菜礼盒",
        "userName": "张三",
        "userPhone": "13800138000",
        "content": "到店自取商品",
        "description": "农场散养土鸡蛋",
        "participantCount": 0,
        "isPickupOrder": true,
        "deliveryMethod": "pickup",
        "verifyTime": "2026-05-28 14:30:00",
        "time": "2026-05-28 14:30:00",
        "createTime": "2026-05-28 12:00:00",
        "status": "verified",
        "orderId": "COM20260528120000123",
        "items": [
          { "name": "农场散养土鸡蛋", "image": "/api/file/image/egg.jpg", "quantity": 2, "price": 29.90 },
          { "name": "有机蔬菜礼盒", "image": "/api/file/image/veggies.jpg", "quantity": 1, "price": 68.00 }
        ]
      }
    ],
    "total": 50,
    "page": 1,
    "pageSize": 10
  }
}
```

### 各类型历史记录示例

#### 商品自取（voucherType=goods_pickup）

| 字段 | 说明 |
|------|------|
| `id` | 格式 `gpu_{CommodityVerifyRecord.Id}` |
| `voucherType` | `"goods_pickup"` |
| `typeName` | `"商品自取"` |
| `goodsName` | 商品名集合（顿号分隔） |
| `content` | `"到店自取商品"` |
| `isPickupOrder` | `true` |
| `deliveryMethod` | `"pickup"` |
| `items` | 商品明细数组 `[{name, image, quantity, price}]` |

#### 活动券（voucherType=parent_child_study / pick_experience）

| 字段 | 说明 |
|------|------|
| `id` | 格式 `pcs_{ActivityVerificationRecord.RecordId}` |
| `voucherType` | `"parent_child_study"` 或 `"pick_experience"` |
| `typeName` | `"亲子研学"` 或 `"采摘体验"` |
| `categoryName` | 同 typeName，活动类型名称 |
| `content` | 活动标题 |
| `description` | 活动描述 |
| `participantCount` | 参与人数 |
| `items` | 无（活动券无商品明细） |

#### 积分兑换（voucherType=points_exchange）

| 字段 | 说明 |
|------|------|
| `id` | 格式 `pex_{PointsExchange.Id}` |
| `voucherType` | `"points_exchange"` |
| `typeName` | `"积分兑换"` |
| `goodsName` | 积分商品名称 |
| `content` | `null` |
| `items` | 无 |

---

## 4. 验证员工权限

```
GET /api/staff-verify/permission
```

校验当前登录用户是否为员工角色。

### 响应

有权限：
```json
{
  "code": 0,
  "data": {
    "hasPermission": true,
    "role": "staff",
    "staffId": "1"
  }
}
```

无权限：
```json
{
  "code": 0,
  "data": {
    "hasPermission": false,
    "role": null,
    "staffId": null
  }
}
```

---

## 错误响应

| code | message | 说明 |
|------|---------|------|
| 400 | `请输入核销码` | 核销码为空 |
| 400 | `该兑换状态异常，无法核销` | 积分兑换状态异常 |
| 403 | `无权限访问` | 当前用户不是员工角色 |
| 403 | `该券未支付，无法核销` | 订单未支付 |
| 403 | `该券已取消，无法核销` | 订单已取消 |
| 403 | `该订单未支付，无法核销` | 自取订单未支付 |
| 403 | `该券已过期` | 活动券超过有效天数 |
| 403 | `该兑换已取消，无法核销` | 积分兑换已取消 |
| 404 | `未找到该券信息` | 核销码无效 |
| 409 | `该订单状态不支持核销` | 自取订单状态不是待核销 |
| 409 | `该兑换已核销，不能重复核销` | 积分重复核销 |

> 所有响应均返回 HTTP 200，业务状态通过 `code` 判断：
> - `code === 0` → 业务成功
> - `code !== 0` → 业务失败，`message` 为错误描述

---

## 前端对接注意

### 商品自取核销：直接使用 items

`POST /api/staff-verify/voucher-info`、`POST /api/staff-verify/voucher`、`GET /api/staff-verify/history` 三个接口中，**商品自取类型均包含 `items` 字段**。

前端应直接从接口响应中读取 `items`，**不要**再调用 `GET /api/orders/{orderNo}` 重新查询。原因：
- 核销接口的 `items` 已包含所有商品信息
- `GET /api/orders/{orderNo}` 只返回当前登录用户本人的订单，员工账号调用会返回空

### 前端字段映射

| 前端字段 | 接口字段 |
|---------|---------|
| `name` | `name` |
| `image` | `image` |
| `quantity` | `quantity` |
| `price` | `price` |
