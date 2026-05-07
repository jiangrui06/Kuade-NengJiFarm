# 产品管理 API 接口文档

本文档基于当前前端页面 [product.html](/e:/Kuade-NengJiFarm/web_management/product.html)、[product-add.html](/e:/Kuade-NengJiFarm/web_management/product-add.html)、[product-edit.html](/e:/Kuade-NengJiFarm/web_management/product-edit.html) 的字段与交互整理，用于前后端联调或 mock 接口开发。

当前产品页仍主要使用 `localStorage` 模拟数据，后端接入时建议按本文档提供的接口结构返回，便于直接替换前端现有本地逻辑。

## 一、适用页面

- `product.html`
  产品列表、分页、搜索、删除、跳转新增/编辑
- `product-add.html`
  新增产品表单提交
- `product-edit.html`
  按产品 ID 加载详情并提交编辑

## 二、公共约定

### 2.1 请求头

| 参数名 | 是否必传 | 类型 | 说明 |
|---|---|---|---|
| `Content-Type` | 是 | String | 固定为 `application/json` |
| `token` | 建议 | String | 登录接口返回的认证令牌，后台管理接口建议统一校验 |

### 2.2 统一响应结构

| 参数名 | 类型 | 说明 |
|---|---|---|
| `code` | Number | 业务状态码，`200` 表示成功 |
| `message` | String | 返回提示文案 |
| `data` | Object/Array/Null | 业务数据 |

```json
{
  "code": 200,
  "message": "操作成功",
  "data": {}
}
```

### 2.3 产品字段说明

| 字段名 | 类型 | 是否必传 | 示例 | 说明 |
|---|---|---|---|---|
| `id` | String | 编辑/删除必传 | `20260301120031` | 产品唯一 ID |
| `name` | String | 是 | `西红柿` | 产品名称 |
| `price` | Number | 是 | `3.5` | 产品单价 |
| `stock` | Number | 是 | `50` | 库存数量 |
| `status` | String | 是 | `已上架` | 状态：`已上架`、`已下架` |
| `image` | String | 列表建议返回 | `https://example.com/a.jpg` | 列表页主图字段，建议与 `coverImage` 同值 |
| `coverImage` | String | 是 | `https://example.com/a.jpg` | 产品封面图 |
| `carouselMedia` | Object[] | 否 | `[{ "type": "image", "url": "https://..." }]` | 轮播图/视频，前端最多展示/添加 5 张 |
| `netWeight` | Number | 否 | `1` | 净含量数值 |
| `weightUnit` | String | 否 | `kg` | 单位枚举：`kg`、`g`、`斤` |
| `storageCondition` | String | 否 | `常温保存` | 保存条件 |
| `specImages` | String[] | 否 | `["https://example.com/spec.jpg"]` | 规格图片，前端最多展示/添加 5 张 |
| `description` | String | 否 | `新鲜采摘，口感清甜` | 产品介绍 |
| `uploadTime` | String | 列表建议返回 | `2026-03-01 12:00` | 上架/创建时间，列表页直接展示 |

**carouselMedia 对象结构**：
| 字段名 | 类型 | 说明 |
|---|---|---|
| `type` | String | 媒体类型：`image`（图片）或 `video`（视频） |
| `url` | String | 媒体文件地址 |
| `thumb` | String | 视频缩略图（仅视频类型需要） |

### 2.4 前端字段限制建议

- `name` 不能为空
- `price` 不能小于 `0`
- `status` 只能为 `已上架` 或 `已下架`
- `carouselMedia` 最多 `5` 个（图片/视频混合）
- `specImages` 最多 `5` 张
- `weightUnit` 建议限制为 `kg`、`g`、`斤`

## 三、接口 1：获取产品列表

### 3.1 接口信息

| 项目 | 内容 |
|---|---|
| 接口名称 | 获取产品列表 |
| 请求路径 | `/api/product/list` |
| 请求方式 | `GET` |
| 接口说明 | 用于 `product.html` 列表页初始化、分页、刷新和搜索 |

### 3.2 Query 参数

| 参数名 | 是否必传 | 类型 | 示例 | 说明 |
|---|---|---|---|---|
| `pageNum` | 是 | Number | `1` | 当前页码，从 `1` 开始 |
| `pageSize` | 是 | Number | `15` | 每页条数 |
| `keyword` | 否 | String | `西红柿` | 按产品 ID 或产品名称模糊搜索 |

### 3.3 成功响应示例

