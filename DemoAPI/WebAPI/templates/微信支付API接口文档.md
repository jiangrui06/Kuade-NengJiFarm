# 微信支付 API 接口文档

> 适用项目：能记农场小程序后端  
> 基础地址：`http://192.168.203.56`  
> 支付模式：微信支付 APIv2 + 小程序 JSAPI 支付  
> 签名方式：默认 `HMAC-SHA256`  
> 统一返回格式：`code = 0` 表示成功，非 0 表示失败。  
> 安全提醒：商户号、APIv2 密钥、AppSecret 等敏感配置不要提交到公开仓库，生产环境建议使用环境变量或密钥管理服务。

## 1. 支付流程概览

### 小程序支付主流程

1. 前端创建订单，拿到 `orderId`。
2. 前端调用 `POST /api/pay/jsapi`。
3. 后端校验订单、用户 openid、金额，然后请求微信支付 `pay/unifiedorder`。
4. 后端返回小程序 `wx.requestPayment` 需要的 `payParams`。
5. 前端调用 `wx.requestPayment(payParams)` 拉起微信支付。
6. 用户完成支付后，微信异步请求 `POST /api/pay/notify`。
7. 后端验签、校验金额、更新订单支付状态。
8. 前端可调用 `POST /api/pay/query-payment-status` 主动查单兜底同步状态。

### 前端调用关系

```javascript
api.pay.createJsapi(orderId)
  .then(data => wx.requestPayment(data.payParams))
  .then(() => api.pay.queryStatus(orderId));
```

---

## 2. 获取支付方式

<details open>
<summary><strong>GET /api/pay/methods</strong> - 获取可用支付方式</summary>

### 中文注释
- 用于支付页展示支付方式。
- 当前只返回微信小程序 JSAPI 支付。

### 是否鉴权
- 否

### 请求参数

无。

### 请求示例

```http
GET /api/pay/methods
```

### 成功响应

```json
{
  "code": 0,
  "message": "success",
  "data": [
    {
      "id": 1,
      "name": "微信支付",
      "icon": "wechat-pay",
      "description": "小程序 JSAPI 支付"
    }
  ]
}
```

</details>

---

## 3. 创建小程序支付参数

<details open>
<summary><strong>POST /api/pay/jsapi</strong> - 创建微信 JSAPI 支付</summary>

### 中文注释
- 用于小程序支付页发起真实微信支付。
- 后端会根据订单金额创建微信预支付订单。
- 返回的 `payParams` 可直接传给 `wx.requestPayment`。
- 如果订单已经支付，则不会再次创建微信预支付订单，会直接返回已支付状态。

### 是否鉴权
- 是

### 请求参数

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| orderId | number | 是 | 订单 ID |
| description | string | 否 | 支付描述，空值时后端默认使用订单号生成描述 |

### 请求示例

```json
{
  "orderId": 80,
  "description": "能记农场订单 80"
}
```

### 成功响应：待支付订单

```json
{
  "code": 0,
  "message": "预支付创建成功",
  "data": {
    "orderId": 80,
    "orderNumber": "202604151154180",
    "paymentStatus": 0,
    "amount": 9.9,
    "payParams": {
      "appId": "wx986e22f241e13ba2",
      "timeStamp": "1776300000",
      "nonceStr": "1f4adcf0d1a44c1289f6d2f72e1d6a80",
      "package": "prepay_id=wx161234567890abcdef",
      "signType": "HMAC-SHA256",
      "paySign": "A1B2C3D4..."
    }
  }
}
```

### 成功响应：订单已支付

```json
{
  "code": 0,
  "message": "订单已支付",
  "data": {
    "orderId": 80,
    "orderNumber": "202604151154180",
    "paymentStatus": 1,
    "paymentTime": "2026-04-15 11:54:21",
    "amount": 9.9
  }
}
```

### 失败说明

| code | 说明 |
| --- | --- |
| 400 | 请求参数错误，或用户缺少微信 openid |
| 404 | 订单不存在 |
| -1 | 创建微信预支付订单失败、配置缺失、微信返回错误等 |

### 后端校验规则

| 校验项 | 说明 |
| --- | --- |
| 订单归属 | 只能支付当前登录用户自己的订单 |
| 用户 openid | 当前用户必须存在 `WxOpenId` |
| 支付金额 | 订单实际支付金额必须大于 0 |
| 商户订单号 | 使用订单表中的 `OrderNumber` 作为 `out_trade_no` |
| 回调地址 | 默认由当前请求 Host 拼接 `/api/pay/notify`，生产建议配置公网 HTTPS `WeChat:NotifyUrl` |

</details>

---

## 4. 兼容创建支付参数

<details>
<summary><strong>POST /api/pay/initiate-payment</strong> - 兼容发起支付接口</summary>

### 中文注释
- 兼容旧前端命名。
- 内部逻辑完全复用 `POST /api/pay/jsapi`。

### 是否鉴权
- 是

### 请求参数

同 `POST /api/pay/jsapi`。

