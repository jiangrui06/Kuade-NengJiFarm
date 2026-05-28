// API 封装
const BASE_URL = 'http://192.168.101.50';
// 需要登录才能访问的接口路径前缀（这些接口无 token 时自动跳登录）
const AUTH_REQUIRED_PREFIXES = [
  '/api/user',
  '/api/orders',
  '/api/cart',
  '/api/OrderDetails',
  '/api/order/create',
  '/api/commodity-order',
  '/api/pay',
  '/api/acres',
  '/api/address',
  '/api/logistics',
  '/api/staff',
  '/api/activity',  // 活动报名需要登录
  '/api/points'     // 积分相关需要登录
];

/**
 * 检查是否需要 token 的接口
 */
function requiresAuth(url) {
  const lowerUrl = url.toLowerCase();
  return AUTH_REQUIRED_PREFIXES.some(prefix => lowerUrl.startsWith(prefix));
}

/**
 * 基础请求函数
 * @param {Object} options - 请求选项
 * @param {string} options.url - 请求地址
 * @param {string} options.method - 请求方法
 * @param {Object} options.data - 请求数据
 * @param {Object} options.header - 请求头
 * @param {boolean} options.showLoading - 是否显示加载提示
 * @param {string} options.loadingText - 加载提示文字
 * @returns {Promise} - 请求结果
 */
function request({ url, method = 'GET', data = {}, header = {}, showLoading = true, loadingText = '加载中...' }) {
  return new Promise((resolve, reject) => {
    // 获取 token
    const token = wx.getStorageSync('token');

    // 需要登录的接口：无 token 直接拒绝，跳转登录页
    if (!token && requiresAuth(url)) {
      wx.showToast({ title: '请先登录', icon: 'none' });
      setTimeout(() => {
        wx.reLaunch({ url: '/pages/login/login' });
      }, 500);
      reject({ code: 401, message: '未登录' });
      return;
    }

    // 显示加载提示
    if (showLoading) {
      wx.showLoading({ title: loadingText, mask: true });
    }

    const defaultHeader = {
      'Content-Type': 'application/json'
    };
    
    // 添加 token 到请求头
    if (token) {
      defaultHeader.Authorization = 'Bearer ' + token;
    }

    // 合并请求头
    const finalHeader = { ...defaultHeader, ...header };

    // 处理请求地址
    let requestUrl;
    if (/^https?:\/\//i.test(url)) {
      requestUrl = url;
    } else {
      // 确保基础 URL 后面有斜杠
      const baseUrl = BASE_URL.endsWith('/') ? BASE_URL : BASE_URL + '/';
      // 确保路径不以斜杠开头
      const path = url.startsWith('/') ? url.substring(1) : url;
      requestUrl = baseUrl + path;
    }

    // 标记是否已隐藏 loading（防止重复调用）
    let loadingHidden = false;
    const hideLoadingSafe = () => {
      if (showLoading && !loadingHidden) {
        wx.hideLoading({
          fail: () => {} // 忽略未配对警告
        });
        loadingHidden = true;
      }
    };
    // 发起请求
    wx.request({
      url: requestUrl,
      method,
      data,
      header: finalHeader,
      timeout: 30000,  // 增加超时时间到30秒
      success(res) {
        // 隐藏加载提示
        hideLoadingSafe();

        // 检查 HTTP 状态码
        if (res.statusCode !== 200) {
          const msg = `请求失败 (${res.statusCode})`;
          reject({ code: res.statusCode, message: msg });
          return;
        }

        // 处理响应
        if (res.data && (res.data.code === 200 || res.data.code === 0)) {
          resolve(res.data.data);
        } else {
          const msg = res.data && res.data.message ? res.data.message : '请求出错';
          reject(res.data);
        }
      },
      fail(err) {
        // 隐藏加载提示
        hideLoadingSafe();

        // 处理网络错误
        reject(err);
      }
    });
  });
}

/**
 * GET 请求
 * @param {string} url - 请求地址
 * @param {Object} data - 请求数据
 * @param {Object} options - 其他选项
 * @returns {Promise} - 请求结果
 */
function get(url, data = {}, options = {}) {
  return request({ ...options, url, method: 'GET', data });
}

/**
 * POST 请求
 * @param {string} url - 请求地址
 * @param {Object} data - 请求数据
 * @param {Object} options - 其他选项
 * @returns {Promise} - 请求结果
 */
function post(url, data = {}, options = {}) {
  return request({ ...options, url, method: 'POST', data });
}

/**
 * PUT 请求
 * @param {string} url - 请求地址
 * @param {Object} data - 请求数据
 * @param {Object} options - 其他选项
 * @returns {Promise} - 请求结果
 */
