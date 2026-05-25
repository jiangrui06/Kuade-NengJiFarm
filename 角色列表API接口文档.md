# 角色列表 API (`GET /api/back-user/roles`)

## 说明

从 `role` 表查询所有角色数据，用于用户管理中的身份下拉框。

## 请求

```
GET /api/back-user/roles
```

### Headers

| Header | 值 | 必填 |
|--------|------|------|
| Authorization | Bearer {token} | 是 |
| Content-Type | application/json | 否 |

## 成功响应

```json
{
  "code": 200,
  "message": "获取成功",
  "data": {
    "records": [
      { "roleId": 1, "roleName": "普通用户" },
      { "roleId": 2, "roleName": "员工" },
      { "roleId": 3, "roleName": "后勤" },
      { "roleId": 4, "roleName": "厨师" }
    ]
  }
}
```

### 字段说明

| 字段 | 类型 | 说明 |
|------|------|------|
| roleId | int | 角色 ID |
| roleName | string | 角色名称 |

## 错误响应

```json
{
  "code": 400,
  "message": "获取角色列表失败"
}
```
