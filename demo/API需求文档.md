# 农场优选小程序 API 需求文档

## 1. 文档概述

### 1.1 文档目的
本文档旨在定义农场优选小程序的后端API接口规范，为前端开发和后端开发提供明确的接口调用指南，确保前后端开发的一致性和高效性。

### 1.2 术语定义
| 术语 | 解释 |
|------|------|
| 小程序 | 指基于微信平台开发的能记农场小程序 |
| API | 应用程序编程接口，用于前后端数据交互 |
| Token | 用户身份验证令牌 |
| 商品 | 指农场优选平台上销售的农产品 |
| 分类 | 商品的分类信息，如叶菜、番茄、土豆等 |
| 购物车 | 用户添加的待购买商品集合 |

## 2. 接口设计原则

1. **RESTful API设计**：采用RESTful风格设计API，使用HTTP方法（GET、POST、PUT、DELETE）表示操作类型
2. **统一响应格式**：所有API响应采用统一的JSON格式，包含状态码、消息和数据
3. **安全性**：实现用户认证和授权机制，保护用户数据安全
4. **可扩展性**：API设计应考虑未来功能扩展的需求
5. **性能优化**：合理设计API，减少不必要的数据传输和计算

## 3. 接口列表

### 3.1 认证相关接口
| 接口名称 | URL | 方法 | 功能描述 |
|---------|-----|------|----------|
| 一键登录 | /api/auth/login | POST | 用户一键登录 |
| 微信登录 | /api/auth/wechat | POST | 微信授权登录 |
| 手机号登录 | /api/auth/phone | POST | 手机号验证码登录 |
| 登出 | /api/auth/logout | POST | 用户登出 |
| 检查登录状态 | /api/auth/check | GET | 检查用户登录状态 |

### 3.2 首页相关接口
| 接口名称 | URL | 方法 | 功能描述 |
|---------|-----|------|----------|
| 获取首页数据 | /api/home/index | GET | 获取首页轮播图、功能按钮、农场优选商品、热销菜品等数据 |

### 3.3 农场优选相关接口
| 接口名称 | URL | 方法 | 功能描述 |
|---------|-----|------|----------|
| 获取农场优选数据 | /api/farm-goods/index | GET | 获取农场优选页面轮播图、分类和商品数据 |
| 获取分类商品 | /api/farm-goods/category | GET | 根据分类ID获取商品列表 |
| 搜索商品 | /api/farm-goods/search | GET | 根据关键词搜索商品 |

### 3.4 商品相关接口
| 接口名称 | URL | 方法 | 功能描述 |
|---------|-----|------|----------|
| 获取商品详情 | /api/goods/detail | GET | 获取商品详细信息 |

### 3.5 购物车相关接口
| 接口名称 | URL | 方法 | 功能描述 |
|---------|-----|------|----------|
| 获取购物车列表 | /api/cart/list | GET | 获取用户购物车商品列表 |
| 添加商品到购物车 | /api/cart/add | POST | 添加商品到购物车 |
| 更新购物车商品数量 | /api/cart/update | PUT | 更新购物车商品数量 |
| 删除购物车商品 | /api/cart/delete | DELETE | 删除购物车商品 |
| 清空购物车 | /api/cart/clear | DELETE | 清空购物车 |

### 3.6 订单相关接口
| 接口名称 | URL | 方法 | 功能描述 |
|---------|-----|------|----------|
| 创建订单 | /api/order/create | POST | 创建新订单 |
| 获取订单列表 | /api/order/list | GET | 获取用户订单列表 |
| 获取订单详情 | /api/order/detail | GET | 获取订单详细信息 |
| 取消订单 | /api/order/cancel | PUT | 取消订单 |

### 3.7 个人中心相关接口
| 接口名称 | URL | 方法 | 功能描述 |
|---------|-----|------|----------|
| 获取个人信息 | /api/user/profile | GET | 获取用户个人信息 |
| 更新个人信息 | /api/user/profile | PUT | 更新用户个人信息 |
| 获取收货地址列表 | /api/user/address | GET | 获取用户收货地址列表 |
| 添加收货地址 | /api/user/address | POST | 添加新的收货地址 |
| 更新收货地址 | /api/user/address | PUT | 更新收货地址 |
| 删除收货地址 | /api/user/address | DELETE | 删除收货地址 |

