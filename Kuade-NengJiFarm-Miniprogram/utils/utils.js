// 工具函数封装

/**
 * 本地存储操作
 */
const storage = {
  /**
   * 设置本地存储
   * @param {string} key - 存储键名
   * @param {any} value - 存储值
   */
  set(key, value) {
    wx.setStorageSync(key, value);
  },

  /**
   * 获取本地存储
   * @param {string} key - 存储键名
   * @param {any} defaultValue - 默认值
   * @returns {any} - 存储值
   */
  get(key, defaultValue = null) {
    const value = wx.getStorageSync(key);
    return value !== undefined ? value : defaultValue;
  },

  /**
   * 删除本地存储
   * @param {string} key - 存储键名
   */
  remove(key) {
    wx.removeStorageSync(key);
  },

  /**
   * 清空本地存储
   */
  clear() {
    wx.clearStorageSync();
  }
};

/**
 * 时间处理工具
 */
const time = {
  /**
   * 格式化时间
   * @param {number|string|Date} date - 时间
   * @param {string} format - 格式化模板
   * @returns {string} - 格式化后的时间
   */
  format(date, format = 'YYYY-MM-DD HH:mm:ss') {
    const d = new Date(date);
    const year = d.getFullYear();
    const month = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    const hours = String(d.getHours()).padStart(2, '0');
    const minutes = String(d.getMinutes()).padStart(2, '0');
    const seconds = String(d.getSeconds()).padStart(2, '0');

    return format
      .replace('YYYY', year)
      .replace('MM', month)
      .replace('DD', day)
      .replace('HH', hours)
      .replace('mm', minutes)
      .replace('ss', seconds);
  },

  /**
   * 获取相对时间
   * @param {number|string|Date} date - 时间
   * @returns {string} - 相对时间
   */
  getRelativeTime(date) {
    const now = new Date();
    const d = new Date(date);
    const diff = now - d;
    const minutes = Math.floor(diff / 60000);
    const hours = Math.floor(diff / 3600000);
    const days = Math.floor(diff / 86400000);

    if (minutes < 1) return '刚刚';
    if (minutes < 60) return `${minutes}分钟前`;
    if (hours < 24) return `${hours}小时前`;
    if (days < 7) return `${days}天前`;
    return this.format(date, 'YYYY-MM-DD');
  }
};

/**
 * 验证工具
 */
const validate = {
  /**
   * 验证手机号
   * @param {string} phone - 手机号
   * @returns {boolean} - 是否有效
   */
  phone(phone) {
    return /^1[3-9]\d{9}$/.test(phone);
  },

  /**
   * 验证邮箱
   * @param {string} email - 邮箱
   * @returns {boolean} - 是否有效
   */
  email(email) {
    return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email);
  },

  /**
   * 验证身份证号
   * @param {string} idCard - 身份证号
   * @returns {boolean} - 是否有效
   */
  idCard(idCard) {
    return /^[1-9]\d{5}(18|19|20)\d{2}(0[1-9]|1[0-2])(0[1-9]|[12]\d|3[01])\d{3}[\dXx]$/.test(idCard);
  },

  /**
   * 验证密码强度
   * @param {string} password - 密码
   * @returns {number} - 强度等级（1-4）
   */
  passwordStrength(password) {
    let strength = 0;
    if (password.length >= 8) strength++;
    if (/[A-Z]/.test(password)) strength++;
    if (/[a-z]/.test(password)) strength++;
    if (/[0-9]/.test(password)) strength++;
    if (/[^A-Za-z0-9]/.test(password)) strength++;
    return Math.min(strength, 4);
  }
};

/**
 * 数字处理工具
 */
const number = {
  /**
   * 格式化价格
   * @param {number} price - 价格
   * @param {number} decimals - 小数位数
   * @returns {string} - 格式化后的价格
   */
  formatPrice(price, decimals = 2) {
    return parseFloat(price).toFixed(decimals);
  },

  /**
   * 格式化数字
   * @param {number} num - 数字
   * @param {number} decimals - 小数位数
   * @returns {string} - 格式化后的数字
   */
  formatNumber(num, decimals = 0) {
    return parseFloat(num).toFixed(decimals);
  },

  /**
   * 格式化大数字
   * @param {number} num - 数字
   * @returns {string} - 格式化后的数字
   */
  formatLargeNumber(num) {
    if (num >= 100000000) {
      return (num / 100000000).toFixed(1) + '亿';
    } else if (num >= 10000) {
      return (num / 10000).toFixed(1) + '万';
    }
    return num.toString();
  }
};

