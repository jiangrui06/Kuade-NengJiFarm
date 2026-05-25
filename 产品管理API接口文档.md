# 产品管理 API 接口文档

**基础路径：** `/api/product`

**通用响应格式：**
```json
{
  "code": 0,
  "message": "success",
  "data": { ... }
}
```
成功时 `code = 0`，失败时 `code` 为错误码（400/404/500），`message` 为错误描述。

---

## 1. 获取商品状态列表

获取所有商品状态，用于新增/编辑页面状态下拉框。

```
GET /api/product/statuses
```

**响应：**
```json
{
  "code": 0,
  "data": [
    { "commodityStatusId": 1, "statusName": "上架" },
    { "commodityStatusId": 2, "statusName": "下架" },
    { "commodityStatusId": 3, "statusName": "售罄" }
  ]
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| commodityStatusId | int | 状态ID |
| statusName | string | 状态名称 |

> 数据从 `commodity_status` 表动态读取，前端新增/编辑页面的状态下拉框调用此接口获取。

---

## 2. 获取商品分类列表

获取所有商品分类，用于新增/编辑页面分类下拉框。

```
GET /api/product/categories
```

**响应：**
```json
{
  "code": 0,
  "data": [
    { "id": 1, "categoryName": "蔬菜" },
    { "id": 2, "categoryName": "水果" }
  ]
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| id | int | 分类ID |
| categoryName | string | 分类名称 |

> 数据从 `commodity_category` 表动态读取。

---

## 3. 获取已启用的单位列表

```
GET /api/product/units
```

**响应：**
```json
{
  "code": 0,
  "data": [
    { "unitId": 1, "unitName": "斤" },
    { "unitId": 2, "unitName": "kg" },
    { "unitId": 3, "unitName": "g" }
  ]
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| unitId | int | 单位ID |
| unitName | string | 单位名称 |

> 数据从 `unit` 表动态读取，仅返回已启用的单位。

---

## 4. 获取产品列表（分页）

```
GET /api/product/list?pageNum=1&pageSize=15&keyword=xxx
```

**查询参数：**

| 参数 | 类型 | 必填 | 默认值 | 说明 |
|------|------|------|--------|------|
| pageNum | int | 否 | 1 | 页码 |
| pageSize | int | 否 | 15 | 每页条数 |
| keyword | string | 否 | - | 按产品名称/ID模糊搜索 |

**响应：**
```json
{
  "code": 0,
  "data": {
    "records": [
      {
        "id": "1",
        "name": "西红柿",
        "price": 3.00,
        "stock": 50,
        "status": "已上架",
        "image": "/images/product/tomato.jpg",
        "uploadTime": "2026-05-21 10:00",
        "netWeight": 1,
        "weightUnit": "kg",
        "productType": "蔬菜"
      }
    ],
    "total": 1,
    "pageNum": 1,
    "pageSize": 15,
    "pages": 1
  }
}
```

**records 字段说明：**

| 字段 | 类型 | 说明 |
|------|------|------|
| id | string | 产品ID |
| name | string | 产品名称 |
| price | decimal | 价格 |
| stock | int | 库存 |
| status | string | 状态名称，从 `commodity_status` 表动态映射 |
| image | string | 封面图URL |
| uploadTime | string | 上传时间 `yyyy-MM-dd HH:mm` |
| netWeight | decimal | 净含量 |
| weightUnit | string | 重量单位 |
| productType | string | 商品类型名称，从 `commodity_category` 表映射 |

---

## 5. 获取产品详情

```
GET /api/product/detail?id=1
```

**查询参数：**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| id | int | 是 | 产品ID |

**响应：**
```json
{
  "code": 0,
  "data": {
    "id": "1",
    "name": "西红柿",
    "price": 3.00,
    "stock": 50,
    "status": "已上架",
    "image": "/images/product/tomato.jpg",
    "coverImage": "/images/product/tomato.jpg",
    "carouselMedia": [
      { "type": "image", "url": "/images/product/banner1.jpg" }
    ],
    "netWeight": 1,
    "weightUnit": "kg",
    "storageCondition": "常温保存",
    "specImages": [
      "/images/product/spec1.jpg"
    ],
    "description": "新鲜采摘",
    "uploadTime": "2026-05-21 10:00",
    "productType": "蔬菜"
  }
}
```

---

## 6. 新增产品

支持 `application/json` 和 `multipart/form-data` 两种提交方式。

```
POST /api/product/add
```

**请求体（JSON）：**

```json
{
  "name": "西红柿",
  "price": 3.00,
  "stock": 50,
  "status": "已上架",
  "coverImage": "base64编码图片数据",
  "carouselMedia": [
    { "type": "image", "url": "base64编码图片数据" }
  ],
  "netWeight": 1,
  "weightUnit": "kg",
  "storageCondition": "常温保存",
  "specImages": [
    "base64编码图片数据"
  ],
  "description": "新鲜采摘",
  "productType": "蔬菜"
}
```

**字段说明：**

| 字段 | 类型 | 必填 | 默认值 | 说明 |
|------|------|------|--------|------|
| name | string | 是 | - | 产品名称 |
| price | decimal | 否 | 0 | 价格 |
| stock | int | 否 | 0 | 库存 |
| status | string | 否 | "已下架" | 状态名称，自动映射到 `commodity_status_id` |
| coverImage | string | 否 | - | 封面图（JSON传base64，FormData传文件） |
| carouselMedia | array | 否 | [] | 轮播媒体列表 |
| carouselMedia[].type | string | 否 | "image" | 媒体类型: `image` 或 `video` |
| carouselMedia[].url | string | 是 | - | 媒体URL |
| netWeight | decimal | 否 | - | 净含量 |
| weightUnit | string | 否 | - | 重量单位（如 `kg`、`g`、`斤`） |
| storageCondition | string | 否 | "" | 保存条件 |
| specImages | array | 否 | [] | 规格图片URL列表（JSON传base64） |
| description | string | 否 | "" | 产品介绍 |
| productType | string | 否 | - | 商品类型名称，自动映射到 `category_id` |

**响应：**
```json
{
  "code": 0,
  "data": { "id": 1 }
}
```

---

## 7. 编辑产品

```
PUT /api/product/edit
POST /api/product/edit
```

**请求体（JSON）：**

```json
{
  "id": 1,
  "name": "西红柿",
  "price": 3.00,
  "stock": 50,
  "status": "已上架",
  "coverImage": "base64编码图片或留空",
  "carouselMedia": [
    { "type": "image", "url": "base64编码图片数据" }
  ],
  "netWeight": 1,
  "weightUnit": "kg",
  "storageCondition": "常温保存",
  "specImages": [
    "base64编码图片数据"
  ],
  "description": "新鲜采摘",
  "productType": "蔬菜"
}
```

> 字段与新增一致，额外增加 `id`（产品ID，必填）。
> 编辑时轮播图和规格图会**全量替换**（先删旧数据再插入新数据）。

**multipart/form-data 额外字段：**

| 表单字段名 | 类型 | 说明 |
|-----------|------|------|
| id | string | 产品ID |

其余字段与新增的 FormData 一致。

**响应：**
```json
{
  "code": 0,
  "data": "编辑成功"
}
```

---

## 8. 删除产品（软删除）

```
POST /api/product/delete
Content-Type: application/json
```

**请求体：**

```json
{
  "id": 1
}
```

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| id | int | 是 | 产品ID |

> 软删除：将 `isdelete_id` 标记为 1，非物理删除。

**响应：**
```json
{
  "code": 0,
  "data": "删除成功"
}
```

---

## 9. 批量删除产品

```
POST /api/product/deleteBatch
Content-Type: application/json
```

**请求体：**

```json
{
  "ids": [1, 2, 3]
}
```

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| ids | int[] | 是 | 产品ID数组 |

**响应：**
```json
{
  "code": 0,
  "data": "删除成功"
}
```

---

## 10. 获取产品管理统计数据

```
GET /api/product/stats
```

**响应：**
```json
{
  "code": 0,
  "data": {
    "totalProducts": 100,
    "onSaleCount": 80,
    "stockAlertCount": 5,
    "totalStock": 2000
  }
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| totalProducts | int | 产品总数 |
| onSaleCount | int | 上架数量 |
| stockAlertCount | int | 库存预警数量（库存 ≤ 5） |
| totalStock | int | 总库存 |

---

## 附录：状态映射表

| commodityStatusId | statusName | 来源 |
|------------------|-----------|------|
| 1 | 上架 | `commodity_status` 表 |
| 2 | 下架 | `commodity_status` 表 |
| 3 | 售罄 | `commodity_status` 表 |

> 新增/修改状态直接在数据库 `commodity_status` 表操作即可，后端无需更改代码。
> 所有状态数据均从数据库动态读取，前端通过 `GET /api/product/statuses` 获取。