## 4. 接口详细设计

### 4.1 认证相关接口

#### 4.1.1 一键登录
- **URL**: `/api/auth/login`
- **方法**: `POST`
- **请求参数**:
  | 参数名 | 类型 | 必填 | 描述 |
  |--------|------|------|------|
  | deviceId | string | 是 | 设备ID |
  | platform | string | 是 | 平台类型（wx小程序） |
  | version | string | 是 | 小程序版本 |

- **响应参数**:
  | 参数名 | 类型 | 描述 |
  |--------|------|------|
  | code | number | 状态码，0表示成功 |
  | message | string | 响应消息 |
  | data | object | 返回数据 |
  | data.token | string | 登录令牌 |
  | data.userInfo | object | 用户信息 |
  | data.userInfo.id | number | 用户ID |
  | data.userInfo.nickname | string | 用户名 |
  | data.userInfo.avatar | string | 头像URL |

#### 4.1.2 微信登录
- **URL**: `/api/auth/wechat`
- **方法**: `POST`
- **请求参数**:
  | 参数名 | 类型 | 必填 | 描述 |
  |--------|------|------|------|
  | code | string | 是 | 微信登录code |
  | encryptedData | string | 是 | 加密数据 |
  | iv | string | 是 | 加密向量 |

- **响应参数**:
  | 参数名 | 类型 | 描述 |
  |--------|------|------|
  | code | number | 状态码，0表示成功 |
  | message | string | 响应消息 |
  | data | object | 返回数据 |
  | data.token | string | 登录令牌 |
  | data.userInfo | object | 用户信息 |
  | data.userInfo.id | number | 用户ID |
  | data.userInfo.nickname | string | 用户名 |
  | data.userInfo.avatar | string | 头像URL |

#### 4.1.3 手机号登录
- **URL**: `/api/auth/phone`
- **方法**: `POST`
- **请求参数**:
  | 参数名 | 类型 | 必填 | 描述 |
  |--------|------|------|------|
  | phone | string | 是 | 手机号 |
  | code | string | 是 | 验证码 |

- **响应参数**:
  | 参数名 | 类型 | 描述 |
  |--------|------|------|
  | code | number | 状态码，0表示成功 |
  | message | string | 响应消息 |
  | data | object | 返回数据 |
  | data.token | string | 登录令牌 |
  | data.userInfo | object | 用户信息 |
  | data.userInfo.id | number | 用户ID |
  | data.userInfo.nickname | string | 用户名 |
  | data.userInfo.avatar | string | 头像URL |

#### 4.1.4 登出
- **URL**: `/api/auth/logout`
- **方法**: `POST`
- **请求头**:
  | 参数名 | 类型 | 必填 | 描述 |
  |--------|------|------|------|
  | Authorization | string | 是 | Bearer token |

- **响应参数**:
  | 参数名 | 类型 | 描述 |
  |--------|------|------|
  | code | number | 状态码，0表示成功 |
  | message | string | 响应消息 |

#### 4.1.5 检查登录状态
- **URL**: `/api/auth/check`
- **方法**: `GET`
- **请求头**:
  | 参数名 | 类型 | 必填 | 描述 |
  |--------|------|------|------|
  | Authorization | string | 是 | Bearer token |

- **响应参数**:
  | 参数名 | 类型 | 描述 |
  |--------|------|------|
  | code | number | 状态码，0表示成功 |
  | message | string | 响应消息 |
  | data | object | 返回数据 |
  | data.isLoggedIn | boolean | 是否已登录 |
  | data.userInfo | object | 用户信息 |
  | data.userInfo.id | number | 用户ID |
  | data.userInfo.nickname | string | 用户名 |
  | data.userInfo.avatar | string | 头像URL |

### 4.2 首页相关接口

#### 4.2.1 获取首页数据
- **URL**: `/api/home/index`
- **方法**: `GET`

