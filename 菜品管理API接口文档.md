# 菜品管理 API 接口文档

**基础路径：** `/api/dish`

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

## 1. 获取菜品状态列表

获取所有菜品状态，用于新增/编辑页面状态下拉框。

```
GET /api/dish/statuses
```

**响应：**
```json
{
  "code": 0,
  "data": [
    { "statusId": 1, "statusName": "上架" },
    { "statusId": 2, "statusName": "下架" },
    { "statusId": 3, "statusName": "售罄" }
  ]
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| statusId | int | 状态ID |
| statusName | string | 状态名称 |

> 数据从 `dish_status` 表动态读取。

---

## 2. 获取菜品类型列表

获取所有菜品类型，用于新增/编辑页面菜品类型下拉框。

```
GET /api/dish/categories
```

**响应：**
```json
{
  "code": 0,
  "data": [
    { "categoryId": 1, "categoryName": "农家热菜" },
    { "categoryId": 2, "categoryName": "特色主食" },
    { "categoryId": 3, "categoryName": "田园饮品" },
    { "categoryId": 4, "categoryName": "清爽凉菜" },
    { "categoryId": 5, "categoryName": "农家汤品" }
  ]
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| categoryId | int | 类型ID |
| categoryName | string | 类型名称 |

> 数据从 `dish_category` 表动态读取。

---

## 3. 获取菜品列表（分页）

```
GET /api/dish/list?pageNum=1&pageSize=15&keyword=xxx&status=上架
```

**查询参数：**

| 参数 | 类型 | 必填 | 默认值 | 说明 |
|------|------|------|--------|------|
| pageNum | int | 否 | 1 | 页码 |
| pageSize | int | 否 | 15 | 每页条数 |
| keyword | string | 否 | - | 按菜品名称/ID模糊搜索 |
| status | string | 否 | - | 按状态名称筛选（如"上架"） |

**响应：**
```json
{
  "code": 0,
  "data": {
    "records": [
      {
        "id": "1",
        "name": "红烧肉",
        "price": 68.00,
        "stock": 100,
        "status": "上架",
        "image": "/images/farm/xxx.jpg",
        "uploadTime": "2026-05-21 10:00",
        "description": "经典红烧肉",
        "dishType": "农家热菜",
        "specImages": [
          "/images/farm/spec1.jpg"
        ]
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
| id | string | 菜品ID |
| name | string | 菜品名称 |
| price | decimal | 价格 |
| stock | int | 库存 |
| status | string | 状态名称，从 `dish_status` 表映射 |
| image | string | 封面图URL |
| uploadTime | string | 上传时间 `yyyy-MM-dd HH:mm` |
| description | string | 菜品描述 |
| dishType | string | 菜品类型名称，从 `dish_category` 表映射 |
| specImages | array | 规格图片列表 |

---

## 4. 获取菜品详情

```
GET /api/dish/detail?id=1
```

**查询参数：**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| id | int | 是 | 菜品ID |

**响应：**
```json
{
  "code": 0,
  "data": {
    "id": "1",
    "name": "红烧肉",
    "price": 68.00,
    "stock": 100,
    "status": "上架",
    "image": "/images/farm/xxx.jpg",
    "coverImage": "/images/farm/xxx.jpg",
    "description": "经典红烧肉",
    "dishType": "农家热菜",
    "carouselMedia": [
      { "type": "image", "url": "/images/farm/carousel1.jpg" }
    ],
    "specImages": [
      "/images/farm/spec1.jpg"
    ],
    "uploadTime": "2026-05-21 10:00"
  }
}
```

---

## 5. 新增菜品

支持 `application/json` 和 `multipart/form-data` 两种提交方式。

```
POST /api/dish/add
```

**请求体（JSON）：**

```json
{
  "name": "红烧肉",
  "price": 68.00,
  "stock": 100,
  "status": "上架",
  "image": "base64编码图片数据",
  "description": "经典红烧肉",
  "dishType": "农家热菜",
  "carouselMedia": [
    { "type": "image", "url": "base64编码图片数据" }
  ],
  "specImages": [
    "base64编码图片数据"
  ]
}
```

**字段说明：**

| 字段 | 类型 | 必填 | 默认值 | 说明 |
|------|------|------|--------|------|
| name | string | 是 | - | 菜品名称 |
| price | decimal | 否 | 0 | 价格 |
| stock | int | 否 | 0 | 库存 |
| status | string | 否 | "上架" | 状态名称，自动映射到 `status_id` |
| image | string | 否 | - | 封面图（JSON传base64，FormData传文件） |
| description | string | 否 | "" | 菜品描述 |
| dishType | string | 否 | - | 菜品类型名称，自动映射到 `dish_category_id` |
| carouselMedia | array | 否 | [] | 轮播媒体列表 |
| carouselMedia[].type | string | 否 | "image" | 媒体类型: `image` 或 `video` |
| carouselMedia[].url | string | 是 | - | 媒体URL |
| specImages | array | 否 | [] | 规格图片URL列表（JSON传base64） |

**multipart/form-data 方式提交字段映射：**

| 表单字段名 | 类型 | 说明 |
|-----------|------|------|
| name | string | 菜品名称 |
| price | string | 价格 |
| stock | string | 库存 |
| status | string | 状态名称（如"上架"） |
| image | file | 封面图文件上传 |
| description | string | 菜品描述 |
| dishType | string | 菜品类型名称（如"农家热菜"） |
| specImages | file[] | 规格图片文件（多文件上传） |

**响应：**
```json
{
  "code": 0,
  "data": { "id": 1 }
}
```

---

## 6. 编辑菜品

```
PUT /api/dish/edit
POST /api/dish/edit
```

**请求体（JSON）：**

```json
{
  "id": 1,
  "name": "红烧肉",
  "price": 68.00,
  "stock": 100,
  "status": "上架",
  "image": "base64编码图片或留空",
  "description": "经典红烧肉",
  "dishType": "农家热菜",
  "carouselMedia": [
    { "type": "image", "url": "base64编码图片数据" }
  ],
  "specImages": [
    "base64编码图片数据"
  ]
}
```

> 字段与新增一致，额外增加 `id`（菜品ID，必填）。
> 编辑时轮播图和规格图会**全量替换**（先删旧数据再插入新数据）。

**multipart/form-data 额外字段：**

| 表单字段名 | 类型 | 说明 |
|-----------|------|------|
| id | string | 菜品ID |

其余字段与新增的 FormData 一致。

**响应：**
```json
{
  "code": 0,
  "data": "编辑成功"
}
```

---

## 7. 删除菜品（软删除）

```
POST /api/dish/delete
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
| id | int | 是 | 菜品ID |

> 软删除：将 `isdelete_id` 标记为 1，非物理删除。

**响应：**
```json
{
  "code": 0,
  "data": "删除成功"
}
```

---

## 8. 批量删除菜品

```
POST /api/dish/deleteBatch
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
| ids | int[] | 是 | 菜品ID数组 |

**响应：**
```json
{
  "code": 0,
  "data": "删除成功"
}
```

---

## 附录：状态映射表

| statusId | statusName | 来源 |
|----------|-----------|------|
| 1 | 上架 | `dish_status` 表 |
| 2 | 下架 | `dish_status` 表 |
| 3 | 售罄 | `dish_status` 表 |

> 新增/修改状态直接在数据库 `dish_status` 表操作即可，后端无需更改代码。

## 附录：菜品类型映射表

| categoryId | categoryName | 来源 |
|-----------|-------------|------|
| 1 | 农家热菜 | `dish_category` 表 |
| 2 | 特色主食 | `dish_category` 表 |
| 3 | 田园饮品 | `dish_category` 表 |
| 4 | 清爽凉菜 | `dish_category` 表 |
| 5 | 农家汤品 | `dish_category` 表 |

> 新增/修改菜品类型直接在数据库 `dish_category` 表操作即可，后端无需更改代码。