```json
{
  "code": 200,
  "message": "获取成功",
  "data": {
    "records": [
      {
        "id": "20260301120031",
        "name": "西红柿",
        "image": "https://example.com/product/tomato-cover.jpg",
        "coverImage": "https://example.com/product/tomato-cover.jpg",
        "price": 3,
        "stock": 50,
        "status": "已上架",
        "uploadTime": "2026-03-01 12:00"
      },
      {
        "id": "20260302120032",
        "name": "黄瓜",
        "image": "https://example.com/product/cucumber-cover.jpg",
        "coverImage": "https://example.com/product/cucumber-cover.jpg",
        "price": 2.5,
        "stock": 60,
        "status": "已上架",
        "uploadTime": "2026-03-02 12:00"
      }
    ],
    "total": 26,
    "pageNum": 1,
    "pageSize": 15,
    "pages": 2
  }
}
```

### 3.4 前端使用说明

- 列表页表格至少依赖：`id`、`image`、`name`、`uploadTime`、`price`、`stock`、`status`
- 搜索框当前只匹配 `id` 和 `name`
- 若后端只维护 `coverImage`，建议同时返回 `image`，减少前端改动

## 四、接口 2：获取产品详情

### 4.1 接口信息

| 项目 | 内容 |
|---|---|
| 接口名称 | 获取产品详情 |
| 请求路径 | `/api/product/detail` |
| 请求方式 | `GET` |
| 接口说明 | 用于 `product-edit.html?id=产品ID` 页面回填表单 |

### 4.2 Query 参数

| 参数名 | 是否必传 | 类型 | 示例 | 说明 |
|---|---|---|---|---|
| `id` | 是 | String | `20260301120031` | 产品 ID |

### 4.3 成功响应示例

```json
{
  "code": 200,
  "message": "获取成功",
  "data": {
    "id": "20260301120031",
    "name": "西红柿",
    "price": 3,
    "stock": 50,
    "status": "已上架",
    "image": "https://example.com/product/tomato-cover.jpg",
    "coverImage": "https://example.com/product/tomato-cover.jpg",
    "carouselMedia": [
      {
        "type": "image",
        "url": "https://example.com/product/tomato-banner-1.jpg"
      },
      {
        "type": "video",
        "url": "https://example.com/product/tomato-video.mp4",
        "thumb": "https://example.com/product/tomato-video-thumb.jpg"
      }
    ],
    "netWeight": 1,
    "weightUnit": "kg",
    "storageCondition": "常温保存",
    "specImages": [
      "https://example.com/product/tomato-spec-1.jpg",
      "https://example.com/product/tomato-spec-2.jpg"
    ],
    "description": "新鲜西红柿，适合凉拌、炒菜和炖汤",
    "uploadTime": "2026-03-01 12:00"
  }
}
```

## 五、接口 3：新增产品

### 5.1 接口信息

| 项目 | 内容 |
|---|---|
| 接口名称 | 新增产品 |
| 请求路径 | `/api/product/add` |
| 请求方式 | `POST` |
| 接口说明 | 用于 `product-add.html` 提交新增产品表单 |

### 5.2 Body 参数

| 参数名 | 是否必传 | 类型 | 示例 | 说明 |
|---|---|---|---|---|
| `name` | 是 | String | `西红柿` | 产品名称 |
| `price` | 是 | Number | `3` | 产品价格 |
| `stock` | 是 | Number | `50` | 库存数量 |
| `status` | 是 | String | `已上架` | 上下架状态 |
| `coverImage` | 是 | String | `https://example.com/product/tomato-cover.jpg` | 封面图 |
| `carouselMedia` | 否 | Object[] | 见示例 | 轮播图/视频数组 |
| `netWeight` | 否 | Number | `1` | 净含量 |
| `weightUnit` | 否 | String | `kg` | 单位 |
| `storageCondition` | 否 | String | `常温保存` | 保存条件 |
| `specImages` | 否 | String[] | `["https://example.com/spec.jpg"]` | 规格图 |
| `description` | 否 | String | `新鲜采摘` | 产品介绍 |

### 5.3 请求示例

```json
{
  "name": "西红柿",
  "price": 3,
  "stock": 50,
  "status": "已上架",
  "coverImage": "https://example.com/product/tomato-cover.jpg",
  "carouselMedia": [
    {
      "type": "image",
      "url": "https://example.com/product/tomato-banner-1.jpg"
    },
    {
      "type": "video",
      "url": "https://example.com/product/tomato-video.mp4",
      "thumb": "https://example.com/product/tomato-video-thumb.jpg"
    }
  ],
  "netWeight": 1,
  "weightUnit": "kg",
  "storageCondition": "常温保存",
  "specImages": [
    "https://example.com/product/tomato-spec-1.jpg"
  ],
  "description": "新鲜采摘，适合凉拌、炒菜和炖汤"
}
```

