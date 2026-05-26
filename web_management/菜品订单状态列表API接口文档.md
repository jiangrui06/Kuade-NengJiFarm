# 菜品订单状态列表 API 接口文档

> 版本：v1.0
> 日期：2026-05-26
> 基础路径：`/api/dish/order`
> Controller：`DishOrderController`

---

## 接口说明

获取菜品订单的所有状态分类，供前端筛选下拉框或状态标签使用。

---

## 1. 获取菜品订单状态列表

### 1.1 请求地址

- **URL：** `/api/dish/order/statuses`
- **Method：** `GET`

### 1.2 请求参数

无

### 1.3 响应结构

```json
{
  "code": 200,
  "message": "操作成功",
  "data": [
    { "statusId": 1, "statusName": "待付款" },
    { "statusId": 2, "statusName": "待出餐" },
    { "statusId": 3, "statusName": "已完成" },
    { "statusId": 4, "statusName": "已取消" },
    { "statusId": 5, "statusName": "退款中" },
    { "statusId": 6, "statusName": "已退款" }
  ]
}
```

### 1.4 字段说明

| 字段名 | 类型 | 说明 |
|--------|------|------|
| `statusId` | int | 状态 ID（对应 `dish_orders.order_status_id`） |
| `statusName` | string | 状态名称 |

### 1.5 数据来源

数据库 `dish_order_status` 表，按 `order_status_id` 升序排列。

### 1.6 异常响应

```json
{
  "code": 500,
  "message": "获取菜品订单状态列表失败",
  "data": null
}
```

---

## 2. 后端改动文件

| 文件 | 改动 |
|------|------|
| `Controllers/DishOrderController.cs` | 新增 `GET /api/dish/order/statuses` 端点 |
