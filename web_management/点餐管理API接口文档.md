# 点餐管理 API 接口文档

本文基于当前前端页面逻辑整理，主要参考以下页面：

- [dish.html](/e:/Kuade-NengJiFarm/web_management/dish.html)
- [dish-add.html](/e:/Kuade-NengJiFarm/web_management/dish-add.html)
- [dish-edit.html](/e:/Kuade-NengJiFarm/web_management/dish-edit.html)
- [table.html](/e:/Kuade-NengJiFarm/web_management/table.html)
- [table-form.html](/e:/Kuade-NengJiFarm/web_management/table-form.html)

当前"点餐管理"实际包含两块：

- 菜品管理：列表、搜索、分页、新增、编辑、单删、批量删除
- 餐桌管理：列表、搜索、分页、新增、编辑、状态切换、查看明细、单删、批量删除

## 一、公共约定

### 1.1 请求头

| 字段 | 是否必传 | 类型 | 说明 |
|---|---|---|---|
| `Content-Type` | 是 | String | 固定为 `application/json` |
| `token` | 建议 | String | 后台登录成功后返回的认证令牌 |

### 1.2 统一响应结构

| 字段 | 类型 | 说明 |
|---|---|---|
| `code` | Number | `200` 表示成功 |
| `message` | String | 提示文案 |
| `data` | Object / Array / Null | 业务数据 |

```json
{
  "code": 200,
  "message": "操作成功",
  "data": {}
}
```

### 1.3 时间格式

页面当前统一按 `yyyy-MM-dd HH:mm` 格式展示时间。

示例：

- `2026-04-01 12:00`
- `2026-12-01 09:30`

## 二、菜品管理

### 2.1 菜品字段说明

当前 `dish.html` 列表依赖以下字段：

| 字段 | 类型 | 是否必传 | 示例 | 说明 |
|---|---|---|---|---|
| `id` | String | 编辑/删除/详情必传 | `20240601120000` | 菜品唯一 ID |
| `name` | String | 是 | `宫保鸡丁` | 菜品名称 |
| `price` | Number | 是 | `28.5` | 菜品价格 |
| `stock` | Number | 是 | `25` | 库存数量 |
| `status` | String | 是 | `已上架` | 状态：`已上架` / `已下架` |
| `image` | String | 列表建议返回 | `https://example.com/dish-cover.jpg` | 列表封面图 |
| `coverImage` | String | 是 | `https://example.com/dish-cover.jpg` | 编辑页主封面 |
| `carouselMedia` | Object[] | 否 | `[{ "type": "image", "url": "..." }]` | 轮播图/视频，前端最多 5 个 |
| `specImages` | String[] | 否 | `["https://example.com/spec.jpg"]` | 规格图，前端最多 5 张 |
| `description` | String | 否 | `经典川味，下饭爽口` | 菜品介绍 |
| `uploadTime` | String | 列表建议返回 | `2026-04-01 12:00` | 上架时间或创建时间 |

**carouselMedia 对象结构**：

| 字段 | 类型 | 说明 |
|---|---|---|
| `type` | String | 媒体类型：`image`（图片）或 `video`（视频） |
| `url` | String | 媒体文件地址 |
| `thumb` | String | 视频缩略图（仅视频类型需要） |

说明：

- 菜品新增和编辑页面都有库存输入框，`stock` 为必传字段
- 前端判断状态时直接比较 `已上架` / `已下架`，不建议返回数字枚举
- 为减少前端兼容成本，详情接口建议同时返回 `image` 和 `coverImage`

### 2.2 获取菜品列表

| 项目 | 内容 |
|---|---|
| 接口名称 | 获取菜品列表 |
| 请求路径 | `/api/dish/list` |
| 请求方式 | `GET` |
| 接口说明 | 用于 `dish.html` 首屏加载、搜索、分页、刷新 |

#### Query 参数

| 字段 | 是否必传 | 类型 | 示例 | 说明 |
|---|---|---|---|---|
| `pageNum` | 是 | Number | `1` | 页码 |
| `pageSize` | 是 | Number | `15` | 每页条数 |
| `keyword` | 否 | String | `宫保` | 按菜品 ID 或菜品名称模糊搜索 |

#### 成功响应示例

