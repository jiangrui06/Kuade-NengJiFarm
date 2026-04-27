// API 封装
const BASE_URL = 'http://192.168.101.47';

// 需要登录才能访问的接口路径前缀（这些接口无 token 时自动跳登录）
const AUTH_REQUIRED_PREFIXES = ['/api/user', '/api/orders', '/api/cart', '/api/OrderDetails', '/api/pay', '/api/acres', '/api/address', '/api/logistics', '/api/staff'];

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
        if (res.data && res.data.code === 0) {
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
          if (data.code === 0) {
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
    // 获取商品详情
    getDetail: (id) => get(`/api/goods/${id}`),
    // 搜索商品
    search: (keyword, params = {}) => get('/api/goods/search', { keyword, ...params })
  },
  
  // 认购一亩田相关
  acre: {
    // 获取认购列表
    getList: () => get('/api/acres'),
    // 获取认购详情
    getDetail: (id) => get(`/api/acres/${id}`),
    adopt: (id, data = {}) => post(`/api/acres/${id}/adopt`, data)
  },
  
  // 订单相关
  order: {
    // 创建订单
    create: (data) => post('/api/OrderDetails/create', data),
    // 获取订单列表
    getList: (params = {}) => get('/api/orders', params),
    // 获取订单详情
    getDetail: (id) => get(`/api/orders/${id}`),
    // 取消订单
    cancel: (id) => put(`/api/orders/${id}/status`, { status: 'cancelled' }),
    // 删除订单
    delete: (id) => del(`/api/orders/${id}`),
    // 支付订单
    pay: (id, data) => post(`/api/orders/${id}/mock-pay`, data),
    // 更新订单状态
    updateStatus: (id, status) => put(`/api/orders/${id}/status`, { status }),
    // 获取订单统计
    getCounts: () => get('/api/orders/counts'),
    // 获取活动订单核销二维码
    getQrcode: (id) => get(`/api/orders/${id}/qrcode`)
  },
  
  // 购物车相关
  cart: {
    // 获取购物车列表
    getList: () => get('/api/cart'),
    // 添加商品到购物车
    add: (data) => post('/api/cart/add', data),
    // 更新购物车商品数量
    update: (id, data) => put(`/api/cart/${id}`, data),
    // 删除购物车商品
    delete: (id) => del(`/api/cart/${id}`),
    // 清空购物车
    clear: () => del('/api/cart')
  },
  
  // 用户相关
  user: {
    // 登录
    login: (data) => post('/api/user/login', data),
    // 注册
    register: (data) => post('/api/user/register', data),
    // 获取用户信息
    getInfo: () => get('/api/user/profile'),
    // 更新用户信息
    updateInfo: (data) => put('/api/user/profile', data),
    // 获取地址列表
    getAddressList: () => get('/api/user/address'),
    // 添加地址
    addAddress: (data) => post('/api/user/address', data),
    // 更新地址
    updateAddress: (id, data) => put(`/api/user/address/${id}`, data),
    // 删除地址
    deleteAddress: (id) => del(`/api/user/address/${id}`)
  },

  // 支付相关
  pay: {
    // 获取可用支付方式
    getMethods: () => get('/api/pay/methods'),
    // 创建微信 JSAPI 支付
    createJsapi: (orderId, options = {}) => post('/api/pay/jsapi', { orderId, ...options }),
    // 兼容发起支付接口
    initiatePayment: (orderId, options = {}) => post('/api/pay/initiate-payment', { orderId, ...options }),
    // 查询本地订单支付状态
    getStatus: (orderId) => get('/api/pay/status', { orderId }),
    // 微信查单并同步状态
    queryStatus: (orderId) => post('/api/pay/query-payment-status', { orderId }),
    // 获取支付页展示信息
    getInfo: (orderId) => get('/api/pay/info', { orderId })
  },

  // 物流相关
  logistics: {
    // 获取物流详情
    getDetail: (orderId) => get(`/api/logistics/${orderId}`),
    // 获取物流轨迹
    getTrace: (orderId) => get(`/api/logistics/${orderId}/trace`)
  },

  // 员工核销相关
  staff: {
    // 扫码核销券（采摘券/活动券）
    verifyVoucher: (code) => post('/api/staff/verify', { code }),
    // 获取待核销的券列表
    getPendingVouchers: (params = {}) => get('/api/staff/vouchers', params),
    // 获取核销记录
    getVerifyHistory: (params = {}) => get('/api/staff/verify-history', params)
  }
};

module.exports = {
  request,
  get,
  post,
  put,
  del,
  upload,
  api
};
