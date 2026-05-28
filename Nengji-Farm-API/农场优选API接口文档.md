# 农场优选 API 接口文档

**基础路径**: `/api/farm-goods`  
**统一响应格式**:

```json
{
  "code": 0,
  "message": "success",
  "data": { ... }
}
```

---

## 1. 获取农场优选首页

```
GET /api/farm-goods/index
```

无需认证。获取首页轮播图、分类列表、今日优选、热销推荐。

**响应示例**:

```json
{
  "code": 0,
  "data": {
    "swiperList": [
      { "id": 1, "image": "/api/file/image/banner1.jpg" }
    ],
    "categories": [
      { "id": "1", "name": "蔬菜", "icon": "蔬", "color": "#4CAF50", "count": 12 }
    ],
    "todayGoods": [ /* 6 条商品卡片 */ ],
    "hotGoods": [ /* 6 条商品卡片 */ ]
  }
}
```

**响应字段说明**:

| 字段 | 类型 | 说明 |
|---|---|---|
| `swiperList` | array | 轮播图列表，从 `carousels` 表读取 `position = "goods"` 的数据 |
| `categories` | array | 商品分类列表（`icon` 取分类名首字，`color` 四色循环） |
| `todayGoods` | array | 今日优选，最新 12 条商品的前 6 条 |
| `hotGoods` | array | 热销推荐，最新 12 条商品的后 6 条 |

---

## 2. 分页查询商品列表

```
GET /api/farm-goods
```

**Query 参数**:

| 参数 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `type` | string | `"all"` | 筛选类型：`"all"` 全部、`"acre"` 田地（categoryId=5）、`"goods"` 非田地商品 |
| `category` | string | `"all"` | 分类筛选，传分类 ID 或分类名称 |
| `keyword` | string | - | 关键字搜索（按商品名模糊匹配） |
| `minPrice` | decimal | - | 最低价格 |
| `maxPrice` | decimal | - | 最高价格 |
| `page` | int | 1 | 页码 |
| `pageSize` | int | 10 | 每页条数 |

**响应**:

```json
{
  "code": 0,
  "data": {
    "list": [ /* GoodsCard[] */ ],
    "page": 1,
    "pageSize": 10,
    "total": 56
  }
}
```

**说明**:
- 只返回 `is_delete = 0` 且 `product_status = 1`（已上架）的商品
- 按 `commodity_id` 倒序排列（最新优先）
- `type=acre` 等价于 `categoryId=5`，`type=goods` 排除 categoryId=5
- `category` 参数支持传分类名称自动映射到分类 ID

---

## 3. 按分类查询商品

```
GET /api/farm-goods/category
```

**Query 参数**:

| 参数 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `categoryId` | string | - | 分类 ID |
| `category` | string | - | 分类 ID 或名称（别名） |
| `id` | string | - | 分类 ID（别名，优先级最低） |
| `page` | int | 1 | 页码 |
| `pageSize` | int | 50 | 每页条数 |

**响应**:

```json
{
  "code": 0,
  "data": {
    "category": "1",
    "categories": [],
    "items": [ /* GoodsCard[] */ ],
    "goodsList": [ /* 同 items */ ],
    "list": [ /* 同 items */ ],
    "page": 1,
    "pageSize": 50,
    "total": 12,
    "hasMore": false
  }
}
```

**说明**:
- `categoryId`、`category`、`id` 三个参数任传其一即可
- `categories` 始终返回空数组（此接口不包含分类列表）
- 默认 pageSize 为 50

---

## 4. 获取分类列表

```
GET /api/farm-goods/categories
```

无需认证。返回所有启用的商品分类。

**响应示例**:

```json
{
  "code": 0,
  "data": [
    { "id": "1", "name": "蔬菜", "icon": "蔬", "color": "#4CAF50", "count": 12 },
    { "id": "2", "name": "水果", "icon": "水", "color": "#FF9800", "count": 8 },
    { "id": "3", "name": "禽蛋", "icon": "禽", "color": "#2F7D8C", "count": 5 },
    { "id": "4", "name": "粮油", "icon": "粮", "color": "#C66B3D", "count": 3 }
  ]
}
```

**分类字段说明**:

| 字段 | 类型 | 说明 |
|---|---|---|
| `id` | string | 分类 ID |
| `name` | string | 分类名称 |
| `icon` | string | 分类图标（取名称首字符） |
| `color` | string | 主题色，四色循环：`#4CAF50`、`#FF9800`、`#2F7D8C`、`#C66B3D` |
| `count` | int | 该分类下已上架商品数量 |