/**
 * 字符串处理工具
 */
const string = {
  /**
   * 截断字符串
   * @param {string} str - 字符串
   * @param {number} length - 长度
   * @param {string} suffix - 后缀
   * @returns {string} - 截断后的字符串
   */
  truncate(str, length, suffix = '...') {
    if (str.length <= length) return str;
    return str.substring(0, length) + suffix;
  },

  /**
   * 去除首尾空格
   * @param {string} str - 字符串
   * @returns {string} - 处理后的字符串
   */
  trim(str) {
    return str.trim();
  },

  /**
   * 转义HTML
   * @param {string} str - 字符串
   * @returns {string} - 转义后的字符串
   */
  escapeHtml(str) {
    return str
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#039;');
  }
};

/**
 * 数组处理工具
 */
const array = {
  /**
   * 数组去重
   * @param {Array} arr - 数组
   * @returns {Array} - 去重后的数组
   */
  unique(arr) {
    return [...new Set(arr)];
  },

  /**
   * 数组排序
   * @param {Array} arr - 数组
   * @param {string} key - 排序键
   * @param {string} order - 排序顺序（asc/desc）
   * @returns {Array} - 排序后的数组
   */
  sort(arr, key, order = 'asc') {
    return arr.sort((a, b) => {
      const valA = a[key];
      const valB = b[key];
      if (order === 'asc') {
        return valA > valB ? 1 : -1;
      } else {
        return valA < valB ? 1 : -1;
      }
    });
  },

  /**
   * 数组分组
   * @param {Array} arr - 数组
   * @param {string|Function} key - 分组键或分组函数
   * @returns {Object} - 分组后的对象
   */
  groupBy(arr, key) {
    return arr.reduce((groups, item) => {
      const group = typeof key === 'function' ? key(item) : item[key];
      groups[group] = groups[group] || [];
      groups[group].push(item);
      return groups;
    }, {});
  }
};

/**
 * 导航工具
 */
const navigation = {
  /**
   * 跳转到页面
   * @param {string} url - 页面路径
   * @param {Object} params - 参数
   */
  navigateTo(url, params = {}) {
    const queryString = Object.keys(params)
      .map(key => `${key}=${encodeURIComponent(params[key])}`)
      .join('&');
    const fullUrl = queryString ? `${url}?${queryString}` : url;
    wx.navigateTo({ url: fullUrl });
  },

  /**
   * 跳转到tab页面
   * @param {string} url - 页面路径
   */
  switchTab(url) {
    wx.switchTab({ url });
  },

  /**
   * 重定向到页面
   * @param {string} url - 页面路径
   * @param {Object} params - 参数
   */
  redirectTo(url, params = {}) {
    const queryString = Object.keys(params)
      .map(key => `${key}=${encodeURIComponent(params[key])}`)
      .join('&');
    const fullUrl = queryString ? `${url}?${queryString}` : url;
    wx.redirectTo({ url: fullUrl });
  },

  /**
   * 返回上一页
   * @param {number} delta - 返回页数
   */
  navigateBack(delta = 1) {
    wx.navigateBack({ delta });
  }
};

/**
 * 提示工具
 */
const toast = {
  /**
   * 显示成功提示
   * @param {string} title - 提示文字
   * @param {number} duration - 持续时间
   */
  success(title, duration = 1500) {
    wx.showToast({ title, icon: 'success', duration });
  },

  /**
   * 显示错误提示
   * @param {string} title - 提示文字
   * @param {number} duration - 持续时间
   */
  error(title, duration = 1500) {
    wx.showToast({ title, icon: 'none', duration });
  },

  /**
   * 显示加载提示
   * @param {string} title - 提示文字
   */
  loading(title = '加载中...') {
    wx.showLoading({ title, mask: true });
  },

  /**
   * 隐藏加载提示
   */
  hideLoading() {
    wx.hideLoading();
  }
};

module.exports = {
  storage,
  time,
  validate,
  number,
  string,
  array,
  navigation,
  toast
};