```json
{
  "code": 200,
  "message": "获取成功",
  "data": {
    "records": [
      {
        "id": "20240601120000",
        "name": "宫保鸡丁",
        "image": "https://example.com/dish/gongbao-cover.jpg",
        "coverImage": "https://example.com/dish/gongbao-cover.jpg",
        "uploadTime": "2026-04-01 12:00",
        "price": 28.5,
        "stock": 25,
        "status": "已上架"
      },
      {
        "id": "20240701120000",
        "name": "麻婆豆腐",
        "image": "https://example.com/dish/mapo-cover.jpg",
        "coverImage": "https://example.com/dish/mapo-cover.jpg",
        "uploadTime": "2026-04-02 12:00",
        "price": 18.9,
        "stock": 30,
        "status": "已下架"
      }
    ],
    "total": 2,
    "pageNum": 1,
    "pageSize": 15,
    "pages": 1
  }
}
```

### 2.3 获取菜品详情

| 项目 | 内容 |
|---|---|
| 接口名称 | 获取菜品详情 |
| 请求路径 | `/api/dish/detail` |
| 请求方式 | `GET` |
| 接口说明 | 用于 `dish-edit.html?id=菜品ID` 回填表单 |

#### Query 参数

| 字段 | 是否必传 | 类型 | 示例 | 说明 |
|---|---|---|---|---|
| `id` | 是 | String | `20240601120000` | 菜品 ID |

#### 成功响应示例

```json
{
  "code": 200,
  "message": "获取成功",
  "data": {
    "id": "20240601120000",
    "name": "宫保鸡丁",
    "price": 28.5,
    "stock": 25,
    "status": "已上架",
    "image": "https://example.com/dish/gongbao-cover.jpg",
    "coverImage": "https://example.com/dish/gongbao-cover.jpg",
    "carouselMedia": [
      {
        "type": "image",
        "url": "https://example.com/dish/gongbao-banner-1.jpg"
      },
      {
        "type": "video",
        "url": "https://example.com/dish/gongbao-video.mp4",
        "thumb": "https://example.com/dish/gongbao-video-thumb.jpg"
      }
    ],
    "specImages": [
      "https://example.com/dish/gongbao-spec-1.jpg"
    ],
    "description": "经典川味，下饭爽口",
    "uploadTime": "2026-04-01 12:00"
  }
}
```

### 2.4 新增菜品

| 项目 | 内容 |
|---|---|
| 接口名称 | 新增菜品 |
| 请求路径 | `/api/dish/add` |
| 请求方式 | `POST` |
| 接口说明 | 用于 `dish-add.html` 提交新增表单 |

#### Body 参数

| 字段 | 是否必传 | 类型 | 示例 | 说明 |
|---|---|---|---|---|
| `name` | 是 | String | `宫保鸡丁` | 菜品名称 |
| `price` | 是 | Number | `28.5` | 菜品价格 |
| `stock` | 是 | Number | `25` | 库存数量 |
| `status` | 是 | String | `已上架` | 状态：`已上架` / `已下架` |
| `coverImage` | 是 | String | `https://example.com/dish/gongbao-cover.jpg` | 封面图 |
| `carouselMedia` | 否 | Object[] | `[{ "type": "image", "url": "..." }]` | 轮播图/视频数组 |
| `specImages` | 否 | String[] | `["https://example.com/spec.jpg"]` | 规格图 |
| `description` | 否 | String | `经典川味，下饭爽口` | 菜品介绍 |

#### 请求示例

```json
{
  "name": "宫保鸡丁",
  "price": 28.5,
  "stock": 25,
  "status": "已上架",
  "coverImage": "https://example.com/dish/gongbao-cover.jpg",
  "carouselMedia": [
    {
      "type": "image",
      "url": "https://example.com/dish/gongbao-banner-1.jpg"
    },
    {
      "type": "video",
      "url": "https://example.com/dish/gongbao-video.mp4",
      "thumb": "https://example.com/dish/gongbao-video-thumb.jpg"
    }
  ],
  "specImages": [
    "https://example.com/dish/gongbao-spec-1.jpg"
  ],
  "description": "经典川味，下饭爽口"
}
```

#### 成功响应示例

```json
{
  "code": 200,
  "message": "新增成功",
  "data": {
    "id": "20260413103001"
  }
}
```

### 2.5 编辑菜品

