# 券类管理 API 接口文档

本文基于当前前端页面逻辑整理，主要参考以下页面：

- [coupon.html](/e:/Kuade-NengJiFarm/web_management/coupon.html)
- [coupon-add.html](/e:/Kuade-NengJiFarm/web_management/coupon-add.html)
- [coupon-edit.html](/e:/Kuade-NengJiFarm/web_management/coupon-edit.html)

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

示例：`2026-04-01 12:00`

### 1.4 券品 ID 格式

前端生成规则：`Q` + 年月日时分秒 + 两位序号

示例：`Q20260409103001`

## 二、券品字段说明

当前前端依赖以下字段：

| 字段 | 类型 | 是否必传 | 示例 | 说明 |
|---|---|---|---|---|
| `id` | String | 编辑/删除/详情必传 | `Q20260409103001` | 券品唯一 ID |
| `name` | String | 是 | `春日草莓采摘券` | 券品名称 |
| `type` | String | 是 | `采摘券` | 券品类型：`研学活动券` / `采摘券` |
| `price` | Number | 是 | `68.00` | 售价（元），精确到小数点后两位 |
| `stock` | Number | 是 | `120` | 库存数量（张） |
| `limitPerOrder` | Number | 是 | `4` | 单次限购数量 |
| `validityPeriod` | Number | 是 | `30` | 有效期数值 |
| `validityUnit` | String | 是 | `天` | 有效期单位：`天` / `月` / `年` |
| `validity` | String | 列表建议返回 | `30天` | 有效期展示文本 |
| `refundRule` | String | 是 | `支持未使用且未核销退款` | 退款规则 |
| `usageRules` | String | 是 | `适用于草莓采摘园区单人入园体验...` | 使用规则 |
| `image` | String | 否 | `https://example.com/coupon-cover.jpg` | 封面图（base64 或 URL） |
| `imageName` | String | 否 | `草莓采摘券.jpg` | 封面图文件名 |
| `carouselMedia` | Object[] | 否 | `[{ "type": "image", "url": "..." }]` | 轮播图/视频，最多 5 个 |
| `soldCount` | Number | 列表建议返回 | `86` | 已售数量 |
| `verifiedCount` | Number | 列表建议返回 | `52` | 已核销数量 |
| `createTime` | String | 列表建议返回 | `2026-04-01 12:00` | 创建时间 |

**carouselMedia 对象结构**：

| 字段 | 类型 | 说明 |
|---|---|---|
| `type` | String | 媒体类型：`image`（图片）或 `video`（视频） |
| `url` | String | 媒体文件地址（base64 或 URL） |
| `thumb` | String | 视频缩略图（仅视频类型需要） |

**refundRule 可选值**：

- `支持未使用且未核销退款`
- `需人工审核退款`
- `不支持退款`

**validityUnit 可选值**：

- `天`
- `月`
- `年`

## 三、接口 1：获取券品列表

| 项目 | 内容 |
|---|---|
| 接口名称 | 获取券品列表 |
| 请求路径 | `/api/coupon/list` |
| 请求方式 | `GET` |
| 接口说明 | 用于 `coupon.html` 首屏加载、搜索、分页、刷新 |

### Query 参数

| 字段 | 是否必传 | 类型 | 示例 | 说明 |
|---|---|---|---|---|
| `pageNum` | 是 | Number | `1` | 页码 |
| `pageSize` | 是 | Number | `15` | 每页条数 |
| `keyword` | 否 | String | `采摘` | 按券品 ID、名称、类型等模糊搜索 |

### 成功响应示例

```json
{
  "code": 200,
  "message": "获取成功",
  "data": {
    "records": [
      {
        "id": "Q20260310071001",
        "name": "春日草莓采摘券",
        "type": "采摘券",
        "price": 68.00,
        "stock": 120,
        "limitPerOrder": 4,
        "validityPeriod": 30,
        "validityUnit": "天",
        "validity": "30天",
        "refundRule": "支持未使用且未核销退款",
        "usageRules": "适用于草莓采摘园区单人入园体验，需到店出示核销码后使用，节假日需提前预约。",
        "image": "",
        "carouselMedia": [],
        "soldCount": 86,
        "verifiedCount": 52,
        "createTime": "2026-03-10 17:07"
      },
      {
        "id": "Q20260312093002",
        "name": "周末亲子采摘券",
        "type": "采摘券",
        "price": 128.00,
        "stock": 80,
        "limitPerOrder": 2,
        "validityPeriod": 45,
        "validityUnit": "天",
        "validity": "45天",
        "refundRule": "需人工审核退款",
        "usageRules": "适用于 2 大 1 小亲子采摘体验，周末及节假日可用，需至少提前 1 天预约。",
        "image": "https://example.com/coupon-cover.jpg",
        "carouselMedia": [
          {
            "type": "image",
            "url": "https://example.com/coupon-banner-1.jpg"
          },
          {
            "type": "video",
            "url": "https://example.com/coupon-video.mp4",
            "thumb": "https://example.com/coupon-video-thumb.jpg"
          }
        ],
        "soldCount": 51,
        "verifiedCount": 30,
        "createTime": "2026-03-12 09:30"
      }
    ],
    "total": 8,
    "pageNum": 1,
    "pageSize": 15,
    "pages": 1
  }
}
```

