# 能记农场 (NenJi Farm)

> 智慧农场全栈管理平台，提供微信小程序、管理后台、厨房看板三端服务。
>
> 后端基于 **ASP.NET Core Web API (.NET 8)** + **MySQL 8.0**，前端基于 **微信小程序** + **Vue 3**。

---

## 项目结构

```
NenJi-API/
├── NenJi-API/WebAPI/WebAPI/    ← 本仓库后端 API（.NET 8）
├── ManageAPI/                   ← 管理后台 API（旧版）
├── v1Kuade-NengJiFarm-Miniprogram/  ← 微信小程序前端
├── web_management/              ← 管理后台 Web 前端
├── Web_BackFood/               ← 厨房看板 Web 页面
├── db/                          ← 数据库脚本
└── .claude/                     ← Claude Code 配置
```

---

## 后端 API（.NET 8）

### 技术栈

| 分类 | 技术 |
|------|------|
| 框架 | ASP.NET Core 8 (WebAPI) |
| 语言 | C# 12 |
| 数据库 | MySQL 8.0 |
| ORM | Entity Framework Core 8 + Pomelo MySQL |
| 认证 | JWT Bearer（无状态 Token） |
| 密码 | BCrypt.Net-Next |
| 文档 | Swagger (Swashbuckle) |
| 支付 | 微信支付 |
| 二维码 | QRCoder |

### 核心功能模块

- **商品交易**：商品管理、购物车、订单流转、配送追踪
- **餐饮点单**：菜品管理、餐桌二维码、扫码点单、厨房推送
- **活动预约**：活动创建、分类管理、报名核销、库存管理
- **用户体系**：微信静默登录、后台管理员 JWT 登录、员工角色权限
- **退款售后**：小程序端申请退款、管理后台审核（通过/驳回）、退款记录
- **认购一亩田**：地块管理、认购订单、配送排期
- **积分系统**：积分查询、兑换、记录

### 接口概览（33 个控制器）

| 分组 | 路径 | 说明 |
|------|------|------|
| 商品 | `/api/product`、`/api/goods` | 商品 CRUD、小程序商城 |
| 菜品 | `/api/dish` | 菜品管理 |
| 活动 | `/api/activity`、`/api/activity-manage` | 活动浏览、后台管理 |
| 餐桌 | `/api/table` | 餐桌管理、二维码 |
| 订单 | `/api/product/order`、`/api/dish/order`、`/api/activity-order` | 各类型订单 |
| 退款 | `/api/orders/...` | 申请/审核/驳回 |
| 用户 | `/api/back-user`、`/api/auth` | 管理员管理、微信登录 |
| 支付 | `/api/pay` | 微信支付、回调 |
| 文件 | `/api/common` | 文件上传 |
| 厨房 | `/api/kitchen` | 厨房看板 |
| 购物车 | `/api/cart` | 购物车 |
| 积分 | `/api/points` | 积分 |
| 其他 | `/api/farm`、`/api/staff` 等 | 认购、员工核销 |

> 详细接口文档见 `NenJi-API/WebAPI/WebAPI/完整接口文档.md`

---

### 快速运行

```bash
# 进入后端项目目录
cd NenJi-API/WebAPI/WebAPI

# 修改 appsettings.json 中数据库连接字符串
# 安装 .NET 8 SDK 后执行
dotnet run
```

API 默认地址：`http://localhost:5000`  
Swagger 文档：`http://localhost:5000/swagger`

---

## 参考

- `claude-code-skills` — [Claude Code 技能库](https://github.com/HongMengSeng/claude-code-skills)
