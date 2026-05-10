# 能记农场 - NengJi Farm API

## 项目简介
能记农场微信小程序后端 API，基于 ASP.NET Core 8.0 + MySQL (nengjidb_v1)。

## 技术栈
- ASP.NET Core 8.0 Web API
- Entity Framework Core (Pomelo MySQL)
- JWT Bearer 认证
- Swagger (Farm Mini Program API)
- 微信支付 JSAPI

## 项目结构
- `WebAPI/Controllers/` — API 控制器
- `WebAPI/Entities/` — 数据库实体模型
- `WebAPI/Services/` — 业务服务层
- `WebAPI/Data/` — DbContext 和数据库配置
- `WebAPI/Migrations/` — EF Core 迁移

## 本地运行
1. 数据库连接: `server=localhost;port=3306;database=nengjidb_v1;user=root;password=Kuade@NengJi9144`
2. 启动: `dotnet run --project WebAPI/WebAPI.csproj`
3. Swagger: `http://localhost:5000/swagger`

## 模块
- 认证/用户: auth, user
- 农场: farm, acres
- 商品/点餐: goods, farm-goods, dish, cart
- 订单: order, orders, OrderDetails, commodity-order
- 支付: pay, refund
- 物流: logistics
- 活动: activity
- 员工: staff, staff-verify
- 文件: file