function put(url, data = {}, options = {}) {
  return request({ ...options, url, method: 'PUT', data });
}

/**
 * DELETE 请求
 * @param {string} url - 请求地址
 * @param {Object} data - 请求数据
 * @param {Object} options - 其他选项
 * @returns {Promise} - 请求结果
 */
function del(url, data = {}, options = {}) {
  return request({ ...options, url, method: 'DELETE', data });
}

/**
 * 上传文件
 * @param {string} url - 上传地址
 * @param {string} filePath - 文件路径
 * @param {string} name - 文件字段名
 * @param {Object} formData - 其他表单数据
 * @param {Object} options - 其他选项
 * @returns {Promise} - 上传结果
 */
function upload(url, filePath, name, formData = {}, options = {}) {
  return new Promise((resolve, reject) => {
    // 显示加载提示
    if (options.showLoading !== false) {
      wx.showLoading({ title: options.loadingText || '上传中...', mask: true });
    }

    // 获取 token
    const token = wx.getStorageSync('token');
    const header = {
      ...options.header
    };
    
    // 添加 token 到请求头
    if (token) {
      header.Authorization = 'Bearer ' + token;
    }

    // 处理请求地址
    let requestUrl;
    if (/^https?:\/\//i.test(url)) {
      requestUrl = url;
    } else {
      // 确保基础 URL 后面有斜杠
      const baseUrl = BASE_URL.endsWith('/') ? BASE_URL : BASE_URL + '/';
      // 确保路径不以斜杠开头
      const path = url.startsWith('/') ? url.substring(1) : url;
      requestUrl = baseUrl + path;
    }

    // 发起上传请求
    wx.uploadFile({
      url: requestUrl,
      filePath,
      name,
      formData,
      header,
      success(res) {
        // 隐藏加载提示
        if (options.showLoading !== false) {
          wx.hideLoading();
        }

        // 处理响应
        try {
          const data = JSON.parse(res.data);
          if (data.code === 200 || data.code === 0) {
            resolve(data.data);
          } else {
            const msg = data.message || '上传失败';
            wx.showToast({ title: msg, icon: 'none' });
            reject(data);
          }
        } catch (e) {
          wx.showToast({ title: '上传失败', icon: 'none' });
          reject({ code: -1, message: '解析响应失败' });
        }
      },
      fail(err) {
        // 隐藏加载提示
        if (options.showLoading !== false) {
          wx.hideLoading();
        }

        // 处理上传错误
        wx.showToast({ title: '上传失败', icon: 'none' });
        reject(err);
      }
    });
  });
}