### 请求示例

```json
{
  "orderId": 80,
  "description": "能记农场订单 80"
}
```

### 成功响应

同 `POST /api/pay/jsapi`。

</details>

---

## 5. 查询本地支付状态

<details open>
<summary><strong>GET /api/pay/status</strong> - 查询本地订单支付状态</summary>

### 中文注释
- 查询当前数据库中的订单支付状态。
- 不主动请求微信查单。
- 适用于进入支付页前、支付后刷新 UI。

### 是否鉴权
- 是

### 请求参数

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| orderId | number | 是 | 订单 ID |

### 请求示例

```http
GET /api/pay/status?orderId=80
Authorization: Bearer {token}
```

### 成功响应

```json
{
  "code": 0,
  "message": "success",
  "data": {
    "orderId": 80,
    "orderNumber": "202604151154180",
    "orderStatus": "paid",
    "paymentStatus": 1,
    "paid": true,
    "paymentMethod": 1,
    "paymentTime": "2026-04-15 11:54:21",
    "amount": 9.9
  }
}
```

### 失败说明

| code | 说明 |
| --- | --- |
| 400 | `orderId` 参数错误 |
| 404 | 订单不存在 |
| -1 | 查询失败 |

</details>

---

## 6. 主动查询微信支付状态

<details open>
<summary><strong>POST /api/pay/query-payment-status</strong> - 微信查单并同步状态</summary>

### 中文注释
- 用于支付成功后的兜底查单。
- 如果微信返回支付成功，后端会校验金额并把订单更新为已支付。
- 推荐在 `wx.requestPayment` 成功回调后调用一次。

### 是否鉴权
- 是

### 请求参数

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| orderId | number | 是 | 订单 ID |

### 请求示例

```json
{
  "orderId": 80
}
```

### 成功响应

```json
{
  "code": 0,
  "message": "success",
  "data": {
    "orderId": 80,
    "orderNumber": "202604151154180",
    "orderStatus": "paid",
    "paymentStatus": 1,
    "paid": true,
    "paymentMethod": 1,
    "paymentTime": "2026-04-15 11:54:21",
    "amount": 9.9
  }
}
```

### 失败说明

| code | 说明 |
| --- | --- |
| 400 | 请求参数错误 |
| 404 | 订单不存在 |
| -1 | 微信查单失败、金额不匹配、同步状态失败 |

### 金额校验

后端会把订单 `ActualPayment` 转为分，并与微信返回的 `total_fee` 对比：

```text
订单金额 9.90 元 -> 990 分
微信 total_fee 必须等于 990
```

金额不一致时，后端不会把订单标记为已支付。

</details>

---

## 7. 微信支付结果通知

<details open>
<summary><strong>POST /api/pay/notify</strong> - 微信支付异步回调</summary>

### 中文注释
- 此接口由微信支付服务器调用，不由小程序主动调用。
- 微信会以 XML 格式提交支付结果。
- 后端会进行 APIv2 签名校验、支付结果校验、金额校验，然后更新订单状态。
- 返回内容必须是微信支付 APIv2 要求的 XML。

### 是否鉴权
- 否

### 请求 Content-Type

```http
text/xml
```

### 微信请求示例

```xml
<xml>
  <appid><![CDATA[wx986e22f241e13ba2]]></appid>
  <mch_id><![CDATA[1107056517]]></mch_id>
  <nonce_str><![CDATA[abc123]]></nonce_str>
  <sign><![CDATA[...]]></sign>
  <sign_type><![CDATA[HMAC-SHA256]]></sign_type>
  <return_code><![CDATA[SUCCESS]]></return_code>
  <result_code><![CDATA[SUCCESS]]></result_code>
  <openid><![CDATA[oUpF8uMuAJO_M2pxb1Q9zNjWeS6o]]></openid>
  <trade_type><![CDATA[JSAPI]]></trade_type>
  <bank_type><![CDATA[OTHERS]]></bank_type>
  <total_fee>990</total_fee>
  <transaction_id><![CDATA[4200000000202604151234567890]]></transaction_id>
  <out_trade_no><![CDATA[202604151154180]]></out_trade_no>
  <time_end><![CDATA[20260415115421]]></time_end>
</xml>
```

### 成功响应

```xml
<xml>
  <return_code><![CDATA[SUCCESS]]></return_code>
  <return_msg><![CDATA[OK]]></return_msg>
</xml>
```

### 失败响应

```xml
<xml>
  <return_code><![CDATA[FAIL]]></return_code>
  <return_msg><![CDATA[ORDER_NOT_FOUND]]></return_msg>
</xml>
```

### 回调处理规则

| 规则 | 说明 |
| --- | --- |
| 验签 | 使用 APIv2 Key 校验 XML 中的 `sign` |
| 支付结果 | `return_code` 和 `result_code` 都必须为 `SUCCESS` |
| 订单匹配 | 使用 `out_trade_no` 匹配本地订单 `OrderNumber` |
| 金额校验 | 微信 `total_fee` 必须等于本地订单实际支付金额，单位：分 |
| 幂等处理 | 订单已支付时直接跳过重复更新 |
| 成功回包 | 返回 `SUCCESS` 后微信不会继续重试 |

