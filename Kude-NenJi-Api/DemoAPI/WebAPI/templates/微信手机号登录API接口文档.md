# 微信手机号登录 API 接口文档

> 适用项目：能记农场小程序后端  
> 基础地址：`http://192.168.203.56`  
> 统一返回格式：`code = 0` 表示成功，非 0 表示失败

## 1. 接口说明

当前登录相关接口分为两类：

1. 一键登录（保留原逻辑，不获取手机号）
2. 手机号快捷登录（新增，独立接口）

---

## 2. 一键登录（原接口，已保留）

### 2.1 接口信息

- 方法：`POST`
- 路径：`/api/Auth/wxlogin`
- 鉴权：否

### 2.2 请求参数

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| code | string | 是 | `wx.login` 获取的 code |
| nickname | string | 否 | 微信昵称（可选） |
| avatar | string | 否 | 微信头像（可选） |

### 2.3 请求示例

```json
{
  "code": "0e1234567890abcdef",
  "nickname": "微信用户",
  "avatar": "https://..."
}
```

### 2.4 成功响应示例

```json
{
  "code": 0,
  "message": "success",
  "data": {
    "token": "eyJhbGciOiJIUzI1NiIs...",
    "isNewUser": false,
    "user_id": 9,
    "user_guid": "f2d2f4d8d9e64f0b9a7a63f3431d9a11",
    "register_time": "2026-04-17T10:12:30",
    "openid": "o8xw95abcde12345",
    "phone_number": ""
  }
}
```

---

## 3. 微信手机号快捷登录（新增接口）

### 3.1 接口信息

- 方法：`POST`
- 路径：`/api/Auth/wx-phone-login`
- 鉴权：否

### 3.2 适用场景

- 用户点击“手机号登录”按钮
- 前端先拿 `wx.login` 的 `code`，再拿 `getPhoneNumber` 的 `phoneCode`
- 后端通过 `phoneCode` 获取微信手机号并登录

### 3.3 请求参数

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| code | string | 是 | `wx.login` 获取的 code |
| phoneCode | string | 是 | 小程序 `getPhoneNumber` 回调中的 `detail.code` |
| nickname | string | 否 | 微信昵称（可选） |
| avatar | string | 否 | 微信头像（可选） |

### 3.4 请求示例

```json
{
  "code": "0eabcdef1234567890",
  "phoneCode": "1f1234567890abcdef",
  "nickname": "张三",
  "avatar": "https://..."
}
```

### 3.5 成功响应示例

```json
{
  "code": 0,
  "message": "success",
  "data": {
    "token": "eyJhbGciOiJIUzI1NiIs...",
    "isNewUser": false,
    "user_id": 9,
    "user_guid": "f2d2f4d8d9e64f0b9a7a63f3431d9a11",
    "register_time": "2026-04-17T10:12:30",
    "openid": "o8xw95abcde12345",
    "phone_number": "13800138000"
  }
}
```

### 3.6 失败响应示例

```json
{
  "code": 1,
  "message": "获取手机号失败",
  "data": null
}
```

### 3.7 常见错误码

| code | 说明 |
| --- | --- |
| 0 | 成功 |
| 1 | 业务失败（参数缺失、获取手机号失败、配置缺失等） |
| 409 | 手机号已绑定其他微信账号 |
| 40029 等 | 微信 `jscode2session` 返回的 errcode（会透传） |

---

## 4. 旧接口（登录后补绑手机号）

### 4.1 接口信息

- 方法：`POST`
- 路径：`/api/Auth/phone`
- 鉴权：是（需要 Bearer Token）

### 4.2 请求参数

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| code | string | 是 | 小程序 `getPhoneNumber` 回调中的 `detail.code` |

---

## 5. 前端调用示例（小程序）

```javascript
// 1) 获取微信登录 code
wx.login({
  success: (loginRes) => {
    const code = loginRes.code;

    // 2) 用户点了手机号按钮后，拿到 phoneCode
    // e.detail.code => phoneCode
    api.request({
      url: '/api/Auth/wx-phone-login',
      method: 'POST',
      data: {
        code,
        phoneCode
      }
    }).then((data) => {
      wx.setStorageSync('token', data.token || '');
    });
  }
});
```

---

## 6. 注意事项

1. `phoneCode` 为一次性凭证，通常短时有效，请实时调用后端，不要缓存复用。
2. 前端如果用户拒绝手机号授权，`e.detail.code` 为空，需给出提示并中止请求。
3. 一键登录和手机号登录可以并行存在，互不影响。

