# 农场优选模块 - API 需求文档

> **版本**：v1.0  
> **更新日期**：2026-04-29  
> **基础地址（BASE_URL）**：`http://192.168.203.56`

---

## 目录

1. [通用约定](#1-通用约定)
2. [农场优选商品列表](#2-农场优选商品列表)
3. [农场优选分类列表](#3-农场优选分类列表)
4. [认购专区说明](#4-认购专区说明)

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

### 1.2 图片地址处理

后端返回的图片路径若为相对路径，前端拼接 BASE_URL：`http://192.168.203.56` + `/path/to/image`

---

## 2. 农场优选商品列表

### 2.1 获取农场优选商品列表

```
GET /api/farm-goods
```

**查询参数**：

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `category` | string | 否 | 分类ID |
| `page` | number | 否 | 页码，默认 1 |
| `pageSize` | number | 否 | 每页数量，默认 10 |
| `type` | string | 否 | 类型，默认 'goods' |

**响应 `data`**：Array

| 字段 | 类型 | 说明 |
|------|------|------|
| `id` | number/string | 商品ID |
| `name` | string | 商品名称 |
| `price` | number | 商品价格（元） |
| `originalPrice` | number | 原价（元） |
| `image` | string | 商品图片路径 |
| `category` | string | 所属分类 |
| `categoryId` | string/number | 分类ID |
| `description` | string | 商品简介 |
| `stock` | number | 库存数量 |
| `unit` | string | 计量单位（如：斤、个） |
| `tags` | Array&lt;string&gt; | 商品标签数组 |

**示例响应**：
```json
{
  "code": 0,
  "message": "success",
  "data": [
    {
      "id": 1,
      "name": "有机西红柿",
      "price": 29.9,
      "originalPrice": 39.9,
      "image": "/images/tomato.jpg",
      "category": "vegetable",
      "categoryId": "vegetable",
      "description": "新鲜有机西红柿，现摘现发",
      "stock": 100,
      "unit": "斤",
      "tags": ["有机", "新鲜"]
    }
  ]
}
```

---

## 3. 农场优选分类列表

### 3.1 获取农场优选分类列表

```
GET /api/farm-goods/categories
```

**查询参数**：无

**响应 `data`**：Array

| 字段 | 类型 | 说明 |
|------|------|------|
| `id` | string/number | 分类ID |
| `name` | string | 分类名称 |
| `icon` | string | 分类图标路径（可选） |
| `color` | string | 分类图标背景色（可选） |
| `count` | number | 该分类商品数量（可选） |

**说明**：
- 前端会自动添加 `id: 'all', name: '全部商品'` 到列表开头
- 前端会自动添加 `id: 'acre', name: '认购专区'` 到列表末尾
- 后端返回的分类不需要包含这两项

**示例响应**：
```json
{
  "code": 0,
  "message": "success",
  "data": [
    { "id": "vegetable", "name": "新鲜蔬菜", "icon": "🥬", "color": "#4CAF50", "count": 20 },
    { "id": "fruit", "name": "时令水果", "icon": "🍎", "color": "#FF5722", "count": 15 },
    { "id": "meat", "name": "禽畜肉蛋", "icon": "🥩", "color": "#795548", "count": 10 },
    { "id": "dry", "name": "干货特产", "icon": "🌰", "color": "#FF9800", "count": 8 }
  ]
}
```

---

## 4. 认购专区说明

**认购专区**是农场优选模块中的一个特殊分类，用于展示可认购的土地信息：

### 4.1 认购专区数据来源

认购地块数据来自 `/api/acres` 接口（详见认购一亩田模块文档）。

### 4.2 认购专区显示逻辑

1. 当用户选择 `category: 'acre'` 时，页面只显示认购地块列表
2. 当用户选择 `category: 'all'` 时，页面顶部显示认购地块列表，下方显示商品列表
3. 当用户选择其他分类时，只显示对应分类的商品列表

### 4.3 认购地块数据结构

认购地块数据来自 `/api/acres` 接口，数据结构如下：

| 字段 | 类型 | 说明 |
|------|------|------|
| `id` | number | 地块ID |
| `name` | string | 地块名称/标题 |
| `image` / `cover` | string | 封面图路径 |
| `area` | number | 地块面积（亩） |
| `price` | number | 认购价格（元/亩） |
| `status` | string | 状态：`available`/`soldOut` |
| `location` | string | 地块位置描述 |
| `description` | string | 地块详细描述 |

---

## 接口清单速查

| 模块 | 方法 | 路径 | 需鉴权 |
|------|------|------|--------|
| FarmGoods | GET | `/api/farm-goods` | ❌ |
| FarmGoods | GET | `/api/farm-goods/categories` | ❌ |

---

*文档说明：农场优选模块与商品模块类似，但有独立的 API 接口，并且包含认购专区的特殊处理。*