</details>

---

## 8. 获取待支付订单信息

<details>
<summary><strong>GET /api/pay/info</strong> - 获取支付页展示信息</summary>

### 中文注释
- 用于支付页展示订单金额、收货信息、商品信息。
- 不创建微信预支付订单。

### 是否鉴权
- 是

### 请求参数

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| orderId | number | 是 | 订单 ID |

### 请求示例

```http
GET /api/pay/info?orderId=80
Authorization: Bearer {token}
```

### 成功响应

```json
{
  "code": 0,
  "message": "success",
  "data": {
    "orderId": 80,
    "orderNumber": "202604151154180",
    "totalAmount": 9.9,
    "actualAmount": 9.9,
    "discountAmount": 0,
    "paymentStatus": 0,
    "paymentTime": null,
    "paymentMethod": 0,
    "userInfo": {
      "name": "张三",
      "phone": "138****8000"
    },
    "addressInfo": {
      "contactPerson": "张三",
      "contactNumber": "138****8000",
      "shippingAddress": "广东省广州市越秀区农科街道新河"
    },
    "orderItems": [
      {
        "commodityId": 1,
        "name": "农家橘子",
        "image": "farm_0000000000010.jpg",
        "price": 9.9,
        "actualPrice": 9.9,
        "count": 1,
        "subtotal": 9.9
      }
    ]
  }
}
```

</details>

---

## 9. 小程序前端调用示例

### 创建支付并调起微信支付

```javascript
api.pay.createJsapi(orderId, {
  description: `能记农场订单 ${orderId}`
})
  .then((data) => {
    if (data.paymentStatus === 1 && !data.payParams) {
      return null;
    }

    return new Promise((resolve, reject) => {
      wx.requestPayment({
        timeStamp: String(data.payParams.timeStamp),
        nonceStr: data.payParams.nonceStr,
        package: data.payParams.package,
        signType: data.payParams.signType,
        paySign: data.payParams.paySign,
        success: resolve,
        fail: reject
      });
    });
  })
  .then(() => api.pay.queryStatus(orderId))
  .then((status) => {
    if (status && status.paid) {
      wx.showToast({
        title: '支付成功',
        icon: 'success'
      });
    }
  });
```

### 查询支付状态

```javascript
api.pay.getStatus(orderId).then(status => {
  console.log('本地支付状态', status);
});
```

### 主动向微信查单

```javascript
api.pay.queryStatus(orderId).then(status => {
  console.log('微信查单同步结果', status);
});
```

---

## 10. 配置项说明

配置节点：`WeChat`

| 配置项 | 必填 | 说明 |
| --- | --- | --- |
| AppId | 是 | 小程序 AppId |
| AppSecret | 是 | 小程序 AppSecret，用于登录等微信能力 |
| MchId | 是 | 微信支付商户号 |
| ApiV2Key | 是 | 微信支付 APIv2 密钥，用于签名和验签 |
| SignType | 否 | 签名方式，默认 `HMAC-SHA256`，也兼容 `MD5` |
| NotifyUrl | 生产必填 | 微信支付异步通知地址，必须是公网可访问 URL，生产建议 HTTPS |

### appsettings 示例

```json
{
  "WeChat": {
    "AppId": "wx986e22f241e13ba2",
    "AppSecret": "your-app-secret",
    "MchId": "1107056517",
    "NotifyUrl": "https://your-domain.com/api/pay/notify",
    "ApiV2Key": "your-api-v2-key",
    "SignType": "HMAC-SHA256"
  }
}
```

### 环境变量示例

```powershell
$env:WeChat__MchId="1107056517"
$env:WeChat__ApiV2Key="your-api-v2-key"
$env:WeChat__NotifyUrl="https://your-domain.com/api/pay/notify"
```

---

## 11. 常见问题

### 1. `当前用户缺少微信 openid`

原因：用户没有通过微信登录，或数据库 `user.wx_openid` 为空。  
处理：先完成微信登录并保存 openid。

### 2. `WeChat:NotifyUrl must be a public HTTPS URL`

原因：创建支付时无法得到可用回调地址。  
处理：生产环境配置 `WeChat:NotifyUrl=https://你的域名/api/pay/notify`。

### 3. 支付成功但订单仍显示未支付

可能原因：
- 微信异步通知未到达后端。
- 本地服务没有公网回调地址。
- 金额校验失败。
- 回调验签失败。

处理方式：
- 前端支付成功后调用 `POST /api/pay/query-payment-status`。
- 检查后端日志中的微信回调错误。
- 确认商户平台回调地址能访问后端。

### 4. 金额不匹配

后端以订单表 `ActualPayment` 为准，并转换为分与微信 `total_fee` 对比。  
如果订单金额发生变化，需要重新创建订单或重新发起支付。
