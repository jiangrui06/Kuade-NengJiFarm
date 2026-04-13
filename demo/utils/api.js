// API 封装
const BASE_URL = 'http://192.168.203.56';

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
    // 显示加载提示
    if (showLoading) {
      wx.showLoading({ title: loadingText, mask: true });
    }

    // 获取 token
    const token = wx.getStorageSync('token');
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
    const requestUrl = /^https?:\/\//i.test(url) ? url : BASE_URL + url;

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
    getImage: (name) => get(`/api/file/image/${name}`)
  },
  
  // 活动相关
  activity: {
    // 获取活动列表
    getList: () => get('/api/activity/list'),
    // 获取活动详情
    getDetail: (id) => get('/api/activity/detail', { id })
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
    getDetail: (id) => get(`/api/acres/${id}`)
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
  }
};

module.exports = {
  request,
  get,
  post,
  put,
  del,
  api
};