- **响应参数**:
  | 参数名 | 类型 | 描述 |
  |--------|------|------|
  | code | number | 状态码，0表示成功 |
  | message | string | 响应消息 |
  | data | object | 返回数据 |
  | data.swiperList | array | 轮播图数据 |
  | data.swiperList[].id | number | 轮播图ID |
  | data.swiperList[].image | string | 轮播图图片URL |
  | data.functionButtons | array | 功能按钮数据 |
  | data.functionButtons[].id | number | 按钮ID |
  | data.functionButtons[].name | string | 按钮名称 |
  | data.functionButtons[].color | string | 按钮颜色 |
  | data.functionButtons[].path | string | 按钮跳转路径 |
  | data.farmGoods | array | 农场优选商品数据 |
  | data.farmGoods[].id | number | 商品ID |
  | data.farmGoods[].name | string | 商品名称 |
  | data.farmGoods[].image | string | 商品图片URL |
  | data.farmGoods[].price | number | 商品价格 |
  | data.farmGoods[].originalPrice | number | 商品原价 |
  | data.farmGoods[].tags | array | 商品标签 |
  | data.farmGoods[].stock | number | 商品库存 |
  | data.hotDishes | array | 热销菜品数据 |
  | data.hotDishes[].id | number | 菜品ID |
  | data.hotDishes[].name | string | 菜品名称 |
  | data.hotDishes[].image | string | 菜品图片URL |
  | data.hotDishes[].price | number | 菜品价格 |
  | data.hotDishes[].tags | array | 菜品标签 |

### 4.3 农场优选相关接口

#### 4.3.1 获取农场优选数据
- **URL**: `/api/farm-goods/index`
- **方法**: `GET`

- **响应参数**:
  | 参数名 | 类型 | 描述 |
  |--------|------|------|
  | code | number | 状态码，0表示成功 |
  | message | string | 响应消息 |
  | data | object | 返回数据 |
  | data.swiperList | array | 轮播图数据 |
  | data.swiperList[].id | number | 轮播图ID |
  | data.swiperList[].image | string | 轮播图图片URL |
  | data.categories | array | 分类数据 |
  | data.categories[].id | string | 分类ID |
  | data.categories[].name | string | 分类名称 |
  | data.categories[].icon | string | 分类图标 |
  | data.categories[].color | string | 分类颜色 |
  | data.todayGoods | array | 今日优选商品数据 |
  | data.todayGoods[].id | number | 商品ID |
  | data.todayGoods[].name | string | 商品名称 |
  | data.todayGoods[].image | string | 商品图片URL |
  | data.todayGoods[].price | number | 商品价格 |
  | data.todayGoods[].originalPrice | number | 商品原价 |
  | data.todayGoods[].stock | number | 商品库存 |
  | data.todayGoods[].tags | array | 商品标签 |
  | data.hotGoods | array | 热门商品数据 |
  | data.hotGoods[].id | number | 商品ID |
  | data.hotGoods[].name | string | 商品名称 |
  | data.hotGoods[].image | string | 商品图片URL |
  | data.hotGoods[].price | number | 商品价格 |
  | data.hotGoods[].originalPrice | number | 商品原价 |
  | data.hotGoods[].stock | number | 商品库存 |
  | data.hotGoods[].tags | array | 商品标签 |

#### 4.3.2 获取分类商品
- **URL**: `/api/farm-goods/category`
- **方法**: `GET`
- **请求参数**:
  | 参数名 | 类型 | 必填 | 描述 |
  |--------|------|------|------|
  | categoryId | string | 是 | 分类ID |
  | page | number | 否 | 页码，默认1 |
  | pageSize | number | 否 | 每页数量，默认10 |

- **响应参数**:
  | 参数名 | 类型 | 描述 |
  |--------|------|------|
  | code | number | 状态码，0表示成功 |
  | message | string | 响应消息 |
  | data | object | 返回数据 |
  | data.goodsList | array | 商品列表 |
  | data.goodsList[].id | number | 商品ID |
  | data.goodsList[].name | string | 商品名称 |
  | data.goodsList[].image | string | 商品图片URL |
  | data.goodsList[].price | number | 商品价格 |
  | data.goodsList[].tags | array | 商品标签 |
  | data.total | number | 总商品数 |
  | data.page | number | 当前页码 |
  | data.pageSize | number | 每页数量 |

#### 4.3.3 搜索商品
- **URL**: `/api/farm-goods/search`
- **方法**: `GET`
- **请求参数**:
  | 参数名 | 类型 | 必填 | 描述 |
  |--------|------|------|------|
  | keyword | string | 是 | 搜索关键词 |
  | page | number | 否 | 页码，默认1 |
  | pageSize | number | 否 | 每页数量，默认10 |