### 统计卡说明

列表页顶部统计卡数据直接来源于 `records` 数组汇总：

| 统计项 | 计算方式 |
|---|---|
| 券品总数 | `records.length` |
| 剩余库存 | `records.reduce((sum, c) => sum + c.stock, 0)` |
| 累计已售 | `records.reduce((sum, c) => sum + c.soldCount, 0)` |
| 累计已核销 | `records.reduce((sum, c) => sum + c.verifiedCount, 0)` |

## 四、接口 2：获取券品详情

| 项目 | 内容 |
|---|---|
| 接口名称 | 获取券品详情 |
| 请求路径 | `/api/coupon/detail` |
| 请求方式 | `GET` |
| 接口说明 | 用于 `coupon-edit.html?id=券品ID` 回填表单 |

### Query 参数

| 字段 | 是否必传 | 类型 | 示例 | 说明 |
|---|---|---|---|---|
| `id` | 是 | String | `Q20260310071001` | 券品 ID |

### 成功响应示例

```json
{
  "code": 200,
  "message": "获取成功",
  "data": {
    "id": "Q20260310071001",
    "name": "春日草莓采摘券",
    "type": "采摘券",
    "price": 68.00,
    "stock": 120,
    "limitPerOrder": 4,
    "validityPeriod": 30,
    "validityUnit": "天",
    "validity": "30天",
    "refundRule": "支持未使用且未核销退款",
    "usageRules": "适用于草莓采摘园区单人入园体验，需到店出示核销码后使用，节假日需提前预约。",
    "image": "",
    "imageName": "",
    "carouselMedia": [
      {
        "type": "image",
        "url": "https://example.com/coupon-banner-1.jpg"
      },
      {
        "type": "video",
        "url": "https://example.com/coupon-video.mp4",
        "thumb": "https://example.com/coupon-video-thumb.jpg"
      }
    ],
    "soldCount": 86,
    "verifiedCount": 52,
    "createTime": "2026-03-10 17:07"
  }
}
```

## 五、接口 3：新增券品

| 项目 | 内容 |
|---|---|
| 接口名称 | 新增券品 |
| 请求路径 | `/api/coupon/add` |
| 请求方式 | `POST` |
| 接口说明 | 用于 `coupon-add.html` 提交新增表单 |

### Body 参数

| 字段 | 是否必传 | 类型 | 示例 | 说明 |
|---|---|---|---|---|
| `name` | 是 | String | `春日草莓采摘券` | 券品名称 |
| `type` | 是 | String | `采摘券` | 券品类型：`研学活动券` / `采摘券` |
| `price` | 是 | Number | `68.00` | 售价 |
| `stock` | 是 | Number | `120` | 库存数量 |
| `limitPerOrder` | 是 | Number | `4` | 单次限购数量 |
| `validityPeriod` | 是 | Number | `30` | 有效期数值 |
| `validityUnit` | 是 | String | `天` | 有效期单位：`天` / `月` / `年` |
| `refundRule` | 是 | String | `支持未使用且未核销退款` | 退款规则 |
| `usageRules` | 是 | String | `适用于草莓采摘园区...` | 使用规则 |
| `image` | 否 | String | `https://example.com/cover.jpg` | 封面图 |
| `carouselMedia` | 否 | Object[] | `[{ "type": "image", "url": "..." }]` | 轮播图/视频数组 |

### 请求示例

```json
{
  "name": "春日草莓采摘券",
  "type": "采摘券",
  "price": 68.00,
  "stock": 120,
  "limitPerOrder": 4,
  "validityPeriod": 30,
  "validityUnit": "天",
  "refundRule": "支持未使用且未核销退款",
  "usageRules": "适用于草莓采摘园区单人入园体验，需到店出示核销码后使用，节假日需提前预约。",
  "image": "https://example.com/coupon-cover.jpg",
  "carouselMedia": [
    {
      "type": "image",
      "url": "https://example.com/coupon-banner-1.jpg"
    },
    {
      "type": "video",
      "url": "https://example.com/coupon-video.mp4",
      "thumb": "https://example.com/coupon-video-thumb.jpg"
    }
  ]
}
```

### 成功响应示例

```json
{
  "code": 200,
  "message": "新增成功",
  "data": {
    "id": "Q20260413103001"
  }
}
```

### 前端校验规则

| 字段 | 校验规则 |
|---|---|
| `name` | 不能为空 |
| `price` | 必须大于 0 |
| `stock` | 必须大于等于 0 |
| `limitPerOrder` | 必须大于等于 1 |
| `validityPeriod` | 必须大于等于 1 |
| `usageRules` | 不能为空 |

## 六、接口 4：编辑券品

| 项目 | 内容 |
|---|---|
| 接口名称 | 编辑券品 |
| 请求路径 | `/api/coupon/edit` |
| 请求方式 | `PUT` |
| 接口说明 | 用于 `coupon-edit.html` 提交修改结果 |

