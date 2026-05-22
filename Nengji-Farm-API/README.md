# 能记农场 (NenJi Farm) — 后端 API 服务

> 智慧农场管理平台后端服务，支撑微信小程序端、管理后台端、厨房看板端三端业务。
>
> 项目为农场提供完整的商品交易、餐饮点单、活动预约、用户管理、退款售后、配送追踪等核心业务能力。

---

## 目录

- [技术栈](#技术栈)
- [项目架构](#项目架构)
- [核心功能模块](#核心功能模块)
- [接口概览](#接口概览)
- [快速开始](#快速开始)
- [项目结构](#项目结构)

---

## 技术栈

| 分类 | 技术 | 说明 |
|------|------|------|
| **运行时** | .NET 8 (WebAPI) | 跨平台高性能后端框架 |
| **语言** | C# 12 | 现代类型安全语言 |
| **数据库** | MySQL 8.0 | 关系型数据库 |
| **ORM** | Entity Framework Core 8.0 + Pomelo MySQL | 数据访问层 |
| **认证** | JWT Bearer (JSON Web Token) | 无状态身份认证 |
| **密码加密** | BCrypt.Net-Next | 密码哈希存储 |
| **API 文档** | Swagger / Swashbuckle | 自动生成接口文档 |
| **序列化** | System.Text.Json + Newtonsoft.Json | JSON 处理 |
| **二维码** | QRCoder | 餐桌二维码生成 |
| **架构模式** | Controller-Service-Repository | 分层架构 |

### 第三方集成

| 集成 | 用途 |
|------|------|
| 微信支付 | 订单支付 |
| 微信小程序登录 | 用户认证 |
| 微信物流助手 | 配送追踪 |

---

## 项目架构

```
┌─────────────────────────────────────────────────────┐
│                    客户端层                           │
│  ┌──────────────┐  ┌──────────────┐  ┌───────────┐  │
│  │ 微信小程序     │  │ 管理后台 Web  │  │ 厨房看板   │  │
│  └──────┬───────┘  └──────┬───────┘  └─────┬─────┘  │
└─────────┼──────────────────┼────────────────┼────────┘
          │                  │                │
          ▼                  ▼                ▼
┌─────────────────────────────────────────────────────┐
│                   API 网关层                         │
│  ┌──────────────────────────────────────────────┐   │
│  │          JWT 认证 / CORS / 异常中间件          │   │
│  └──────────────────────────────────────────────┘   │
├─────────────────────────────────────────────────────┤
│                   控制器层 (33个)                    │
│  ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐ ┌──────────┐  │
│  │ 商品  │ │ 餐饮  │ │ 活动  │ │ 用户  │ │ 退款/订单 │  │
│  └──┬───┘ └──┬───┘ └──┬───┘ └──┬───┘ └────┬─────┘  │
│     │        │        │        │          │         │
│  ┌──▼────────▼────────▼────────▼──────────▼──────┐  │
│  │              业务服务层 (Services)               │  │
│  └──────────────────────┬─────────────────────────┘  │
│                         │                            │
│  ┌──────────────────────▼─────────────────────────┐  │
│  │          EF Core / MySQL 数据访问层              │  │
│  └─────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────┘
```

---

## 核心功能模块

### 🛒 商品交易
- 商品管理（CRUD、分类、标签、库存、规格图）
- 购物车管理
- 订单流转（待支付 → 待发货 → 待收货 → 已完成）
- 配送追踪（对接微信物流助手）

### 🍽️ 餐饮点单
- 菜品管理（分类、上下架、规格图）
- 餐桌管理（二维码生成、桌号绑定）
- 扫码点单、厨房订单推送
- 退款处理

### 🎯 活动预约
- 活动创建与管理（轮播图、详情图、规格图）
- 活动分类、库存管理
- 用户报名/预约
- 活动核销

### 👥 用户体系
- 微信小程序登录（静默授权）
- 后台管理员登录（JWT）
- 员工账号管理（角色权限）
- 用户信息管理

### 💰 退款售后
- 小程序端申请退款（商品/餐饮/活动）
- 管理后台审核退款（通过/驳回）
- 退款记录查询
- 退款凭证图片上传

### 📦 认购一亩田
- 地块管理
- 认购订单
- 配送排期

---

## 接口概览

项目包含 **33 个控制器**，主要接口分组如下：

| 分组 | 基础路径 | 控制器 | 核心接口 |
|------|---------|--------|---------|
| 商品管理 | `/api/product` | ProductController | 列表/详情/新增/编辑/删除 |
| 商品(小程序) | `/api/goods` | GoodsController | 列表/详情/搜索/分类 |
| 菜品管理 | `/api/dish` | DishController | 列表/详情/新增/编辑/删除 |
| 活动管理 | `/api/activity-manage` | ActivityManageController | 列表/详情/新增/编辑/删除 |
| 活动(小程序) | `/api/activity` | ActivityController | 列表/分类/详情/报名 |
| 餐桌管理 | `/api/table` | TableController | 列表/详情/新增/编辑/删除/二维码 |
| 订单(商品) | `/api/product/order` | ProductOrderController | 列表/详情/状态更新/退款 |
| 订单(餐饮) | `/api/dish/order` | DishOrderController | 列表/详情/退款 |
| 订单(活动) | `/api/activity-order` | ActivityOrderController | 列表/详情/核销/退款 |
| 退款 | `/api/orders/...` | RefundController | 申请/详情/列表/取消 |
| 用户管理 | `/api/back-user` | BackUserController | 列表/新增/编辑/删除/登录 |
| 微信认证 | `/api/auth` | AuthController | 微信登录/静默登录 |
| 文件上传 | `/api/common` | CommonController | 文件上传 |
| 厨房看板 | `/api/kitchen` | KitchenController | 订单列表/出餐/完成 |
| 支付 | `/api/pay` | PayController | 微信支付/回调 |
| 购物车 | `/api/cart` | CartController | 增/删/改/查 |
| 积分 | `/api/points` | PointsController | 积分查询/兑换/记录 |

---

## 快速开始

### 环境要求

- .NET 8 SDK
- MySQL 8.0+
- Visual Studio 2022 / Rider / VS Code

### 1. 克隆项目

```bash
git clone https://github.com/HongMengSeng/NenJi-API.git
cd NenJi-API
```

### 2. 配置数据库

修改 `appsettings.json` 中的连接字符串：

```json
{
  "ConnectionStrings": {
    "ManageConnection": "Server=localhost;Database=nenji_v2;User=root;Password=your_password"
  }
}
```

### 3. 运行数据库迁移

```bash
dotnet ef database update
```

### 4. 启动服务

```bash
dotnet run
```

API 默认运行在 `http://localhost:5000`，Swagger 文档访问 `http://localhost:5000/swagger`。

---

## 项目结构

```
NenJi-API/
├── Controllers/          # API 控制器（33个）
│   ├── Activity*.cs      # 活动相关
│   ├── BackUser.cs       # 后台用户管理
│   ├── Dish*.cs          # 菜品相关
│   ├── GoodsController   # 小程序商品
│   ├── Order*.cs         # 订单相关
│   ├── Product*.cs       # 商品管理
│   ├── RefundController  # 退款
│   └── ...
├── Services/             # 业务逻辑层
├── Dtos/                 # 数据传输对象
├── Entities/             # 数据库实体模型
│   ├── Manage/           # 管理后台实体
│   └── ...
├── Data/                 # DbContext 上下文
│   ├── ManageAppDbContext.cs
│   └── AppDbContext.cs
├── Common/               # 通用工具类
├── Middleware/            # 中间件（异常处理等）
├── Options/              # 配置选项
├── PasswordHash/         # 密码哈希服务
└── wwwroot/              # 静态文件（图片、二维码）
```

---

## 核心设计亮点

### 🔐 双数据库架构
- **ManageAppDbContext**：管理后台业务（商品、订单、活动）
- **AppDbContext**：小程序端业务（用户、购物车、支付）
- 支持 MySQL 主从或独立数据库部署

### 🎨 统一响应格式
所有 API 统一返回 `ApiResult` 格式：
```json
{
  "code": 0,
  "message": "success",
  "data": {}
}
```

### 📱 三端适配
- **微信小程序端**：`/api/goods`、`/api/activity`、`/api/orders/...`
- **管理后台端**：`/api/product`、`/api/dish`、`/api/back-user`
- **厨房看板端**：`/api/kitchen`

### 🛡️ 安全机制
- JWT Bearer Token 身份认证
- BCrypt 密码哈希
- CORS 跨域策略
- 全局异常中间件
- 请求参数校验
