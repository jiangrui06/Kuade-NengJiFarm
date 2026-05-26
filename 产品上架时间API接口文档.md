# 产品上架时间（uploadTime）API 接口文档

> 版本：v1.0
> 日期：2026-05-26
> 基础路径：`/api/product`
> Controller：`ProductController`

---

## 问题描述

产品管理页面的「上架时间」列之前始终显示当前服务器时间（`DateTime.Now`），没有正确反映产品的实际上架时间。

## 修复方案

利用 `commodity` 表已有的 `create_time` 列作为上架时间存储字段，后端新增 `UploadTime` 属性映射该列，并在新增/编辑时按业务规则控制更新时机。

---

## 涉及接口

| 接口 | 方法 | 改动说明 |
|------|------|----------|
| `/api/product/list` | GET | `uploadTime` 改为返回 `create_time` 实际存储值 |
| `/api/product/detail` | GET | `uploadTime` 改为返回 `create_time` 实际存储值 |
| `/api/product/add` | POST | 新增时若 status=已上架，记录上架时间到 `create_time` |
| `/api/product/edit` | POST/PUT | 仅当从非上架变为上架时更新上架时间 |

---

## 接口详情

### 1. GET /api/product/list

#### 响应结构（data.records[].uploadTime 字段行为变更）

```json
{
  "code": 0,
  "message": "获取成功",
  "data": {
    "records": [
      {
        "id": "1",
        "name": "农场散养土鸡",
        "price": 88.00,
        "stock": 100,
        "status": "已上架",
        "image": "/images/xxx.jpg",
        "uploadTime": "2026-05-22 14:01",
        "netWeight": 2.5,
        "weightUnit": "kg",
        "productType": "实物"
      }
    ],
    "total": 50,
    "pageNum": 1,
    "pageSize": 15,
    "pages": 4
  }
}
```

**uploadTime 说明：**
- 返回 `commodity` 表 `create_time` 列的格式化值
- 格式：`yyyy-MM-dd HH:mm`
- 不再返回当前服务器时间

---

### 2. GET /api/product/detail

#### 请求参数

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| id | int | 是 | 产品 ID |

#### 响应结构

```json
{
  "code": 0,
  "message": "获取成功",
  "data": {
    "id": "1",
    "name": "农场散养土鸡",
    "price": 88.00,
    "stock": 100,
    "status": "已上架",
    "image": "/images/xxx.jpg",
    "coverImage": "/images/xxx.jpg",
    "carouselMedia": [],
    "netWeight": 2.5,
    "weightUnit": "kg",
    "storageCondition": "冷藏保存",
    "specImages": [],
    "description": "农场散养土鸡，肉质鲜美",
    "uploadTime": "2026-05-22 14:01",
    "productType": "实物"
  }
}
```

---

### 3. POST /api/product/add

#### uploadTime 设置规则

| 场景 | 行为 |
|------|------|
| 新增时 status=已上架 | 自动记录当前时间到 `create_time`（作为上架时间） |
| 新增时 status=已下架 | 不记录上架时间（`create_time` 仅记录创建时间） |

---

### 4. POST|PUT /api/product/edit

#### uploadTime 更新规则

| 旧状态 | 新状态 | uploadTime 行为 |
|--------|--------|----------------|
| 已下架/售罄 | 已上架 | 更新为当前时间 |
| 已上架 | 已上架 | 保持不变 |
| 已上架 | 已下架 | 保持不变 |
| 已下架 | 已下架 | 保持不变 |

---

## 后端改动文件

| 文件 | 改动内容 |
|------|----------|
| `Entities/Manage/Commodity.cs` | 新增 `UploadTime` 属性映射 `create_time` 列 |
| `Services/ProductService.cs` | 列表/详情返回存储值；新增/编辑按规则控制上架时间 |

## 兼容说明

- 不动数据库结构（复用已有 `create_time` 列）
- 已有数据的上架时间 = 原 `create_time`（创建时间）