- **响应参数**:
  | 参数名 | 类型 | 描述 |
  |--------|------|------|
  | code | number | 状态码，0表示成功 |
  | message | string | 响应消息 |
  | data | object | 返回数据 |
  | data.goodsList | array | 商品列表 |
  | data.goodsList[].id | number | 商品ID |
  | data.goodsList[].name | string | 商品名称 |
  | data.goodsList[].image | string | 商品图片URL |
  | data.goodsList[].price | number | 商品价格 |
  | data.goodsList[].tags | array | 商品标签 |
  | data.total | number | 总商品数 |
  | data.page | number | 当前页码 |
  | data.pageSize | number | 每页数量 |

### 4.4 商品相关接口

#### 4.4.1 获取商品详情
- **URL**: `/api/goods/detail`
- **方法**: `GET`
- **请求参数**:
  | 参数名 | 类型 | 必填 | 描述 |
  |--------|------|------|------|
  | goodsId | number | 是 | 商品ID |

- **响应参数**:
  | 参数名 | 类型 | 描述 |
  |--------|------|------|
  | code | number | 状态码，0表示成功 |
  | message | string | 响应消息 |
  | data | object | 返回数据 |
  | data.id | number | 商品ID |
  | data.name | string | 商品名称 |
  | data.price | number | 商品价格 |
  | data.image | string | 商品图片URL |
  | data.detailImage | string | 商品详情图片URL |
  | data.description | string | 商品描述 |
  | data.weight | string | 商品重量 |
  | data.storage | string | 商品储存方式 |
  | data.stock | number | 商品库存 |
  | data.tags | array | 商品标签 |

### 4.5 购物车相关接口

#### 4.5.1 获取购物车列表
- **URL**: `/api/cart/list`
- **方法**: `GET`
- **请求头**:
  | 参数名 | 类型 | 必填 | 描述 |
  |--------|------|------|------|
  | Authorization | string | 是 | Bearer token |

- **响应参数**:
  | 参数名 | 类型 | 描述 |
  |--------|------|------|
  | code | number | 状态码，0表示成功 |
  | message | string | 响应消息 |
  | data | object | 返回数据 |
  | data.cartList | array | 购物车商品列表 |
  | data.cartList[].id | number | 购物车项ID |
  | data.cartList[].goodsId | number | 商品ID |
  | data.cartList[].name | string | 商品名称 |
  | data.cartList[].image | string | 商品图片URL |
  | data.cartList[].tag | string | 商品标签 |
  | data.cartList[].price | number | 商品价格 |
  | data.cartList[].count | number | 商品数量 |
  | data.cartList[].checked | boolean | 是否选中 |

#### 4.5.2 添加商品到购物车
- **URL**: `/api/cart/add`
- **方法**: `POST`
- **请求头**:
  | 参数名 | 类型 | 必填 | 描述 |
  |--------|------|------|------|
  | Authorization | string | 是 | Bearer token |

- **请求参数**:
  | 参数名 | 类型 | 必填 | 描述 |
  |--------|------|------|------|
  | goodsId | number | 是 | 商品ID |
  | count | number | 是 | 商品数量 |

- **响应参数**:
  | 参数名 | 类型 | 描述 |
  |--------|------|------|
  | code | number | 状态码，0表示成功 |
  | message | string | 响应消息 |

#### 4.5.3 更新购物车商品数量
- **URL**: `/api/cart/update`
- **方法**: `PUT`
- **请求头**:
  | 参数名 | 类型 | 必填 | 描述 |
  |--------|------|------|------|
  | Authorization | string | 是 | Bearer token |

- **请求参数**:
  | 参数名 | 类型 | 必填 | 描述 |
  |--------|------|------|------|
  | cartId | number | 是 | 购物车项ID |
  | count | number | 是 | 商品数量 |

- **响应参数**:
  | 参数名 | 类型 | 描述 |
  |--------|------|------|
  | code | number | 状态码，0表示成功 |
  | message | string | 响应消息 |

#### 4.5.4 删除购物车商品
- **URL**: `/api/cart/delete`
- **方法**: `DELETE`
- **请求头**:
  | 参数名 | 类型 | 必填 | 描述 |
  |--------|------|------|------|
  | Authorization | string | 是 | Bearer token |

