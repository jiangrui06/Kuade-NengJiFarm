# 积分发放与回退 API 接口文档

> **版本：** v1.0（2026-06-02）
> **说明：** 积分发放与回退是后台自动逻辑，嵌入现有订单完成/退款流程中，无需前端额外调用。

---

## 积分规则

积分计算公式：`points = floor(amount / unit_amount) * unit_points`

从 `points_rule` 表读取 `is_active = true` 的规则（按 `id` 降序取第一条）。

| 字段 | 说明 | 示例 |
|------|------|------|
| unit_amount | 每 unit_amount 元 | 10 |
| unit_points | 获得 unit_points 积分 | 1 |
| 计算公式 | `floor(金额 / unit_amount) × unit_points` | 消费 25 元 → `floor(25/10)×1 = 2 积分` |

> 无活跃积分规则或计算值为 0 时，跳过积分操作（不会报错）。

---

## 积分发放场景

| 场景 | 触发点 | 文件 | 说明 |
|------|--------|------|------|
| 厨房出餐完成 | `KitchenService.MarkDishFinishAsync` | `Services/KitchenService.cs` | 最后一道菜出餐，订单自动完成时发放 |
| 产品订单核销 | `ProductOrderService.UpdateOrderStatusAsync` action=verify | `Services/ProductOrderService.cs` | 后台手动核销时发放 |
| 产品订单完成 | `ProductOrderService.UpdateOrderStatusAsync` action=complete | `Services/ProductOrderService.cs` | 后台标记已送达时发放 |
| 活动券后台核销 | `ActivityOrderService.VerifyOrderDetailAsync` | `Services/ActivityOrderService.cs` | 后台核销活动券时发放 |
| 员工核销商品自取 | `StaffVerifyController.VerifyCommodityPickupAsync` | `Controllers/StaffVerifyController.cs` | 员工扫码核销时发放 |
| 员工核销活动券 | `StaffVerifyController.VerifyActivityVoucherAsync` | `Controllers/StaffVerifyController.cs` | 员工扫码核销时发放 |

### 积分流水记录

所有积分发放会写入 `points_record` 表：

| 字段 | 值 |
|------|----|
| type | `"earn"` |
| points | 计算所得积分值 |
| description | `"消费获得积分"` |
| order_no | 对应订单号 |

---

## 积分回退场景（退款时自动扣回）

| 场景 | 触发点 | 文件 |
|------|--------|------|
| 商品订单退款 | `ProductOrderService.RefundAsync` | `Services/ProductOrderService.cs` |
| 活动券订单退款 | `ActivityOrderService.RefundAsync` | `Services/ActivityOrderService.cs` |
| 菜品订单一键退款 | `DishOrderService.RefundAsync` | `Services/DishOrderService.cs` |
| 菜品订单确认退款 | `DishOrderService.RefundProcessAsync` | `Services/DishOrderService.cs` |

### 积分流水记录

| 字段 | 值 |
|------|----|
| type | `"spend"` |
| points | 与发放时相同公式计算 |
| description | `"商品订单退款"` / `"活动券订单退款"` / `"菜品订单退款"` |
| order_no | 对应订单号 |

> 积分扣回失败不会影响退款流程，仅记录警告日志。

---

## 数据库表结构

### user（积分余额）

```sql
ALTER TABLE `user` ADD COLUMN `points` bigint DEFAULT 0;
```

### points_record（积分流水）

```sql
CREATE TABLE `points_record` (
  `id` bigint PRIMARY KEY AUTO_INCREMENT,
  `user_id` int NOT NULL,
  `type` varchar(16) NOT NULL COMMENT 'earn=收入, spend=支出',
  `points` int NOT NULL,
  `description` varchar(255) DEFAULT NULL,
  `order_no` varchar(64) DEFAULT NULL,
  `create_time` datetime NOT NULL
);
```

### points_rule（积分规则）

```sql
CREATE TABLE `points_rule` (
  `id` int PRIMARY KEY AUTO_INCREMENT,
  `rule_name` varchar(100) DEFAULT NULL,
  `unit_amount` decimal(10,2) NOT NULL,
  `unit_points` int NOT NULL,
  `description` varchar(500) DEFAULT NULL,
  `is_active` tinyint DEFAULT 0,
  `created_at` datetime DEFAULT CURRENT_TIMESTAMP
);
```

---

## 验证方法

1. 确认 `points_rule` 表中有 `is_active = 1` 的规则（如 `unit_amount=10, unit_points=1`）
2. 完成一个厨房订单 → 查看 `points_record` 是否多一条 `type=earn` 记录
3. 退款该订单 → 查看 `points_record` 是否多一条 `type=spend` 记录
4. 检查 `user.points` 余额是否正确增减