| 项目 | 内容 |
|---|---|
| 接口名称 | 编辑菜品 |
| 请求路径 | `/api/dish/edit` |
| 请求方式 | `PUT` |
| 接口说明 | 用于 `dish-edit.html` 提交修改结果 |

#### Body 参数

| 字段 | 是否必传 | 类型 | 示例 | 说明 |
|---|---|---|---|---|
| `id` | 是 | String | `20240601120000` | 菜品 ID |
| `name` | 是 | String | `宫保鸡丁` | 菜品名称 |
| `price` | 是 | Number | `29.9` | 菜品价格 |
| `stock` | 是 | Number | `18` | 库存数量 |
| `status` | 是 | String | `已下架` | 状态：`已上架` / `已下架` |
| `coverImage` | 是 | String | `https://example.com/dish/gongbao-cover.jpg` | 封面图 |
| `carouselMedia` | 否 | Object[] | `[{ "type": "image", "url": "..." }]` | 轮播图/视频数组 |
| `specImages` | 否 | String[] | `["https://example.com/spec.jpg"]` | 规格图 |
| `description` | 否 | String | `经典川味，下饭爽口` | 菜品介绍 |

#### 请求示例

```json
{
  "id": "20240601120000",
  "name": "宫保鸡丁",
  "price": 29.9,
  "stock": 18,
  "status": "已下架",
  "coverImage": "https://example.com/dish/gongbao-cover.jpg",
  "carouselMedia": [
    {
      "type": "image",
      "url": "https://example.com/dish/gongbao-banner-1.jpg"
    }
  ],
  "specImages": [
    "https://example.com/dish/gongbao-spec-1.jpg"
  ],
  "description": "经典川味，下饭爽口"
}
```

### 2.6 删除菜品

| 项目 | 内容 |
|---|---|
| 接口名称 | 删除菜品 |
| 请求路径 | `/api/dish/delete` |
| 请求方式 | `POST` |
| 接口说明 | 用于列表页单条删除 |

#### Body 参数

| 字段 | 是否必传 | 类型 | 示例 | 说明 |
|---|---|---|---|---|
| `id` | 是 | String | `20240601120000` | 菜品 ID |

```json
{
  "id": "20240601120000"
}
```

### 2.7 批量删除菜品

`dish.html` 顶部已支持多选删除，建议后端直接提供批量删除接口。

| 项目 | 内容 |
|---|---|
| 接口名称 | 批量删除菜品 |
| 请求路径 | `/api/dish/deleteBatch` |
| 请求方式 | `POST` |

#### Body 参数

| 字段 | 是否必传 | 类型 | 示例 | 说明 |
|---|---|---|---|---|
| `ids` | 是 | String[] | `["20240601120000","20240701120000"]` | 菜品 ID 集合 |

```json
{
  "ids": [
    "20240601120000",
    "20240701120000"
  ]
}
```

## 三、餐桌管理

### 3.1 餐桌字段说明

当前 `table.html` 和 `table-form.html` 依赖以下字段：

| 字段 | 类型 | 是否必传 | 示例 | 说明 |
|---|---|---|---|---|
| `id` | String | 是 | `A01` | 餐桌编号，需唯一 |
| `area` | String | 是 | `大厅A区` | 餐桌区域 |
| `type` | String | 是 | `四人桌` | 餐桌类型 |
| `capacity` | Number | 是 | `4` | 容纳人数 |
| `status` | String | 是 | `空闲` | 状态：`空闲` / `使用中` / `停用` |
| `detail` | String | 否 | `适合家庭或朋友聚餐` | 餐桌详情 |
| `updateTime` | String | 列表建议返回 | `2026-03-31 09:10` | 最后更新时间 |
| `codeStatus` | String | 否 | `点餐码正常` | 点餐码状态，用于明细弹窗展示 |

说明：

- 当前表单页区域选项固定为：`大厅A区`、`大厅B区`、`包厢`、`露台`
- 当前表单页类型选项固定为：`双人桌`、`四人桌`、`六人桌`、`包厢桌`、`露台桌`、`多人桌`
- 当前表单页若未填写 `detail`，前端会按区域和类型自动补默认说明，后端也可以做同样兜底
- 餐桌状态使用中文字符串：`空闲`、`使用中`、`停用`

