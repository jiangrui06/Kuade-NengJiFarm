# 首页内容管理 & 农场简介 - 后端 API 接口文档

**基础路径：** `/api/admin/home`

**认证方式：** 需携带管理员 Token（通过 `token` header 传递）

**响应格式：** 统一使用 `ApiResult` 包装

```json
// 成功
{ "code": 0, "message": "success", "data": {...} }

// 失败
{ "code": 非0, "message": "错误描述", "data": null }
```

---

## 一、轮播图管理

### 1.1 获取轮播图列表

```
GET /api/admin/home/carousels
```

**响应示例：**

```json
{
  "code": 0,
  "message": "success",
  "data": {
    "records": [
      {
        "id": 1,
        "imageUrl": "/images/farm/xxx.jpg",
        "linkUrl": "/pages/about/about",
        "sortOrder": 1
      }
    ]
  }
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| id | long | 轮播图 ID |
| imageUrl | string | 图片地址 |
| linkUrl | string? | 跳转链接 |
| sortOrder | int | 排序号 |

---

### 1.2 新增轮播图

```
POST /api/admin/home/carousel
```

**请求体：**

```json
{
  "imageUrl": "/images/farm/xxx.jpg",
  "linkUrl": "/pages/about/about",
  "sortOrder": 1
}
```

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| imageUrl | string | 是 | 图片地址 |
| linkUrl | string | 否 | 跳转链接 |
| sortOrder | int | 否 | 排序号（默认 0） |

> `title` 字段可在请求中传入，但当前版本暂不存储。

**响应：**

```json
{
  "code": 0,
  "message": "success",
  "data": {
    "id": 1
  }
}
```

---

### 1.3 修改轮播图

```
PUT /api/admin/home/carousel/{id}
```

**请求体：**

```json
{
  "imageUrl": "/images/farm/yyy.jpg",
  "linkUrl": "/pages/about/about",
  "sortOrder": 2
}
```

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| imageUrl | string | 否 | 图片地址（不传则不修改） |
| linkUrl | string | 否 | 跳转链接（不传则不修改） |
| sortOrder | int | 否 | 排序号（不传则不修改） |

**响应：** `{ "code": 0, "message": "success", "data": null }`

---

### 1.4 删除轮播图

```
DELETE /api/admin/home/carousel/{id}
```

**响应：**

```json
// 成功
{ "code": 0, "message": "success", "data": null }

