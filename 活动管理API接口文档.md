# 活动管理 API 接口文档

**基础路径：** `/api/activity-manage`

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

## 1. 获取状态列表

获取所有活动状态，用于新增/编辑页面状态下拉框。

```
GET /api/activity-manage/statuses
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

> 数据从 `activity_status` 表动态读取，表数据变更后接口自动生效。

---

## 2. 获取活动列表（分页）

```
GET /api/activity-manage/list?pageNum=1&pageSize=15&keyword=xxx
```

**查询参数：**

| 参数 | 类型 | 必填 | 默认值 | 说明 |
|------|------|------|--------|------|
| pageNum | int | 否 | 1 | 页码 |
| pageSize | int | 否 | 15 | 每页条数 |
| keyword | string | 否 | - | 按活动名称模糊搜索 |

**响应：**
```json
{
  "code": 0,
  "data": {
    "records": [
      {
        "id": 1,
        "name": "草莓采摘活动",
        "type": "采摘体验",
        "price": 99.00,
        "status": "上架",
        "image": "/images/farm/xxx.jpg",
        "people": 40,
        "duration": 60,
        "location": "三号大棚",
        "startDate": "2026-05-21T10:00:00",
        "endDate": "2026-06-21T10:00:00",
        "carouselMedia": [
          { "type": "image", "url": "/images/farm/yyy.jpg" }
        ],
        "createTime": "2026-05-20 21:33"
      }
    ],
    "total": 100,
    "pageNum": 1,
    "pageSize": 15,
    "pages": 7
  }
}
```

**records 字段说明：**

| 字段 | 类型 | 说明 |
|------|------|------|
| id | long | 活动ID |
| name | string | 活动名称 |
| type | string | 活动类型，从 `activity_type` 表映射 |
| price | decimal | 价格 |
| status | string | 状态名称，从 `activity_status` 表映射 |
| image | string | 封面图URL |
| people | int? | 可容纳人数 |
| duration | int | 活动时长（分钟） |
| location | string | 活动地点 |
| startDate | datetime | 活动开始时间 |
| endDate | datetime | 活动结束时间 |
| carouselMedia | array | 轮播媒体列表（最多5条） |
| createTime | string | 创建时间 `yyyy-MM-dd HH:mm` |

---

## 3. 获取活动详情

```
GET /api/activity-manage/detail?id=1
```

**查询参数：**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| id | long | 是 | 活动ID |

**响应：**
```json
{
  "code": 0,
  "data": {
    "id": 1,
    "name": "草莓采摘活动",
    "type": "采摘体验",
    "price": 99.00,
    "statusId": 1,
    "status": "上架",
    "image": "/images/farm/xxx.jpg",
    "imageName": "xxx.jpg",
    "videoUrl": "",
    "description": "活动描述",
    "location": "三号大棚",
    "people": 40,
    "content": "活动详细介绍内容",
    "duration": 60,
    "startDate": "2026-05-21T10:00:00",
    "endDate": "2026-06-21T10:00:00",
    "stock": 40,
    "carouselMedia": [
      { "type": "image", "url": "/images/farm/yyy.jpg" },
      { "type": "video", "url": "/images/farm/zzz.mp4" }
    ],
    "createTime": "2026-05-20 21:33"
  }
}
```

**详情特有字段：**

| 字段 | 类型 | 说明 |
|------|------|------|
| statusId | int | 状态ID（编辑回填下拉框选中） |
| status | string | 状态名称（展示） |
| imageName | string | 封面图文件名 |
| videoUrl | string | 视频URL |
| description | string | 简短描述 |
| content | string | 详细介绍（富文本） |
| stock | int | 剩余名额（当前 = people） |
| carouselMedia | array | 轮播媒体（最多5条） |

---

## 4. 新增活动

支持 `application/json` 和 `multipart/form-data` 两种提交方式。

```
POST /api/activity-manage/add
```

**请求体（JSON）：**

```json
{
  "name": "草莓采摘活动",
  "type": "采摘体验",
  "price": 99.00,
  "statusId": 1,
  "image": "base64编码的图片数据",
  "videoUrl": "base64编码的视频数据",
  "description": "活动描述",
  "location": "三号大棚",
  "people": 40,
  "content": "活动详细介绍",
  "duration": 60,
  "startDate": "2026-05-21T10:00:00",
  "endDate": "2026-06-21T10:00:00",
  "carouselMedia": [
    { "type": "image", "url": "base64编码图片数据" },
    { "type": "video", "url": "base64编码视频数据" }
  ]
}
```

**字段说明：**

| 字段 | 类型 | 必填 | 默认值 | 说明 |
|------|------|------|--------|------|
| name | string | 是 | - | 活动名称 |
| type | string | 否 | - | 活动类型名称，自动映射到 `type_id` |
| price | decimal | 否 | 0 | 价格 |
| statusId | int | 否 | 1 | 状态ID |
| image | string | 否 | - | 封面图（JSON传base64，FormData传文件） |
| videoUrl | string | 否 | - | 视频（JSON传base64） |
| description | string | 否 | - | 活动描述 |
| location | string | 否 | - | 活动地点 |
| people | int | 否 | - | 可容纳人数 |
| content | string | 否 | - | 活动详细介绍（富文本） |
| duration | int | 否 | 0 | 活动时长（分钟） |
| startDate | datetime | 否 | 当前时间 | 活动开始时间 |
| endDate | datetime | 否 | 当前+30天 | 活动结束时间 |
| carouselMedia | array | 否 | [] | 轮播媒体列表 |
| carouselMedia[].type | string | 否 | "image" | 媒体类型: `image` 或 `video` |
| carouselMedia[].url | string | 是 | - | 媒体URL（JSON传base64） |
| carouselMedia[].thumb | string | 否 | - | 视频封面图（仅视频类型需要） |

**multipart/form-data 方式提交字段映射：**

| 表单字段名 | 类型 | 说明 |
|-----------|------|------|
| name | string | 活动名称 |
| type | string | 活动类型名称（如 "采摘体验"） |
| price | string | 价格 |
| status | string | 状态名称（如 "上架"） |
| image | file | 封面图文件上传 |
| videoUrl | string | 视频URL文本 |
| description | string | 活动描述 |
| location | string | 活动地点 |
| people | string | 可容纳人数 |
| content | string | 活动详细介绍 |
| duration | string | 活动时长（分钟） |
| startDate | string | 开始时间 |
| endDate | string | 结束时间 |
| carouselMedia | file[] | 轮播媒体文件（多文件上传，自动识别图片/视频） |

**响应：**
```json
{
  "code": 0,
  "data": { "id": 1 }
}
```

---

## 5. 编辑活动

```
PUT /api/activity-manage/edit
POST /api/activity-manage/edit
```

**请求体（JSON）：**

```json
{
  "id": 1,
  "name": "草莓采摘活动",
  "type": "采摘体验",
  "price": 99.00,
  "statusId": 1,
  "image": "base64编码图片或留空",
  "videoUrl": "base64编码视频或留空",
  "description": "活动描述",
  "location": "三号大棚",
  "people": 40,
  "content": "活动详细介绍",
  "duration": 60,
  "startDate": "2026-05-21T10:00:00",
  "endDate": "2026-06-21T10:00:00",
  "carouselMedia": [
    { "type": "image", "url": "base64编码图片数据" }
  ]
}
```

> 字段与新增一致，额外增加 `id`（活动ID，必填）。
> 编辑时轮播媒体会**全量替换**（先删旧数据再插入新数据）。

**multipart/form-data 额外字段：**

| 表单字段名 | 类型 | 说明 |
|-----------|------|------|
| id | string | 活动ID |

其余字段与新增的 FormData 一致。

**响应：**
```json
{
  "code": 0,
  "data": "编辑成功"
}
```

---

## 6. 删除活动（软删除）

```
POST /api/activity-manage/delete
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
| id | long | 是 | 活动ID |

> 软删除：将 `isdelete_id` 标记为 1，非物理删除。

**响应：**
```json
{
  "code": 0,
  "data": "删除成功"
}
```

---

## 7. 批量删除活动

```
POST /api/activity-manage/deleteBatch
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
| ids | long[] | 是 | 活动ID数组 |

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
| 1 | 上架 | `activity_status` 表 |
| 2 | 下架 | `activity_status` 表 |
| 3 | 售罄 | `activity_status` 表 |

> 状态数据存于 `activity_status` 表，新增/修改状态直接在数据库操作即可，后端无需更改代码。

## 附录：活动类型映射

活动类型从 `activity_type` 表动态读取，类型名称作为 key 传入，后端自动映射为 `type_id`。