### 3.2 获取餐桌列表

| 项目 | 内容 |
|---|---|
| 接口名称 | 获取餐桌列表 |
| 请求路径 | `/api/table/list` |
| 请求方式 | `GET` |
| 接口说明 | 用于 `table.html` 首屏加载、搜索、分页、刷新 |

#### Query 参数

| 字段 | 是否必传 | 类型 | 示例 | 说明 |
|---|---|---|---|---|
| `pageNum` | 是 | Number | `1` | 页码 |
| `pageSize` | 是 | Number | `10` | 每页条数 |
| `keyword` | 否 | String | `A区` | 按餐桌编号、区域、类型模糊搜索 |

#### 成功响应示例

```json
{
  "code": 200,
  "message": "获取成功",
  "data": {
    "records": [
      {
        "id": "A01",
        "area": "大厅A区",
        "type": "四人桌",
        "capacity": 4,
        "status": "空闲",
        "detail": "适合家庭或朋友聚餐。",
        "updateTime": "2026-03-31 09:10"
      },
      {
        "id": "VIP01",
        "area": "包厢",
        "type": "包厢桌",
        "capacity": 8,
        "status": "使用中",
        "detail": "独立包厢，可提供更私密的就餐环境。",
        "updateTime": "2026-03-31 12:05"
      }
    ],
    "total": 2,
    "pageNum": 1,
    "pageSize": 10,
    "pages": 1
  }
}
```

### 3.3 获取餐桌详情

| 项目 | 内容 |
|---|---|
| 接口名称 | 获取餐桌详情 |
| 请求路径 | `/api/table/detail` |
| 请求方式 | `GET` |
| 接口说明 | 用于 `table-form.html?id=餐桌编号` 回填，也可用于明细弹窗 |

#### Query 参数

| 字段 | 是否必传 | 类型 | 示例 | 说明 |
|---|---|---|---|---|
| `id` | 是 | String | `A01` | 餐桌编号 |

#### 成功响应示例

```json
{
  "code": 200,
  "message": "获取成功",
  "data": {
    "id": "A01",
    "area": "大厅A区",
    "type": "四人桌",
    "capacity": 4,
    "status": "空闲",
    "codeStatus": "点餐码正常",
    "detail": "适合家庭或朋友聚餐。",
    "updateTime": "2026-03-31 09:10"
  }
}
```

### 3.4 新增餐桌

| 项目 | 内容 |
|---|---|
| 接口名称 | 新增餐桌 |
| 请求路径 | `/api/table/add` |
| 请求方式 | `POST` |
| 接口说明 | 用于 `table-form.html` 新增模式保存 |

#### Body 参数

| 字段 | 是否必传 | 类型 | 示例 | 说明 |
|---|---|---|---|---|
| `id` | 是 | String | `A01` | 餐桌编号 |
| `area` | 是 | String | `大厅A区` | 区域 |
| `type` | 是 | String | `四人桌` | 类型 |
| `capacity` | 是 | Number | `4` | 容纳人数 |
| `status` | 是 | String | `空闲` | 状态：`空闲` / `使用中` / `停用` |
| `detail` | 否 | String | `适合家庭或朋友聚餐` | 餐桌详情 |

#### 请求示例

```json
{
  "id": "A01",
  "area": "大厅A区",
  "type": "四人桌",
  "capacity": 4,
  "status": "空闲",
  "detail": "适合家庭或朋友聚餐"
}
```

### 3.5 编辑餐桌

| 项目 | 内容 |
|---|---|
| 接口名称 | 编辑餐桌 |
| 请求路径 | `/api/table/edit` |
| 请求方式 | `PUT` |
| 接口说明 | 用于 `table-form.html?id=餐桌编号` 编辑模式保存 |

#### Body 参数

| 字段 | 是否必传 | 类型 | 示例 | 说明 |
|---|---|---|---|---|
| `id` | 是 | String | `A01` | 餐桌编号 |
| `area` | 是 | String | `大厅A区` | 区域 |
| `type` | 是 | String | `四人桌` | 类型 |
| `capacity` | 是 | Number | `4` | 容纳人数 |
| `status` | 是 | String | `使用中` | 状态：`空闲` / `使用中` / `停用` |
| `detail` | 否 | String | `适合家庭或朋友聚餐` | 餐桌详情 |

