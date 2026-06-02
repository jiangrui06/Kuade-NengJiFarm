# 后厨出餐状态同步 API 接口文档

> **版本：** v2.0（2026-06-02）
> **鉴权：** Bearer JWT Token（需先调用 `POST /api/kitchen/login` 获取）

---

## 调用流程

```
1. 后厨登录获取 Token
   POST /api/kitchen/login
   → 得到 token 和 user_id

2. 查看今日待出餐订单
   GET /api/kitchen/order/list?type=2
   Authorization: Bearer <token>
   → 得到订单列表及菜品明细 ID (dishOrderDetailsId)

3. 逐道出餐
   POST /api/kitchen/dish/finish
   Authorization: Bearer <token>
   Body: { "dishOrderDetailsId": 123 }
   → 最后一道菜出完自动完成订单

4. （可选）查看今日统计
   GET /api/kitchen/today-statistics
   Authorization: Bearer <token>
```

---

## 接口一：厨房登录

### 请求

```
POST /api/kitchen/login
Content-Type: application/json
```

```json
{
  "phoneNumber": "13800138000",
  "password": "12345678aA@"
}
```

### 成功响应

```json
{
  "code": 200,
  "message": "success",
  "data": {
    "token": "eyJhbGciOiJIUzI1NiIs...",
    "user_id": 1,
    "user_name": "后厨人员",
    "phone_number": "13800138000"
  }
}
```

> `token` 有效期为 7 天，可缓存重复使用。

---

## 接口二：逐道出餐（核心）

### 基本信息

```
POST /api/kitchen/dish/finish
Content-Type: application/json
Authorization: Bearer <token>
```

### 请求参数

| 字段 | 类型 | 必填 | 说明 |
|------|------|:----:|------|
| dishOrderDetailsId | long | 是 | 菜品明细 ID（从订单列表或详情获取） |

```json
{
  "dishOrderDetailsId": 10086
}
```

### 业务逻辑

1. 根据 `dishOrderDetailsId` 查找菜品明细
2. **检查父订单状态** — 若订单为"已取消"、"已完成"、"退款中"，拦截并返回具体状态名
3. 检查菜品是否已被处理（幂等），已处理则返回错误
4. 将菜品状态设为"已出餐"
5. 检查该订单下是否还有待出餐菜品
6. **无待出餐菜品** → 自动将订单状态设为"已完成"

### 成功响应

```json
{
  "code": 200,
  "message": "success",
  "data": {
    "allFinished": true
  }
}
```

### 错误响应

```json
// 菜品不存在
{ "code": 400, "message": "菜品明细不存在" }

// 菜品已被处理（重复操作）
{ "code": 200, "message": "产品出餐标记失败" }

// 订单已取消 / 已完成 / 退款中（拦截）
{ "code": 200, "message": "该订单状态为「已取消」，无法出餐" }
{ "code": 200, "message": "该订单状态为「已完成」，无法出餐" }
{ "code": 200, "message": "该订单状态为「退款中」，无法出餐" }

// dishOrderDetailsId 为空
{ "code": 200, "message": "dishOrderDetailsId 不能为空" }

// Token 无效或过期
{ "code": 401, "message": "未授权" }
```

---

## 接口三：获取待出餐 / 已完成订单列表

### 基本信息

```
GET /api/kitchen/order/list?type=2
Authorization: Bearer <token>
```

### 请求参数

| 字段 | 类型 | 必填 | 说明 |
|------|------|:----:|------|
| type | int | 是 | 2=待出餐, 3=已完成 |

### 成功响应

```json
{
  "code": 0,
  "message": "success",
  "data": [
    {
      "id": 1,
      "no": "DISH20260602001",
      "time": "2026-06-02 10:30:00",
      "table": "A01",
      "total": 168.00,
      "items": [
        {
          "dishOrderDetailsId": 10086,
          "name": "红烧肉",
          "quantity": 2,
          "price": 68.00,
          "status": 1
        }
      ]
    }
  ]
}
```

> `items[].status`：1=待出餐, 2=已出餐, 3=已取消

---

## 接口四：获取订单详情

### 基本信息

```
GET /api/kitchen/order/detail?orderId=1
Authorization: Bearer <token>
```

### 成功响应

```json
{
  "code": 0,
  "message": "success",
  "data": {
    "orderId": 1,
    "orderNo": "DISH20260602001",
    "tableNumber": 1,
    "createTime": "2026-06-02T10:30:00",
    "totalAmount": 168.00,
    "remark": "少辣",
    "dishList": [
      {
        "dishOrderDetailsId": 10086,
        "name": "红烧肉",
        "quantity": 2,
        "status": 1,
        "price": 68.00
      }
    ]
  }
}
```

---

## 接口五：取消出餐

### 基本信息

```
POST /api/kitchen/dish/cancel
Content-Type: application/json
Authorization: Bearer <token>
```

### 请求参数

| 字段 | 类型 | 必填 | 说明 |
|------|------|:----:|------|
| dishOrderDetailsId | long | 是 | 菜品明细 ID |

```json
{
  "dishOrderDetailsId": 10086
}
```

### 业务逻辑

1. 将菜品状态设为"已取消"
2. 若该订单无待出餐菜品：
   - 有已出餐菜品 → 订单设为"已完成"
   - 无已出餐菜品 → 订单设为"已取消"

### 成功响应

```json
{
  "data": {
    "dishOrderDetailsId": 10086,
    "status": 3
  }
}
```

---

## 接口六：今日统计数据

### 基本信息

```
GET /api/kitchen/today-statistics
Authorization: Bearer <token>
```

### 成功响应

```json
{
  "code": 0,
  "message": "success",
  "data": {
    "todayTotalAmount": 1680.00,
    "todayTotalOrder": 12,
    "todayFinishedOrder": 8,
    "todayPendingDish": 5,
    "todayFinishedDish": 30
  }
}
```

---

## 接口汇总

| 接口 | 方法 | 鉴权 | 说明 |
|------|------|:----:|------|
| 厨房登录 | `POST /api/kitchen/login` | 无 | 获取 JWT Token |
| 逐道出餐 | `POST /api/kitchen/dish/finish` | Bearer Token | 逐道出餐，全部出完自动完成订单 |
| 取消出餐 | `POST /api/kitchen/dish/cancel` | Bearer Token | 取消某道菜品 |
| 订单列表 | `GET /api/kitchen/order/list` | Bearer Token | 查看待出餐/已完成订单 |
| 订单详情 | `GET /api/kitchen/order/detail` | Bearer Token | 查看具体订单及菜品 |
| 今日统计 | `GET /api/kitchen/today-statistics` | Bearer Token | 今日营业额、出餐数等 |
| 登出 | `POST /api/kitchen/logout` | Bearer Token | 退出登录 |