- **请求参数**:
  | 参数名 | 类型 | 必填 | 描述 |
  |--------|------|------|------|
  | cartId | number | 是 | 购物车项ID |

- **响应参数**:
  | 参数名 | 类型 | 描述 |
  |--------|------|------|
  | code | number | 状态码，0表示成功 |
  | message | string | 响应消息 |

#### 4.5.5 清空购物车
- **URL**: `/api/cart/clear`
- **方法**: `DELETE`
- **请求头**:
  | 参数名 | 类型 | 必填 | 描述 |
  |--------|------|------|------|
  | Authorization | string | 是 | Bearer token |

- **响应参数**:
  | 参数名 | 类型 | 描述 |
  |--------|------|------|
  | code | number | 状态码，0表示成功 |
  | message | string | 响应消息 |

### 4.6 订单相关接口

#### 4.6.1 创建订单
- **URL**: `/api/order/create`
- **方法**: `POST`
- **请求头**:
  | 参数名 | 类型 | 必填 | 描述 |
  |--------|------|------|------|
  | Authorization | string | 是 | Bearer token |

- **请求参数**:
  | 参数名 | 类型 | 必填 | 描述 |
  |--------|------|------|------|
  | addressId | number | 是 | 收货地址ID |
  | cartIds | array | 是 | 购物车项ID列表 |
  | totalPrice | number | 是 | 总价格 |
  | remark | string | 否 | 订单备注 |

- **响应参数**:
  | 参数名 | 类型 | 描述 |
  |--------|------|------|
  | code | number | 状态码，0表示成功 |
  | message | string | 响应消息 |
  | data | object | 返回数据 |
  | data.orderId | number | 订单ID |

#### 4.6.2 获取订单列表
- **URL**: `/api/order/list`
- **方法**: `GET`
- **请求头**:
  | 参数名 | 类型 | 必填 | 描述 |
  |--------|------|------|------|
  | Authorization | string | 是 | Bearer token |

- **请求参数**:
  | 参数名 | 类型 | 必填 | 描述 |
  |--------|------|------|------|
  | status | string | 否 | 订单状态（all, pending, paid, shipped, completed, cancelled） |
  | page | number | 否 | 页码，默认1 |
  | pageSize | number | 否 | 每页数量，默认10 |

- **响应参数**:
  | 参数名 | 类型 | 描述 |
  |--------|------|------|
  | code | number | 状态码，0表示成功 |
  | message | string | 响应消息 |
  | data | object | 返回数据 |
  | data.orderList | array | 订单列表 |
  | data.orderList[].id | number | 订单ID |
  | data.orderList[].orderNo | string | 订单编号 |
  | data.orderList[].totalPrice | number | 订单总价格 |
  | data.orderList[].status | string | 订单状态 |
  | data.orderList[].createTime | string | 创建时间 |
  | data.total | number | 总订单数 |
  | data.page | number | 当前页码 |
  | data.pageSize | number | 每页数量 |

#### 4.6.3 获取订单详情
- **URL**: `/api/order/detail`
- **方法**: `GET`
- **请求头**:
  | 参数名 | 类型 | 必填 | 描述 |
  |--------|------|------|------|
  | Authorization | string | 是 | Bearer token |

- **请求参数**:
  | 参数名 | 类型 | 必填 | 描述 |
  |--------|------|------|------|
  | orderId | number | 是 | 订单ID |

- **响应参数**:
  | 参数名 | 类型 | 描述 |
  |--------|------|------|
  | code | number | 状态码，0表示成功 |
  | message | string | 响应消息 |
  | data | object | 返回数据 |
  | data.id | number | 订单ID |
  | data.orderNo | string | 订单编号 |
  | data.totalPrice | number | 订单总价格 |
  | data.status | string | 订单状态 |
  | data.createTime | string | 创建时间 |
  | data.address | object | 收货地址 |
  | data.address.name | string | 收货人姓名 |
  | data.address.phone | string | 收货人电话 |
  | data.address.address | string | 收货地址 |
  | data.goodsList | array | 订单商品列表 |
  | data.goodsList[].id | number | 商品ID |
  | data.goodsList[].name | string | 商品名称 |
  | data.goodsList[].image | string | 商品图片URL |
  | data.goodsList[].price | number | 商品价格 |
  | data.goodsList[].count | number | 商品数量 |

