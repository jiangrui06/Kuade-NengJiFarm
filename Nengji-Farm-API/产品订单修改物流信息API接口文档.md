# 产品订单修改物流信息 API 接口文档

> 版本：v1.0
> 日期：2026-06-01

---

## 背景

产品订单发货后进入"运输中"状态，管理员选错物流类型或填错物流单号时，提供修改入口。

---

## 1. 修改订单物流信息

| 项目 | 内容 |
|------|------|
| 接口名称 | 修改订单物流信息 |
| 请求路径 | `POST /api/product/order/update-logistics` |
| 请求方式 | `POST` |
| 鉴权 | 需要登录 Token（Bearer） |
| Content-Type | `application/json` |

### 请求参数

#### 请求头

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `Authorization` | string | 是 | `Bearer {token}` |

#### 请求体

```json
{
  "orderNo": "GOODS202605290001",
  "logisticsType": "顺丰",
  "logisticsNo": "SF1234567890"
}
```

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `orderNo` | string | 是 | 订单号 |
| `logisticsType` | string | 是 | 物流类型名称，从 `GET /api/product/logistics/types` 获取下拉选项 |
| `logisticsNo` | string | 否 | 物流单号，传空字符串 `""` 表示清空 |

### 响应格式

| 字段 | 类型 | 说明 |
|------|------|------|
| `code` | Number | `200` 表示成功 |
| `message` | String | 提示文案 |
| `data` | Object/Null | 更新后的物流信息 |

#### 成功响应

```json
{
  "code": 200,
  "message": "物流信息修改成功",
  "data": {
    "orderNo": "GOODS202605290001",
    "logisticsType": "顺丰",
    "logisticsNo": "SF1234567890"
  }
}
```

#### 失败响应

```json
{
  "code": 400,
  "message": "订单不存在"
}
```

### 状态码说明

| HTTP 状态码 | code | message | 说明 |
|------------|------|---------|------|
| 200 | 200 | 物流信息修改成功 | 修改成功 |
| 200 | 400 | 请求参数不能为空 | 请求体为空 |
| 200 | 400 | 订单号不能为空 | 缺少 orderNo |
| 200 | 400 | 物流类型不能为空 | 缺少 logisticsType |
| 200 | 400 | 订单不存在 | 未找到订单 |
| 200 | 400 | 当前订单状态不允许修改物流信息 | 订单状态不是"运输中" |
| 200 | 400 | 物流类型不存在 | logisticsType 不在 tracking_type 表中 |
| 401 | 401 | 登录已过期，请重新登录 | Token 过期或未登录 |
| 500 | 500 | 服务器异常 | 服务端错误 |

### 后端处理逻辑

1. **参数校验** — `orderNo` 不能为空，`logisticsType` 不能为空
2. **身份验证** — 从请求 Header 解析 Token，获取当前管理员信息
3. **订单检查** — 根据 `orderNo` 查询订单是否存在，校验订单状态是否为"运输中"
4. **物流类型解析** — 从 `tracking_type` 表查询 `logisticsType` 对应的 `tracking_type_id`
5. **更新物流信息** — 更新订单的 `tracking_type_id` 和 `tracking_number`，`logisticsNo` 传空字符串则清空
6. **返回成功**

---

## 2. 获取物流类型列表（已部署）

前端通过 `GET /api/product/logistics/types` 获取物流类型下拉选项，该接口已存在，无需重复开发。

```json
{
  "code": 200,
  "msg": "success",
  "data": ["顺丰", "邮政", "京东物流", "中通", "圆通", "韵达", "申通", "极兔", "德邦", "其他"]
}
```

---

## 数据库说明

| 表 | 操作 | 说明 |
|----|------|------|
| `commodity_orders` | 更新 | `tracking_type_id` 和 `tracking_number` 字段 |
| `tracking_type` | 查询 | 物流类型名称 → ID 映射（不做硬编码） |

无数据库结构变更（不建表、不改字段、不加索引）。
