# 活动类型查询 API 接口文档

> 版本：v1.0
> 日期：2026-05-26
> 基础路径：`/api/activity-manage`
> Controller：`ActivityManageController`

---

## 背景

活动新增/编辑页面的「活动分类」下拉选项之前在前端硬编码（采摘体验、亲子研学），新增分类必须修改前端代码。

**解决方案：** 后端新增活动类型查询接口，前端动态获取分类列表。

---

## 1. 获取活动类型列表

### 1.1 接口地址

- **URL：** `/api/activity-manage/types`
- **Method：** `GET`

### 1.2 请求参数

无

### 1.3 响应结构

```json
{
  "code": 200,
  "message": "操作成功",
  "data": [
    {
      "typeId": 1,
      "typeName": "采摘体验"
    },
    {
      "typeId": 2,
      "typeName": "亲子研学"
    }
  ]
}
```

### 1.4 字段说明

| 字段名 | 类型 | 说明 |
|--------|------|------|
| `typeId` | int | 类型 ID（`activity_type.activity_type_id`） |
| `typeName` | string | 类型名称（`activity_type.type_name`，展示在下拉框中） |

### 1.5 数据来源

从数据库 `activity_type` 表动态读取，按 `activity_type_id` 升序排列：

```sql
SELECT activity_type_id, type_name FROM activity_type ORDER BY activity_type_id
```

当前数据库数据：

| typeId | typeName |
|--------|----------|
| 1 | 采摘体验 |
| 2 | 亲子研学 |

> 如需新增分类，只需向 `activity_type` 表插入数据即可，无需修改代码。

### 1.6 异常响应

```json
{
  "code": 500,
  "message": "获取活动类型列表失败",
  "data": null
}
```

---

## 2. 后端改动文件

| 文件 | 改动内容 |
|------|----------|
| `Controllers/ActivityManageController.cs` | 新增 `GET /api/activity-manage/types` 端点 |

---

## 3. 前端对接

### 3.1 修改文件

- `coupon-add.html`（新增页）
- `coupon-edit.html`（编辑页）

### 3.2 修改内容

删除 `data()` 中的硬编码 `activityTypes`：

```javascript
// 删除
activityTypes: [
    { value: '采摘体验', label: '采摘体验' },
    { value: '亲子研学', label: '亲子研学' }
],
```

改为空数组：

```javascript
activityTypes: [],
```

新增加载方法：

```javascript
async loadActivityTypes() {
    try {
        const response = await fetch(FARMAPI_URL('/api/activity-manage/types'), {
            method: 'GET',
            headers: this.getAuthHeaders()
        });
        const result = await this.parseResponseData(response);
        if (response.ok && this.isSuccessResponse(result)) {
            const list = Array.isArray(result.data) ? result.data : [];
            this.activityTypes = list.map(t => ({ value: t.typeName, label: t.typeName }));
        }
    } catch (error) {
        console.error('获取活动类型列表失败:', error);
    }
}
```

在 `mounted()` 中追加调用：

```javascript
mounted() {
    this.loadStatuses();
    this.loadActivityTypes();  // ← 新增
}
```

---

## 4. 兼容说明

- 此接口为新增接口，不与现有接口冲突
- 若接口返回空列表或请求失败，下拉框将无选项可选，建议静态保留至少「请选择分类」一项兜底