// 不存在
{ "code": 404, "message": "轮播图不存在", "data": null }
```

---

### 1.5 轮播图排序

```
POST /api/admin/home/carousel/sort
```

**请求体：**

```json
{
  "ids": [3, 1, 2]
}
```

`ids` 数组的顺序即为新的排序顺序，服务端将依次设置 `SortOrder` 为 1, 2, 3...

**响应：** `{ "code": 0, "message": "success", "data": null }`

---

## 二、视频管理

### 2.1 获取视频列表

```
GET /api/admin/home/videos
```

**响应示例：**

```json
{
  "code": 0,
  "message": "success",
  "data": {
    "records": [
      {
        "id": 1,
        "videoUrl": "/api/file/video/xxx.mp4",
        "sortOrder": 1
      }
    ]
  }
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| id | long | 视频 ID |
| videoUrl | string | 视频地址 |
| sortOrder | int | 排序号 |

---

### 2.2 新增视频

```
POST /api/admin/home/video
```

**请求体：**

```json
{
  "videoUrl": "/api/file/video/xxx.mp4",
  "sortOrder": 1
}
```

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| videoUrl | string | 是 | 视频地址 |
| sortOrder | int | 否 | 排序号（默认 0） |

> `title`、`coverUrl` 字段可在请求中传入，但当前版本暂不存储。

**响应：**

```json
{
  "code": 0,
  "message": "success",
  "data": {
    "id": 1
  }
}
```

---

### 2.3 修改视频

```
PUT /api/admin/home/video/{id}
```

**请求体：**

```json
{
  "videoUrl": "/api/file/video/yyy.mp4",
  "sortOrder": 2
}
```

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| videoUrl | string | 否 | 视频地址（不传则不修改） |
| sortOrder | int | 否 | 排序号（不传则不修改） |

**响应：** `{ "code": 0, "message": "success", "data": null }`

---

### 2.4 删除视频

```
DELETE /api/admin/home/video/{id}
```

**响应：**

```json
// 成功
{ "code": 0, "message": "success", "data": null }

// 不存在
{ "code": 404, "message": "视频不存在", "data": null }
```

---

### 2.5 视频排序

```
POST /api/admin/home/video/sort
```

**请求体：**

```json
{
  "ids": [3, 1, 2]
}
```

`ids` 数组的顺序即为新的排序顺序，服务端将依次设置 `SortOrder` 为 1, 2, 3...

**响应：** `{ "code": 0, "message": "success", "data": null }`

---

## 三、农场简介管理

### 3.1 获取农场简介

```
GET /api/admin/home/farm-intro
```

**响应示例：**

```json
{
  "code": 0,
  "message": "success",
  "data": {
    "name": "能记家庭农场",
    "introduction": "我们的农场位于风景秀丽的乡村...",
    "philosophy": "我们秉承自然、健康、可持续的发展理念...",
    "image": "/images/farm/farm_intro.jpg",
    "contact": {
      "address": "江苏省南京市溧水区能记农场",
      "phone": "138-1234-5678",
      "email": "info@nengjifarm.com"
    }
  }
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| name | string | 农场名称 |
| introduction | string | 农场简介 |
| philosophy | string | 农场理念 |
| image | string | 农场图片地址 |
| contact | object | 联系方式 |
| contact.address | string | 地址 |
| contact.phone | string | 电话 |
| contact.email | string | 邮箱 |

数据来源为 `sys_config` 表的以下配置键：

| 配置键 | 响应字段 | 说明 |
|--------|---------|------|
| farm_name | name | 农场名称 |
| farm_introduction | introduction | 农场简介 |
| farm_philosophy | philosophy | 农场理念 |
| farm_image | image | 农场图片地址 |
| farm_contact | contact | 联系方式 JSON |

---

### 3.2 修改农场简介

```
POST /api/admin/home/farm-intro
```

**请求体：**

```json
{
  "name": "能记家庭农场",
  "introduction": "新的农场简介内容...",
  "philosophy": "新的农场理念...",
  "image": "/images/farm/farm_intro.jpg",
  "contact": {
    "address": "江苏省南京市溧水区能记农场",
    "phone": "138-1234-5678",
    "email": "info@nengjifarm.com"
  }
}
```

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| name | string | 否 | 农场名称（不传则不修改） |
| introduction | string | 否 | 农场简介（不传则不修改） |
| philosophy | string | 否 | 农场理念（不传则不修改） |
| image | string | 否 | 农场图片地址（不传则不修改） |
| contact | object | 否 | 联系方式（不传则不修改） |
| contact.address | string | 否 | 地址 |
| contact.phone | string | 否 | 电话 |
| contact.email | string | 否 | 邮箱 |

各字段分别独立更新 `sys_config` 表中对应配置键的值（新增或覆盖）。`image` 字段存入 `farm_image` 键。

**响应：** `{ "code": 0, "message": "success", "data": null }`

---

## 四、注意事项

1. **认证方式**：所有接口需携带管理员 Token，通过 `token` 请求头传递
2. **排序机制**：`sort` 接口按传入的 id 数组顺序依次设置为 1, 2, 3...
3. **图片/视频上传**：先调用 `POST /api/common/upload` 获取文件地址，再传入对应字段
4. **数据库兼容性**：本接口不修改数据库结构，`carousel` 和 `videos` 表字段保持不变
5. **响应格式**：全部使用 `ApiResult.Success()` / `ApiResult.Fail()` 统一包装
