// API 封装
const BASE_URL = 'http://192.168.203.56';

// 需要登录才能访问的接口路径前缀（这些接口无 token 时自动跳登录）
const AUTH_REQUIRED_PREFIXES = [
  '/api/user', 
  '/api/orders', 
  '/api/cart', 
  '/api/OrderDetails', 
  '/api/pay', 
  '/api/acres', 
  '/api/address', 
  '/api/logistics', 
  '/api/staff',
  '/api/commodity-order',
  '/api/activity-order',
  '/api/dish-order'
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

    // 发起请求
    wx.request({
      url: requestUrl,
      method,
      data,
      header: finalHeader,
      success(res) {
        // 隐藏加载提示
        if (showLoading) {
          wx.hideLoading();
        }

        // 处理响应
        if (res.data && (res.data.code === 200 || res.data.code === 0)) {
          resolve(res.data.data);
        } else {
          const msg = res.data && res.data.message ? res.data.message : '请求出错';
          wx.showToast({ title: msg, icon: 'none' });
          reject(res.data);
        }
      },
      fail(err) {
        // 隐藏加载提示
        if (showLoading) {
          wx.hideLoading();
        }

        // 处理网络错误
        wx.showToast({ title: '网络错误', icon: 'none' });
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
          console.error('解析上传结果失败:', e);
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
        console.error('上传失败:', err);
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
    getData: (params = {}) => get('/api/home', params)
  },
  
  // 文件相关
  file: {
    // 获取图片列表
    getImages: () => get('/api/file/images'),
    // 获取图片
    getImage: (name) => get(`/api/file/image/${name}`),
    // 上传文件
    upload: (filePath, formData = {}, options = {}) => upload('/api/upload', filePath, 'file', formData, options)
  },
  
  // 活动相关
  activity: {
    // 获取活动列表
    getList: () => get('/api/activity/list'),
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
  
  // 订单相关 - 新版订单聚合API
  order: {
    // 获取订单列表 (支持 type: all/goods/food/activity, status: all/pending/paid/shipping/completed/cancelled)
    getList: (params = {}) => get('/api/orders', params),
    // 获取订单详情 (支持订单号或数字ID)
    getDetail: (id) => get(`/api/orders/${id}`),
    // 更新订单状态
    updateStatus: (id, status, reason) => put(`/api/orders/${id}/status`, { status, reason }),
    // 取消订单
    cancel: (id, reason) => put(`/api/orders/${id}/status`, { status: 'cancelled', reason }),
    // 删除订单
    delete: (id) => del(`/api/orders/${id}`),
    // 获取订单数量统计
    getCounts: (params = {}) => get('/api/orders/counts', params),
    // 获取活动/采摘核销码
    getQrcode: (id) => get(`/api/orders/${id}/qrcode`),
    
    // 创建订单 - 兼容旧接口
    create: (data) => post('/api/OrderDetails/create', data),
    // 创建商品订单 - 兼容旧接口
    createCommodity: (data) => post('/api/OrderDetails/create', { ...data, sourceType: 'goods', sourceName: '商品购买' }),
    // 创建活动订单 - 兼容旧接口
    createActivity: (data) => post('/api/OrderDetails/create', { ...data, sourceType: 'activity', sourceName: '活动报名' }),
    // 创建点餐订单 - 兼容旧接口
    createDish: (data) => post('/api/OrderDetails/create', { ...data, sourceType: 'food', sourceName: '点餐' }),
    
    // 旧接口兼容方法 - 通过聚合接口实现
    getCommodityList: (params = {}) => get('/api/orders', { ...params, type: 'goods' }),
    getActivityList: (params = {}) => get('/api/orders', { ...params, type: 'activity' }),
    getDishList: (params = {}) => get('/api/orders', { ...params, type: 'food' }),
    // 模拟支付 - 兼容旧接口
    pay: (id, data) => post(`/api/orders/${id}/mock-pay`, data)
  },
  
  // 支付相关 - 新版 API
  pay: {
    // 获取可用支付方式
    getMethods: () => get('/api/pay/methods'),
    // 发起微信支付 (JSAPI)
    createJsapi: (orderId) => post('/api/pay/jsapi', { orderId }),
    // 查询支付状态
    getStatus: (orderId) => get('/api/pay/status', { orderId })
  },
  
  // 购物车相关
  cart: {
    // 获取购物车列表
    getList: () => get('/api/cart'),
    // 添加到购物车
    add: (data) => post('/api/cart', data),
    // 更新购物车数量
    update: (id, quantity) => put(`/api/cart/${id}`, { quantity }),
    // 从购物车删除
    remove: (id) => del(`/api/cart/${id}`),
    // 清空购物车
    clear: () => del('/api/cart/clear')
  },
  
  // 个人中心/用户相关
  user: {
    // 获取用户信息
    getProfile: () => get('/api/user/profile'),
    // 更新用户信息
    updateProfile: (data) => put('/api/user/profile', data),
    // 获取收货地址
    getAddresses: () => get('/api/address'),
    // 获取默认地址
    getDefaultAddress: () => get('/api/address/default'),
    // 添加地址
    addAddress: (data) => post('/api/address', data),
    // 更新地址
    updateAddress: (id, data) => put(`/api/address/${id}`, data),
    // 删除地址
    deleteAddress: (id) => del(`/api/address/${id}`),
    // 设为默认地址
    setDefaultAddress: (id) => put(`/api/address/${id}/default`),
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
  
  // 物流相关
  logistics: {
    // 获取物流信息
    getTrack: (orderId) => get(`/api/logistics/track/${orderId}`)
  },
  
  // 员工端相关
  staff: {
    // 员工登录
    login: (data) => post('/api/staff/login', data),
    // 核销订单
    verifyOrder: (code) => post('/api/staff/verify', { code }),
    // 获取待核销列表
    getPendingList: () => get('/api/staff/pending'),
    // 获取已核销记录
    getHistory: () => get('/api/staff/history')
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