#### 4.6.4 取消订单
- **URL**: `/api/order/cancel`
- **方法**: `PUT`
- **请求头**:
  | 参数名 | 类型 | 必填 | 描述 |
  |--------|------|------|------|
  | Authorization | string | 是 | Bearer token |

- **请求参数**:
  | 参数名 | 类型 | 必填 | 描述 |
  |--------|------|------|------|
  | orderId | number | 是 | 订单ID |

- **响应参数**:
  | 参数名 | 类型 | 描述 |
  |--------|------|------|
  | code | number | 状态码，0表示成功 |
  | message | string | 响应消息 |

### 4.7 个人中心相关接口

#### 4.7.1 获取个人信息
- **URL**: `/api/user/profile`
- **方法**: `GET`
- **请求头**:
  | 参数名 | 类型 | 必填 | 描述 |
  |--------|------|------|------|
  | Authorization | string | 是 | Bearer token |

- **响应参数**:
  | 参数名 | 类型 | 描述 |
  |--------|------|------|
  | code | number | 状态码，0表示成功 |
  | message | string | 响应消息 |
  | data | object | 返回数据 |
  | data.id | number | 用户ID |
  | data.nickname | string | 用户名 |
  | data.avatar | string | 头像URL |
  | data.phone | string | 手机号 |
  | data.email | string | 邮箱 |

#### 4.7.2 更新个人信息
- **URL**: `/api/user/profile`
- **方法**: `PUT`
- **请求头**:
  | 参数名 | 类型 | 必填 | 描述 |
  |--------|------|------|------|
  | Authorization | string | 是 | Bearer token |

- **请求参数**:
  | 参数名 | 类型 | 必填 | 描述 |
  |--------|------|------|------|
  | nickname | string | 否 | 用户名 |
  | avatar | string | 否 | 头像URL |
  | email | string | 否 | 邮箱 |

- **响应参数**:
  | 参数名 | 类型 | 描述 |
  |--------|------|------|
  | code | number | 状态码，0表示成功 |
  | message | string | 响应消息 |

#### 4.7.3 获取收货地址列表
- **URL**: `/api/user/address`
- **方法**: `GET`
- **请求头**:
  | 参数名 | 类型 | 必填 | 描述 |
  |--------|------|------|------|
  | Authorization | string | 是 | Bearer token |

- **响应参数**:
  | 参数名 | 类型 | 描述 |
  |--------|------|------|
  | code | number | 状态码，0表示成功 |
  | message | string | 响应消息 |
  | data | array | 收货地址列表 |
  | data[].id | number | 地址ID |
  | data[].name | string | 收货人姓名 |
  | data[].phone | string | 收货人电话 |
  | data[].province | string | 省份 |
  | data[].city | string | 城市 |
  | data[].district | string | 区县 |
  | data[].address | string | 详细地址 |
  | data[].isDefault | boolean | 是否默认地址 |

#### 4.7.4 添加收货地址
- **URL**: `/api/user/address`
- **方法**: `POST`
- **请求头**:
  | 参数名 | 类型 | 必填 | 描述 |
  |--------|------|------|------|
  | Authorization | string | 是 | Bearer token |

- **请求参数**:
  | 参数名 | 类型 | 必填 | 描述 |
  |--------|------|------|------|
  | name | string | 是 | 收货人姓名 |
  | phone | string | 是 | 收货人电话 |
  | province | string | 是 | 省份 |
  | city | string | 是 | 城市 |
  | district | string | 是 | 区县 |
  | address | string | 是 | 详细地址 |
  | isDefault | boolean | 否 | 是否默认地址 |

- **响应参数**:
  | 参数名 | 类型 | 描述 |
  |--------|------|------|
  | code | number | 状态码，0表示成功 |
  | message | string | 响应消息 |

#### 4.7.5 更新收货地址
- **URL**: `/api/user/address`
- **方法**: `PUT`
- **请求头**:
  | 参数名 | 类型 | 必填 | 描述 |
  |--------|------|------|------|
  | Authorization | string | 是 | Bearer token |