// API 接口封装
const api = {
  // 首页相关
  home: {
    // 获取首页数据
    getData: (params = {}) => get('/api/home', params),
    // 首页搜索（支持商品+活动）
    search: (keyword, params = {}) => get('/api/home/search', { keyword, ...params })
  },

  // 农场介绍
  farm: {
    // 获取农场介绍
    getIntro: () => get('/api/farm/intro')
  },

  // 文件相关
  file: {
    // 获取图片列表
    getImages: () => get('/api/file/images'),
    // 获取图片
    getImage: (name) => get(`/api/file/image/${name}`),
    // 获取视频列表
    getVideos: () => get('/api/file/videos'),
    // 获取视频
    getVideo: (name) => get(`/api/file/video/${name}`),
    // 上传文件
    upload: (filePath, formData = {}, options = {}) => upload('/api/file/upload', filePath, 'file', formData, options),
    // 上传头像
    uploadAvatar: (filePath) => upload('/api/file/upload/avatar', filePath, 'file')
  },
  
  // 活动相关
  activity: {
    // 获取活动列表
    getList: (params = {}) => get('/api/activity/list', params),
    // 获取活动详情
    getDetail: (id) => get('/api/activity/detail', { id }),
    register: (id, data = {}) => post(`/api/activity/${id}/register`, data)
  },
  
  // 商品相关
  goods: {
    // 获取商品列表
    getList: (params = {}) => get('/api/goods', params),
    // 获取分类列表
    getCategories: (params = {}) => get('/api/goods/categories', params),
    // 获取商品详情
    getDetail: (id) => get(`/api/goods/${id}`),
    // 搜索商品
    search: (keyword, params = {}) => get('/api/goods/search', { keyword, ...params })
  },
  
  // 农场优选相关
  farmGoods: {
    // 获取农场优选商品列表
    getList: (params = {}) => get('/api/farm-goods', params),
    // 获取农场优选分类列表
    getCategories: (params = {}) => get('/api/farm-goods/categories', params)
  },
  
  // 认购一亩田相关
  acre: {
    // 获取认购列表
    getList: () => get('/api/acres'),
    // 获取认购详情
    getDetail: (id) => get(`/api/acres/${id}`),
    adopt: (id, data = {}) => post(`/api/acres/${id}/adopt`, data)
  },
  
  // 订单相关 - 统一订单管理API
  order: {
    // ========== 统一订单管理 API ==========
    // 获取订单列表 (支持 type: all/goods/food/activity, status: all/pending/paid/shipping/completed/cancelled)
    getList: (params = {}, options = {}) => get('/api/orders', params, options),

    // 订单搜索 (支持关键词搜索、状态过滤、类型过滤)
    searchOrders: (params = {}, options = {}) => get('/api/orders/search', params, options),

    // 获取订单数量统计
    getCounts: (params = {}) => get('/api/orders/counts', params),

    // 获取订单详情 (支持订单号或数字ID)
    getDetail: (id) => get(`/api/orders/${id}`),

    // 更新订单状态
    // 发货时 extra 可传 { trackingNumber, trackingTypeId, trackingTypeName, deliveryId }
    updateStatus: (id, status, extra = {}) => {
      const payload = typeof extra === 'string'
        ? { status, reason: extra }
        : { status, ...extra };
      return put(`/api/orders/${id}/status`, payload);
    },

    // 取消订单
    cancel: (id, reason) => put(`/api/orders/${id}/status`, { status: 'cancelled', reason }),

    // 删除订单
    delete: (id) => del(`/api/orders/${id}`),

    // 模拟支付
    pay: (id, data) => post(`/api/orders/${id}/mock-pay`, data),

    // 获取活动核销码
    getQrcode: (id) => get(`/api/orders/${id}/qrcode`),

    // ========== 订单详情聚合 API ==========
    // 获取聚合订单列表
    getAggregatedList: (params = {}) => get('/api/OrderDetails', params),

    // 获取聚合订单详情
    getAggregatedDetail: (id) => get(`/api/OrderDetails/${id}`),

    // 创建订单（支持 sourceType 区分订单类型）
    create: (data) => post('/api/OrderDetails/create', data),

    // ========== 商品订单 API ==========
    // 获取点餐菜单数据（公开接口）
    getMenu: () => get('/api/order'),

    // 获取商品订单列表
    getCommodityList: (params = {}) => get('/api/order/list', params),

    // 创建订单（自动识别商品/点餐类型）
    createOrder: (data) => post('/api/order/create', data),

    // 获取订单详情
    getOrderDetail: (id) => get(`/api/order/detail`, { id }),

    // 取消订单
    cancelOrder: (id) => post(`/api/order/cancel`, { id }),

    // 支付订单
    payOrder: (id, data) => post(`/api/order/${id}/pay`, data),

    // 确认收货
    confirmReceipt: (id) => post(`/api/order/${id}/confirm`, data),

    // 创建商品订单（独立接口）
    createCommodityV2: (data) => post('/api/commodity-order/create', data),
  },

// 桌台相关
  table: {
    // 获取桌台列表
    getList: () => get('/api/order/tables'),
    // 获取桌台详情（扫码校验用，停用桌台返回 404）
    getDetail: (tableNo) => get('/api/table/detail/' + tableNo)
  },

  // 退款相关（对应新版 RESTful 接口 /api/orders/{id}/refund）
  refund: {
    // 申请退款 POST /api/orders/{id}/refund
    apply: (orderId, data) => post(`/api/orders/${orderId}/refund`, data),
    // 取消退款申请 PUT /api/orders/{id}/refund/cancel
    cancel: (orderId) => put(`/api/orders/${orderId}/refund/cancel`),
    // 用户退款记录列表 GET /api/orders/refunds?page=1&pageSize=10&status=
    getList: (params = {}) => get('/api/orders/refunds', params),
    // 退款详情 GET /api/orders/{id}/refund
    getDetail: (orderId) => get(`/api/orders/${orderId}/refund`),
    // 管理员退款列表 GET /api/refund/admin/list（保留，后台接口不变）
    getAdminList: (params = {}) => get('/api/refund/admin/list', params),
    // 管理员处理退款 POST /api/refund/admin/process（保留，后台接口不变）
    process: (data) => post('/api/refund/admin/process', data),
  },

  // 支付相关 - 新版 API
  pay: {
    // 获取可用支付方式
    getMethods: () => get('/api/pay/methods'),
    // 发起微信支付 (JSAPI) — 请求体: { orderId, id, orderNo, type, description }
    createJsapi: (data) => post('/api/pay/jsapi', data),
    // 查询支付状态
    getStatus: (params = {}) => get('/api/pay/status', params),
    // 获取支付信息
    getInfo: (params = {}) => get('/api/pay/info', params)
  },
  
  // 购物车相关
  cart: {
    // 获取购物车列表
    getList: () => get('/api/cart'),
    // 添加到购物车
    add: (data) => post('/api/cart/add', data),
    // 同步购物车（批量）
    sync: (data) => post('/api/cart/sync', data),
    // 更新购物车项数量
    update: (id, quantity) => put(`/api/cart/items/${id}`, { count: quantity }),
    // 从购物车删除单项
    remove: (id) => del(`/api/cart/items/${id}`),
    // 清空购物车
    clear: () => del('/api/cart')
  },
  
  // 个人中心/用户相关
  user: {
    // 获取用户资料
    getProfile: () => get('/api/user/profile'),
    // 更新用户资料
    updateProfile: (data) => put('/api/user/profile', data),
    // 用户信息预览（无需登录）
    getPreview: () => get('/api/user/profile-preview'),
    // 获取收货地址列表
    getAddresses: (options = {}) => get('/api/user/address', {}, options),
    // 添加地址
    addAddress: (data) => post('/api/user/address', data),
    // 更新地址
    updateAddress: (data) => put('/api/user/address', data),
    // 删除地址
    deleteAddress: (data) => { return request({ url: '/api/user/address', method: 'DELETE', data }); },
    // 获取优惠券
    getCoupons: () => get('/api/user/coupons'),
    // 领取优惠券
    collectCoupon: (id) => post(`/api/user/coupons/${id}`),
    // 获取余额流水
    getBalanceHistory: () => get('/api/user/balance/history')
  },
  
  // 登录授权相关
  auth: {
    // 微信登录
    wxLogin: (code, userInfo = {}) => post('/api/Auth/wxlogin', { code, ...userInfo }),
    // 手机号登录
    phoneLogin: (data) => post('/api/Auth/wx-phone-login', data)
  },
  
  // 积分相关
  points: {
    // 获取积分总览 GET /api/points/summary
    summary: (options = {}) => get('/api/points/summary', {}, options),
    // 获取积分商品列表 GET /api/points/goods
    goods: (params = {}) => get('/api/points/goods', params),
    // 获取积分商品详情 GET /api/points/goods/{id}
    goodsDetail: (id) => get(`/api/points/goods/${id}`),
    // 积分兑换商品 POST /api/points/exchange
    exchange: (data) => post('/api/points/exchange', data),
    // 兑换详情（含核销码和二维码）GET /api/points/exchange-detail/{orderNo}
    exchangeDetail: (orderNo) => get(`/api/points/exchange-detail/${orderNo}`),
    // 取消兑换 POST /api/points/exchange-cancel
    cancelExchange: (orderNo) => post('/api/points/exchange-cancel', { orderNo }),
    // 积分流水 GET /api/points/records
    records: (params = {}) => get('/api/points/records', params),
    // 兑换记录 GET /api/points/exchange-records
    exchangeRecords: (params = {}) => get('/api/points/exchange-records', params),
    // 手动积分入账 POST /api/points/earn
    earn: (data) => post('/api/points/earn', data),
    // 积分规则 GET /api/points/rule
    rule: (options = {}) => get('/api/points/rule', {}, options)
  },

  // 物流相关（使用微信物流插件，仅保留基础接口）
  logistics: {
    // 获取物流详情（物流公司、运单号）
    getDetail: (orderId) => get(`/api/logistics/${orderId}`),

    // 获取物流轨迹（完整轨迹时间线）
    getTrace: (orderId) => get(`/api/logistics/${orderId}/trace`),

    // 获取微信物流查询Token
    getWaybillToken: (data) => post('/api/logistics/waybill-token', data)
  },
  
  // 员工端相关
  staff: {
    // 今日核销统计
    getTodayStats: () => get('/api/staff/today-stats'),
    // 核销凭证（旧接口兼容）
    verifyOrder: (code) => post('/api/staff/verify', { code }),
    // 凭证列表（待核销/已核销）
    getVouchers: (params = {}) => get('/api/staff/vouchers', params),
    // 核销历史记录（旧接口兼容）
    getHistory: (params = {}) => get('/api/staff/verify-history', params),
    
    // ========== 新核销系统 API（根据 staff-verify-api.md）==========
    // 验证员工身份
    verifyPermission: () => get('/api/staff-verify/permission'),
    // 查询券信息（不修改状态）
    getVoucherInfo: (code) => post('/api/staff-verify/voucher-info', { code }),
    // 核销券类
    verifyVoucher: (code) => post('/api/staff-verify/voucher', { code }),
    // 获取核销历史
    getVerifyHistory: (params = {}) => get('/api/staff-verify/history', params)
  }
};

module.exports = {
  request,
  get,
  post,
  put,
  del,
  upload,
  api,
  ...api
};