### 5.4 成功响应示例

```json
{
  "code": 200,
  "message": "新增成功",
  "data": {
    "id": "20260409103001"
  }
}
```

## 六、接口 4：编辑产品

### 6.1 接口信息

| 项目 | 内容 |
|---|---|
| 接口名称 | 编辑产品 |
| 请求路径 | `/api/product/edit` |
| 请求方式 | `PUT` |
| 接口说明 | 用于 `product-edit.html` 提交修改后的产品信息 |

### 6.2 Body 参数

除 `id` 必传外，其余字段可按新增接口结构提交。

| 参数名 | 是否必传 | 类型 | 示例 | 说明 |
|---|---|---|---|---|
| `id` | 是 | String | `20260301120031` | 产品 ID |
| `name` | 是 | String | `西红柿` | 产品名称 |
| `price` | 是 | Number | `3.2` | 产品价格 |
| `stock` | 是 | Number | `38` | 库存数量 |
| `status` | 是 | String | `已下架` | 上下架状态 |
| `coverImage` | 是 | String | `https://example.com/product/tomato-cover.jpg` | 封面图 |
| `carouselMedia` | 否 | Object[] | 见示例 | 轮播图/视频数组 |
| `netWeight` | 否 | Number | `1` | 净含量 |
| `weightUnit` | 否 | String | `kg` | 单位 |
| `storageCondition` | 否 | String | `冷藏保存` | 保存条件 |
| `specImages` | 否 | String[] | `["https://example.com/spec.jpg"]` | 规格图 |
| `description` | 否 | String | `产地直发` | 产品介绍 |

### 6.3 请求示例

```json
{
  "id": "20260301120031",
  "name": "西红柿",
  "price": 3.2,
  "stock": 38,
  "status": "已下架",
  "coverImage": "https://example.com/product/tomato-cover.jpg",
  "carouselMedia": [
    {
      "type": "image",
      "url": "https://example.com/product/tomato-banner-1.jpg"
    }
  ],
  "netWeight": 1,
  "weightUnit": "kg",
  "storageCondition": "冷藏保存",
  "specImages": [
    "https://example.com/product/tomato-spec-1.jpg"
  ],
  "description": "新鲜采摘，适合凉拌和热炒"
}
```

## 七、接口 5：删除产品

### 7.1 接口信息

| 项目 | 内容 |
|---|---|
| 接口名称 | 删除产品 |
| 请求路径 | `/api/product/delete` |
| 请求方式 | `POST` |
| 接口说明 | 用于产品列表页删除单个产品 |

### 7.2 Body 参数

| 参数名 | 是否必传 | 类型 | 示例 | 说明 |
|---|---|---|---|---|
| `id` | 是 | String | `20260301120031` | 产品 ID |

### 7.3 请求示例

```json
{
  "id": "20260301120031"
}
```

## 八、接口 5-1：批量删除产品（推荐）

当前 `product.html` 顶部已有勾选删除入口，若后端准备支持批量操作，建议补充以下接口，便于后续前端直接升级。

| 项目 | 内容 |
|---|---|
| 接口名称 | 批量删除产品 |
| 请求路径 | `/api/product/deleteBatch` |
| 请求方式 | `POST` |

### 8.1 Body 参数

| 参数名 | 是否必传 | 类型 | 示例 | 说明 |
|---|---|---|---|---|
| `ids` | 是 | String[] | `["20260301120031","20260302120032"]` | 待删除产品 ID 集合 |

### 8.2 请求示例

```json
{
  "ids": [
    "20260301120031",
    "20260302120032"
  ]
}
```

## 九、管理接口通用失败场景

| 场景 | 状态码 | 提示文案 |
|---|---|---|
| `token` 无效或过期 | `401` | 登录已过期，请重新登录 |
| 非管理员操作 | `403` | 权限不足，仅管理员可操作 |
| 产品不存在 | `404` | 产品不存在或已被删除 |
| 参数缺失 | `400` | 请求参数不完整 |
| 服务异常 | `500` | 服务器异常，请稍后重试 |

## 十、对接建议

- 列表接口和详情接口尽量同时返回 `image` 与 `coverImage`，减少前端字段转换
- `uploadTime` 建议统一返回 `yyyy-MM-dd HH:mm` 格式
- 如果图片未来改为文件上传，可单独补充 `/api/common/upload` 或 `/api/product/uploadImage`
- 若后端暂不支持批量删除，前端也可循环调用单删接口兼容上线
- `carouselMedia` 支持图片和视频混合，建议后端支持视频上传和存储