- **请求参数**:
  | 参数名 | 类型 | 必填 | 描述 |
  |--------|------|------|------|
  | id | number | 是 | 地址ID |
  | name | string | 否 | 收货人姓名 |
  | phone | string | 否 | 收货人电话 |
  | province | string | 否 | 省份 |
  | city | string | 否 | 城市 |
  | district | string | 否 | 区县 |
  | address | string | 否 | 详细地址 |
  | isDefault | boolean | 否 | 是否默认地址 |

- **响应参数**:
  | 参数名 | 类型 | 描述 |
  |--------|------|------|
  | code | number | 状态码，0表示成功 |
  | message | string | 响应消息 |

#### 4.7.6 删除收货地址
- **URL**: `/api/user/address`
- **方法**: `DELETE`
- **请求头**:
  | 参数名 | 类型 | 必填 | 描述 |
  |--------|------|------|------|
  | Authorization | string | 是 | Bearer token |

- **请求参数**:
  | 参数名 | 类型 | 必填 | 描述 |
  |--------|------|------|------|
  | id | number | 是 | 地址ID |

- **响应参数**:
  | 参数名 | 类型 | 描述 |
  |--------|------|------|
  | code | number | 状态码，0表示成功 |
  | message | string | 响应消息 |

## 5. 错误处理

### 5.1 错误码定义
| 错误码 | 描述 |
|-------|------|
| 0 | 成功 |
| 400 | 请求参数错误 |
| 401 | 未授权，需要登录 |
| 403 | 禁止访问 |
| 404 | 资源不存在 |
| 500 | 服务器内部错误 |
| 1000 | 登录失败 |
| 1001 | 验证码错误 |
| 1002 | 商品库存不足 |
| 1003 | 购物车商品不存在 |
| 1004 | 订单不存在 |

### 5.2 错误响应格式
```json
{
  "code": 401,
  "message": "未授权，请登录",
  "data": null
}
```

## 6. 安全规范

1. **API访问控制**：所有需要用户身份的API必须验证token
2. **数据加密**：敏感数据（如手机号、密码）传输时应进行加密
3. **输入验证**：对所有用户输入进行严格验证，防止SQL注入、XSS等攻击
4. **请求频率限制**：对API请求进行频率限制，防止恶意请求
5. **日志记录**：记录关键操作日志，便于问题排查和安全审计

## 7. 接口测试

### 7.1 测试环境
- 测试环境URL：`https://test-api.farm-goods.com`
- 生产环境URL：`https://api.farm-goods.com`

### 7.2 测试工具
推荐使用Postman、Swagger等工具进行API测试

### 7.3 测试用例
每个API接口应编写对应的测试用例，包括正常场景和异常场景

## 8. 文档维护

### 8.1 文档更新
- 当API接口发生变化时，应及时更新文档
- 文档更新后应通知相关开发人员

### 8.2 版本管理
- 文档应包含版本信息
- 重要的API变更应在文档中记录

## 9. 附录

### 9.1 数据结构定义

#### 商品数据结构
```json
{
  "id": 1,
  "name": "甜腻玉米500g",
  "image": "https://example.com/images/corn.jpg",
  "price": 8.9,
  "originalPrice": 9.9,
  "stock": 464646,
  "tags": ["软糯香甜", "颗粒饱满"],
  "description": "甜玉米是一种营养丰富的蔬菜，含有丰富的维生素、矿物质和膳食纤维。",
  "weight": "500g",
  "storage": "冷藏"
}
```

#### 用户数据结构
```json
{
  "id": 1,
  "nickname": "用户123",
  "avatar": "https://example.com/avatars/user123.jpg",
  "phone": "13800138000",
  "email": "user123@example.com"
}
```

#### 订单数据结构
```json
{
  "id": 1,
  "orderNo": "202603110001",
  "totalPrice": 69.99,
  "status": "pending",
  "createTime": "2026-03-11 10:00:00",
  "address": {
    "name": "张三",
    "phone": "13800138000",
    "address": "北京市朝阳区某某街道123号"
  },
  "goodsList": [
    {
      "id": 1,
      "name": "甜腻玉米500g",
      "image": "https://example.com/images/corn.jpg",
      "price": 69.99,
      "count": 1
    }
  ]
}
```

### 9.2 参考资料
- RESTful API设计规范
- 微信小程序开发文档
- 农产品电商平台API设计最佳实践