### Body 参数

| 字段 | 是否必传 | 类型 | 示例 | 说明 |
|---|---|---|---|---|
| `id` | 是 | String | `Q20260310071001` | 券品 ID |
| `name` | 是 | String | `春日草莓采摘券` | 券品名称 |
| `type` | 是 | String | `采摘券` | 券品类型 |
| `price` | 是 | Number | `68.00` | 售价 |
| `stock` | 是 | Number | `100` | 库存数量 |
| `limitPerOrder` | 是 | Number | `4` | 单次限购数量 |
| `validityPeriod` | 是 | Number | `30` | 有效期数值 |
| `validityUnit` | 是 | String | `天` | 有效期单位 |
| `refundRule` | 是 | String | `支持未使用且未核销退款` | 退款规则 |
| `usageRules` | 是 | String | `适用于草莓采摘园区...` | 使用规则 |
| `image` | 否 | String | `https://example.com/cover.jpg` | 封面图 |
| `carouselMedia` | 否 | Object[] | `[{ "type": "image", "url": "..." }]` | 轮播图/视频数组 |

### 请求示例

```json
{
  "id": "Q20260310071001",
  "name": "春日草莓采摘券",
  "type": "采摘券",
  "price": 68.00,
  "stock": 100,
  "limitPerOrder": 4,
  "validityPeriod": 30,
  "validityUnit": "天",
  "refundRule": "支持未使用且未核销退款",
  "usageRules": "适用于草莓采摘园区单人入园体验，需到店出示核销码后使用，节假日需提前预约。",
  "image": "https://example.com/coupon-cover.jpg",
  "carouselMedia": [
    {
      "type": "image",
      "url": "https://example.com/coupon-banner-1.jpg"
    }
  ]
}
```

### 前端校验规则

与新增接口相同。

## 七、接口 5：删除券品

| 项目 | 内容 |
|---|---|
| 接口名称 | 删除券品 |
| 请求路径 | `/api/coupon/delete` |
| 请求方式 | `POST` |
| 接口说明 | 用于列表页单条删除 |

### Body 参数

| 字段 | 是否必传 | 类型 | 示例 | 说明 |
|---|---|---|---|---|
| `id` | 是 | String | `Q20260310071001` | 券品 ID |

### 请求示例

```json
{
  "id": "Q20260310071001"
}
```

## 八、接口 6：批量删除券品

| 项目 | 内容 |
|---|---|
| 接口名称 | 批量删除券品 |
| 请求路径 | `/api/coupon/deleteBatch` |
| 请求方式 | `POST` |

### Body 参数

| 字段 | 是否必传 | 类型 | 示例 | 说明 |
|---|---|---|---|---|
| `ids` | 是 | String[] | `["Q20260310071001","Q20260312093002"]` | 券品 ID 集合 |

### 请求示例

```json
{
  "ids": [
    "Q20260310071001",
    "Q20260312093002"
  ]
}
```

## 九、前端兼容注意事项

- 券品 ID 格式建议与前端保持一致：`Q` + 年月日时分秒 + 两位序号
- `type` 必须为 `研学活动券` 或 `采摘券`，前端不做数字映射
- `refundRule` 建议使用中文字符串，前端不识别数字枚举
- `validityUnit` 建议使用 `天`、`月`、`年`，前端会拼接成 `validity` 字段展示
- `carouselMedia` 最多 5 个，支持图片和视频混合
- `image` 字段如果为空，前端会根据 `type` 自动生成默认封面图
  - 采摘券：橙色渐变 + "采摘体验" + "PICKING VOUCHER"
  - 研学活动券：蓝色渐变 + "研学活动" + "STUDY TOUR"
- 已售数量 `soldCount` 和已核销数量 `verifiedCount` 由后端维护，前端仅做展示
- 列表搜索会匹配：id、name、type、validity、price、stock、limitPerOrder、soldCount、verifiedCount

## 十、通用失败场景

| 场景 | 状态码 | 提示文案 |
|---|---|---|
| token 无效或过期 | `401` | 登录已过期，请重新登录 |
| 权限不足 | `403` | 权限不足，仅管理员可操作 |
| 参数错误 | `400` | 请求参数不完整或格式错误 |
| 券品不存在 | `404` | 券品不存在或已被删除 |
| 编号重复 | `409` | 券品编号已存在，请更换后再保存 |
| 服务异常 | `500` | 服务器异常，请稍后重试 |

## 十一、对接建议

- 如果后端暂不支持 `carouselMedia` 视频存储，前端上传视频时可先生成缩略图 `thumb` 一起提交
- `image` 和 `carouselMedia` 如果后端采用文件上传而非 base64，可额外提供 `/api/common/upload` 接口
- `soldCount` 和 `verifiedCount` 建议后端主动维护，前端不做增减运算
- `createTime` 建议后端统一返回 `yyyy-MM-dd HH:mm` 格式
- 批量操作接口建议支持，便于前端直接升级多选删除功能
