# Demo 接口映射与调用逻辑（能记农场小程序）

此文档根据项目中 `demo` 小程序前端代码（静态模拟数据）整理，目的是为 Web API 提供一套可用于本地联调的接口与数据结构。

## 一、总体关系

- 小程序页面（`demo/pages/*`）向后端请求 JSON 数据来渲染页面。当前 demo 版本大多使用本地模拟数据（`setTimeout`）。我们在 Web API 中提供对应的接口，返回相同的数据结构以供小程序真实请求调试。

主要页面与建议后端接口：

- `index`（首页） -> `GET /api/demo/home`
- `farm-goods`（商品列表） -> `GET /api/demo/goods?category={id}` 或 `GET /api/demo/goods?pageIndex=&pageSize=`
- `goods-detail` -> `GET /api/demo/goods/{id}`
- `order`（点餐/下单） -> `GET /api/demo/order`、`POST /api/demo/orders`
- `cart`（购物车） -> `GET /api/demo/cart`、`POST /api/demo/cart/items`
- `acre`（认养地块列表） -> `GET /api/demo/acres`
- `acre-detail` -> `GET /api/demo/acres/{id}`
- `activity` -> `GET /api/demo/activities`
- `profile` -> `GET /api/demo/profile`

## 二、统一响应格式（推荐）

所有接口返回格式保持一致，便于小程序统一处理：

{
  code: 0,        // 0 表示成功，非 0 表示错误
  message: "ok",
  data: { ... }   // 业务数据
}

## 三、主要数据结构（示例）

1. 首页数据 `/api/demo/home` 返回示例：
   - `swiperList`: [{ id:int, image:string }]
   - `functionButtons`: [{ id:int, name:string, color:string, path:string }]
   - `farmGoods`: [{ id:int, name:string, image:string, price:decimal, originalPrice:decimal, tags:string[], stock:int }]
   - `hotDishes`: [{ id:int, name:string, image:string, price:decimal, tags:string[] }]

2. 商品列表 `/api/demo/goods`（按分类返回）示例：
   - 返回一个对象，包含多个分类数组或分页结果
   - 单个商品对象字段同上（`id, name, image, price, stock, tags`）

3. 商品详情 `/api/demo/goods/{id}` 示例：
   - `id, name, price, image, detailImage, description, weight, storage`

4. 购物车 `/api/demo/cart` 示例：
   - `cartList`: [{ id:int, name:string, image:string, tag:string, price:decimal, count:int, checked:bool }]
   - `totalPrice`: string 或 number

5. 订单 `/api/demo/orders` 示例：
   - 创建订单请求体：{ items: [{ goodsId, quantity, price }], addressId, remark }
   - 返回：{ orderId }

6. 地块 `/api/demo/acres` 与 `/api/demo/acres/{id}` 示例：
   - `id, name, description, price, image`（`acre-detail` 包含更详细描述）

7. 活动 `/api/demo/activities` 示例：
   - 列表：[{ id, title, price, date, image }]

## 四、调用逻辑建议

- 小程序端使用 `wx.request` 指向后端接口（开发时可用本地地址，如 `https://localhost:5001/api/demo/...`）。
- 后端先实现静态返回（mock）接口，确保前端和数据结构稳定后再替换为真实的数据访问层（数据库/服务）。
- 建议后端实现细化控制器：`GoodsController`, `CartController`, `OrderController`, `AcreController`, `ActivityController`, `ProfileController`。当前先提供一个 `Demo` 控制器返回示例数据用于快速联调。

## 五、后续工作清单

1. 在 `WebApplication1` 项目中创建 `DemoApiController`，暴露上面列出的接口并返回示例数据（用于小程序联调）。
2. 小程序将模拟数据替换为真实请求，验证字段匹配与界面表现。 
3. 将示例数据逐步替换为数据库或业务层实现。

---

文档由代码仓库 demo 页面自动归纳生成，若需我继续：
- 我可以把 demo 中每个页面的确切字段逐条映射到接口模型（C# DTO），并生成对应的 Controller 与 DTO 文件。
- 或者先把一个最小可用集（首页、商品列表、商品详情、地块、活动、购物车）实现到 Web API 中用于联调。

请选择下一步（例如："先实现首页和商品相关接口"，或 "生成全部 demo 接口"）。
