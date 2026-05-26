# 活动券列表汇总统计 API 接口文档

> 版本：v1.1
> 日期：2026-05-26
> 基础路径：`/api/activity-manage`
> Controller：`ActivityManageController`

---

## 背景

原前端实现在列表页顶部展示「活动券总数」、「累计已售」、「累计已核销」三个统计值时，仅基于当前页数据累加计算，多页时数据不准确。

**解决方案：** 后端在列表接口返回值中附带全量汇总统计字段，前端直接展示，不再自行累加。

---

## 1. 获取活动券列表（新增统计字段）

### 1.1 接口地址

- **URL：** `/api/activity-manage/list`
- **Method：** `GET`

### 1.2 请求参数（不变）

| 参数 | 类型 | 必填 | 默认值 | 说明 |
|------|------|------|--------|------|
| pageNum | int | 否 | 1 | 页码 |
| pageSize | int | 否 | 15 | 每页条数 |
| keyword | string | 否 | - | 搜索关键字（按活动券名称模糊匹配） |

### 1.3 响应结构（v1.1 新增字段）

```json
{
  "code": 200,
  "message": "操作成功",
  "data": {
    "records": [
      {
        "id": 1,
        "name": "春季采摘体验",
        "type": "采摘体验",
        "price": 39.90,
        "status": "已上架",
        "image": "/images/xxx.jpg",
        "people": 100,
        "duration": 120,
        "location": "农场A区",
        "startDate": "2026-05-01T00:00:00",
        "endDate": "2026-06-30T00:00:00",
        "carouselMedia": [],
        "createTime": "2026-04-27 13:00",
        "soldCount": 15,
        "verifiedCount": 8
      }
    ],
    "total": 20,
    "pageNum": 1,
    "pageSize": 15,
    "pages": 2,
    "totalActivityCount": 20,
    "totalSoldCount": 3560,
    "totalVerifiedCount": 2840
  }
}
```

### 1.4 新增字段说明

#### data 层汇总字段

| 字段名 | 类型 | 说明 | 计算逻辑 |
|--------|------|------|----------|
| `totalActivityCount` | int | 活动券总数（全量，同 `total`） | `SELECT COUNT(*) FROM activity WHERE isdelete_id = 0` |
| `totalSoldCount` | int | 累计已售（全量） | `SELECT SUM(d.quantity) FROM activity_order_detail d JOIN activity_orders o ON d.activity_order_id = o.order_id WHERE o.order_status_id IN (2,3)` |
| `totalVerifiedCount` | int | 累计已核销（全量） | `SELECT COUNT(*) FROM activity_verification_record` |

#### records[].items 新增字段

| 字段名 | 类型 | 说明 | 计算逻辑 |
|--------|------|------|----------|
| `soldCount` | int | 该活动券累计已售 | 按 `activity_id` 分组查询订单明细求和（同全量逻辑，仅限该活动） |
| `verifiedCount` | int | 该活动券累计已核销 | 按 `activity_id` 分组查询核销记录数（同全量逻辑，仅限该活动） |

### 1.5 统计口径说明

- **已售仅统计已支付订单**（`order_status_id = 2` 待核销 或 `= 3` 已核销），不含待付款、已取消、已退款的订单
- **已核销统计** = `activity_verification_record` 中的记录数（每张券核销一次记录一条）
- 两个统计值均为实时计算，非缓存数据

### 1.6 异常响应

```json
{
  "code": 500,
  "message": "获取失败",
  "data": null
}
```

---

## 2. 后端改动文件

| 文件 | 改动内容 |
|------|----------|
| `Dtos/ActivityListItemDto.cs` | 新增 `SoldCount`(int)、`VerifiedCount`(int) |
| `Services/IActivityService.cs` | 新增 `GetActivityTotalStatsAsync` 接口 |
| `Services/ActivityService.cs` | 列表查询中通过 ADO.NET 计算每活动已售/已核销；新增 `GetActivityTotalStatsAsync` 查全量汇总 |
| `Controllers/ActivityManageController.cs` | 响应中新增 `totalActivityCount`、`totalSoldCount`、`totalVerifiedCount` |

---

## 3. 兼容说明

- 原有分页响应结构完全不变（向后兼容）
- 旧版前端未使用新增字段时，不影响原有功能
- 不动数据库结构（数据从 `activity_order_detail`、`activity_verification_record` 实时计算）