**说明**:
- 来源表：`categories`，只返回 `category_status_id = 1` 的启用状态分类
- 按 `sort_order` → `id` 升序排列

---

## 5. 搜索商品

```
GET /api/farm-goods/search
```

**Query 参数**:

| 参数 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `keyword` | string | `""` | 搜索关键词 |
| `page` | int | 1 | 页码 |
| `pageSize` | int | 10 | 每页条数 |

**响应**:

```json
{
  "code": 0,
  "data": {
    "keyword": "鸡蛋",
    "items": [ /* GoodsCard[] */ ],
    "goodsList": [ /* 同 items */ ],
    "list": [ /* 同 items */ ],
    "total": 3,
    "page": 1,
    "pageSize": 10,
    "hasMore": false
  }
}
```

**说明**:
- 同时匹配 `product_name`（商品名）和 `spec_description`（规格描述）
- 只搜索上架且未删除的商品
- `hasMore` 标识是否有下一页

---

## 6. 商品卡片数据结构 (GoodsCard)

所有接口返回的商品数据均采用以下结构：

| 字段 | 类型 | 说明 |
|---|---|---|
| `id` | string | 商品 ID（commodity_id 转字符串） |
| `type` | string | 商品类型：`"goods"` 普通商品、`"acre"` 田地商品（categoryId=5 时） |
| `name` | string | 商品名称（product_name） |
| `price` | decimal | 当前售价（unit_price） |
| `originalPrice` | decimal | 原价，无原价时等于售价 |
| `image` | string | 商品图片 URL（经 MediaUrlHelper 归一化处理） |
| `stock` | int | 库存量（优先取统计表库存，回退 in_stock 字段） |
| `tags` | string[] | 商品标签列表（从 commodity_tag_relation + tags 表关联查询） |
| `status` | string | 库存状态：`"available"` 有货、`"soldOut"` 售罄（stock > 0 时有货） |
| `categoryId` | string | 分类 ID |
| `category` | string | 分类名称 |
| `spec` | string | 规格文本（如 `"500g/份"`，由 weight_text + unit 拼接） |
| `description` | string | 纯描述文本（从 spec_description 去除规格前缀） |
| `unit` | string | 单位名称（从 units 表关联） |
| `weight` | string | 原始重量文本（weight_text 原值） |
| `netWeight` | decimal? | 解析后的净重数值（如 `"500g"` → `500`） |
| `weightUnit` | string | 解析后的重量单位（如 `"500g"` → `"g"`、`"2.5斤"` → `"斤"`） |
| `sold` | int | 已售数量 |

**商品卡片 JSON 示例**:

```json
{
  "id": "42",
  "type": "goods",
  "name": "散养土鸡蛋",
  "price": 39.90,
  "originalPrice": 49.90,
  "image": "/api/file/image/egg.jpg",
  "stock": 200,
  "tags": ["农家散养", "无激素"],
  "status": "available",
  "categoryId": "3",
  "category": "禽蛋",
  "spec": "20枚/盒",
  "description": "农家散养土鸡蛋，营养丰富",
  "unit": "盒",
  "weight": "20枚",
  "netWeight": 20,
  "weightUnit": "枚",
  "sold": 156
}
```

---

## 相关实体结构

### commodity（商品表）

| 字段 | 类型 | 说明 |
|---|---|---|
| `commodity_id` | int (PK) | 商品 ID |
| `product_name` | varchar(100) | 商品名称 |
| `category_id` | int | 分类 ID |
| `unit_price` | decimal? | 售价 |
| `original_price` | decimal? | 原价（内存字段，不入库） |
| `image_url` | varchar(255)? | 图片 URL |
| `weight_text` | varchar(50)? | 重量文本（如 `"500g"`、`"20枚"`） |
| `spec_description` | varchar(255)? | 规格描述 |
| `in_stock` | int? | 库存量 |
| `quantity` | int? | 销量基数 |
| `unit_id` | int? | 单位 ID（关联 units 表） |
| `unit_name` | varchar? | 单位名称（内存字段，不入库） |
| `commodity_status_id` | int? | 商品状态（1=上架） |
| `points_price` | int? | 积分价格 |
| `isdelete_id` | int | 删除标记（0=正常） |

---

## 附录

### 状态码说明

| code | 说明 |
|---|---|
| `0` | 请求成功 |
| `-1` | 业务错误（ApiResult.Fail） |

### 列表接口统一行为

- 所有列表类接口默认按 `commodity_id` 倒序排列（最新优先）
- 分页参数最小值限制为 1（小于 1 时自动修正）
- 数据层均使用 `AsNoTracking()` 只读查询