#### 请求示例

```json
{
  "id": "A01",
  "area": "大厅A区",
  "type": "四人桌",
  "capacity": 4,
  "status": "使用中",
  "detail": "适合家庭或朋友聚餐"
}
```

### 3.6 删除餐桌

| 项目 | 内容 |
|---|---|
| 接口名称 | 删除餐桌 |
| 请求路径 | `/api/table/delete` |
| 请求方式 | `POST` |
| 接口说明 | 用于列表页单条删除 |

#### Body 参数

| 字段 | 是否必传 | 类型 | 示例 | 说明 |
|---|---|---|---|---|
| `id` | 是 | String | `A01` | 餐桌编号 |

```json
{
  "id": "A01"
}
```

### 3.7 批量删除餐桌

| 项目 | 内容 |
|---|---|
| 接口名称 | 批量删除餐桌 |
| 请求路径 | `/api/table/deleteBatch` |
| 请求方式 | `POST` |

#### Body 参数

| 字段 | 是否必传 | 类型 | 示例 | 说明 |
|---|---|---|---|---|
| `ids` | 是 | String[] | `["A01","A02"]` | 餐桌编号集合 |

```json
{
  "ids": [
    "A01",
    "A02"
  ]
}
```

### 3.8 更新餐桌状态

`table.html` 列表支持直接切换餐桌状态，所以建议单独提供状态更新接口。

| 项目 | 内容 |
|---|---|
| 接口名称 | 更新餐桌状态 |
| 请求路径 | `/api/table/updateStatus` |
| 请求方式 | `PUT` |
| 接口说明 | 用于列表页直接切换状态 |

#### Body 参数

| 字段 | 是否必传 | 类型 | 示例 | 说明 |
|---|---|---|---|---|
| `id` | 是 | String | `A01` | 餐桌编号 |
| `status` | 是 | String | `使用中` | 新状态：`空闲` / `使用中` / `停用` |

#### 请求示例

```json
{
  "id": "A01",
  "status": "使用中"
}
```

#### 成功响应示例

```json
{
  "code": 200,
  "message": "更新成功",
  "data": {
    "id": "A01",
    "status": "使用中",
    "updateTime": "2026-04-27 10:20"
  }
}
```

### 3.9 餐桌二维码说明

当前前端逻辑中：

- 不依赖后端生成二维码图片接口
- 前端直接拼接 `table-scan.html` 落地页地址
- 再使用第三方二维码服务把该链接转成二维码

因此当前后端不是必须提供二维码接口。

如果后端后续希望统一生成，也可以在餐桌详情或列表中补充以下字段：

| 字段 | 类型 | 示例 | 说明 |
|---|---|---|---|
| `scanUrl` | String | `https://your-domain.com/table-scan.html?tableId=A01` | 餐桌落地页链接 |
| `qrCodeUrl` | String | `https://your-domain.com/static/qrcode/A01.png` | 二维码图片地址 |

## 四、前端兼容注意事项

- `dish.html` 当前搜索只依赖 `id` 和 `name`
- `table.html` 当前搜索只依赖 `id`、`area` 和 `type`
- 菜品状态当前前端直接用中文值：`已上架`、`已下架`
- 餐桌状态使用中文字符串：`空闲`、`使用中`、`停用`
- 列表分页建议统一返回：`records`、`total`、`pageNum`、`pageSize`、`pages`
- 菜品编辑页如果只返回 `coverImage` 不返回 `image`，前端仍建议后端保持两者一致，减少兼容判断
- 餐桌详情若未填写 `detail`，前后端都可以按区域和类型生成默认说明
- `carouselMedia` 支持图片和视频混合，建议后端支持视频上传和存储

## 五、通用失败场景

| 场景 | 状态码 | 提示文案 |
|---|---|---|
| token 无效或过期 | `401` | 登录已过期，请重新登录 |
| 权限不足 | `403` | 权限不足，仅管理员可操作 |
| 参数错误 | `400` | 请求参数不完整或格式错误 |
| 菜品不存在 | `404` | 菜品不存在或已被删除 |
| 餐桌不存在 | `404` | 餐桌不存在或已被删除 |
| 编号重复 | `409` | 编号已存在，请更换后再保存 |
| 服务异常 | `500` | 服务器异常，请稍后重试 |
