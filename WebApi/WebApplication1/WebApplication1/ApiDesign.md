# 能记农场小程序 WebApi 设计

> 说明：本文件是为小程序配套的 ASP.NET Web API 设计草案，你可以直接按此拆分控制器和接口。

## 一、整体架构建议

- **项目结构**
  - `WebApi/Kuade.NengJiFarm.Api`：ASP.NET Web API 项目
  - `WebApi/Kuade.NengJiFarm.Domain`：实体、领域服务接口
  - `WebApi/Kuade.NengJiFarm.Infrastructure`：EF Core、数据库访问实现
  - `WebApi/Kuade.NengJiFarm.Application`：应用服务（DTO、业务用例）

- **主要领域对象**
  - 用户 `User`（小程序用户）
  - 农产品 `Goods`
  - 地块/农田 `Acre`
  - 购物车项 `CartItem`
  - 订单 `Order` / `OrderItem`
  - 活动 `Activity`

- **统一返回格式（推荐）**
  - `{ code: 0, message: "ok", data: {} }`

## 二、小程序页面与接口对应关系

- `index` 首页
  - 展示推荐农产品、活动列表、轮播图等
- `farm-goods` 农产品列表
  - 按分类/筛选条件加载农产品
- `goods-detail` 商品详情
  - 获取单个农产品详情，加入购物车或立即下单
- `acre` 地块列表
  - 展示用户认养/可认养地块
- `acre-detail` 地块详情
  - 显示地块状态、种植信息、操作记录
- `cart` 购物车
  - 管理购物车商品、提交订单
- `order` 订单列表/详情
  - 查看历史订单、订单详情、支付状态
- `activity` 活动列表/详情
  - 展示农场活动（促销、体验活动等）
- `profile` 个人中心
  - 展示用户信息、地址、统计数据等

## 三、接口设计（按模块）

### 1. 公共/系统相关

- **获取小程序配置**
  - `GET /api/config`
  - 返回：轮播图、首页推荐区块、客服电话等。

### 2. 用户 / 个人中心 (`ProfileController`)

- **获取当前用户信息**
  - `GET /api/profile`
  - 说明：根据登录态（如小程序 OpenId 映射的用户）返回用户信息。

- **更新用户信息**
  - `PUT /api/profile`
  - 请求体：`{ nickName, avatarUrl, phoneNumber, ... }`

- **获取用户统计信息**
  - `GET /api/profile/statistics`
  - 返回：下单次数、认养地块数量、积分等统计。

### 3. 农产品 (`GoodsController`)

- **分页获取农产品列表**
  - `GET /api/goods`
  - 查询参数：`pageIndex`, `pageSize`, `categoryId`, `keyword`

- **获取单个农产品详情**
  - `GET /api/goods/{id}`

- **获取推荐农产品（首页用）**
  - `GET /api/goods/recommend`

### 4. 地块 / 农田 (`AcreController`)

- **获取地块列表**
  - `GET /api/acres`
  - 查询参数：`status`（可认养/已认养）、`pageIndex`, `pageSize`

- **获取单个地块详情**
  - `GET /api/acres/{id}`

- **认养地块**
  - `POST /api/acres/{id}/adopt`
  - 请求体：`{ months, remark }`

- **获取地块种植/操作记录**
  - `GET /api/acres/{id}/logs`

### 5. 购物车 (`CartController`)

- **获取当前用户购物车**
  - `GET /api/cart`

- **加入购物车**
  - `POST /api/cart/items`
  - 请求体：`{ goodsId, quantity, specId? }`

- **更新购物车项数量**
  - `PUT /api/cart/items/{id}`
  - 请求体：`{ quantity }`

- **删除购物车项**
  - `DELETE /api/cart/items/{id}`

- **清空购物车**
  - `DELETE /api/cart`

### 6. 订单 (`OrderController`)

- **创建订单（来自购物车或立即购买）**
  - `POST /api/orders`
  - 请求体示例：
    - `{ items: [{ goodsId, quantity, price, specId? }], addressId, remark }`

- **获取当前用户订单列表**
  - `GET /api/orders`
  - 查询参数：`status`, `pageIndex`, `pageSize`

- **获取订单详情**
  - `GET /api/orders/{id}`

- **取消订单**
  - `POST /api/orders/{id}/cancel`

- **确认收货**
  - `POST /api/orders/{id}/confirm`

### 7. 活动 (`ActivityController`)

- **获取活动列表**
  - `GET /api/activities`
  - 查询参数：`pageIndex`, `pageSize`, `status`

- **获取活动详情**
  - `GET /api/activities/{id}`

### 8. 支付（可选，如接入微信支付）(`PaymentController`)

- **创建支付订单（统一下单）**
  - `POST /api/payments/wechat`
  - 请求体：`{ orderId }`
  - 返回：小程序调起支付需要的参数。

- **支付回调通知（服务端调用）**
  - `POST /api/payments/wechat/notify`

## 四、后续实现建议

- 优先实现：`GoodsController`, `CartController`, `OrderController`，满足下单闭环。
- 然后补充：`AcreController`, `ActivityController`, `ProfileController`，完善农场特色功能。
- 每个控制器建议使用 ASP.NET Core 的 `ApiController` 特性，并统一异常和结果封装。

> 你可以先创建一个 ASP.NET Core Web API 项目放在 `WebApi/Kuade.NengJiFarm.Api`，然后按本文件的控制器和路由去实现，后续如果需要我也可以帮你一步步写具体代码。

