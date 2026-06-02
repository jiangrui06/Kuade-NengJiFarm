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
   * @param {string} format - 格式化模式
   * @returns {string} - 格式化后的时间
   */
  format(date, format = 'YYYY-MM-DD HH:mm:ss') {
    const d = date instanceof Date ? date : new Date(String(date).replace(/-/g, '/'));
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
    const d = date instanceof Date ? date : new Date(String(date).replace(/-/g, '/'));
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
   * @param {string} key - 分组键
   * @returns {Object} - 分组后的对象
   */
  groupBy(arr, key) {
    return arr.reduce((result, item) => {
      (result[item[key]] = result[item[key]] || []).push(item);
      return result;
    }, {});
  }
};

/**
 * 资源处理工具
 */
const media = {
  /**
   * 处理图片/视频路径，确保使用正确的基础 URL 和 API 映射
   * @param {string} url - 原始 URL 或路径
   * @returns {string} - 处理后的完整 URL
   */
  processUrl(url) {
    if (!url) return '';
    const baseUrl = getApp().globalData?.baseUrl || 'https://api.nengjifarm.com';
    let normalized = String(url).replace(/[`\s]/g, '');

    // 0. 兜底处理旧格式完整 URL（如 https://api.nengjifarm.com/Farm_14.jpg）
    //    转换为 https://api.nengjifarm.com/api/file/image/images/farm/Farm_14.jpg
    if (normalized.startsWith(baseUrl + '/') && !normalized.startsWith(baseUrl + '/api/') && !normalized.startsWith(baseUrl + '/images/')) {
      const rawPath = normalized.substring(baseUrl.length); // /Farm_14.jpg
      const fileName = rawPath.split('/').filter(Boolean).pop() || '';
      return `${baseUrl}/api/file/image/images/farm/${fileName}`;
    }

    // 1. 如果已经是完整的 API 地址且包含 baseUrl，直接返回
    if (normalized.startsWith(baseUrl + '/api/file/')) {
      return normalized;
    }

    // 2. 如果是相对路径的 API 地址，补全 baseUrl
    if (normalized.startsWith('/api/file/')) {
      return baseUrl + normalized;
    }

    // 3. 提取文件名或相对路径
    // 优先处理上传路径，保留子目录结构（如 avatar/xxx.jpg）
    let mediaPath = '';
    if (normalized.includes('/api/file/uploads/')) {
      return baseUrl + normalized.substring(normalized.indexOf('/api/file/uploads/'));
    }
    
    if (normalized.includes('/uploads/')) {
      mediaPath = normalized.split('/uploads/').pop() || '';
      return `${baseUrl}/api/file/uploads/${mediaPath}`;
    }

    // 处理常见的旧路径格式
    let fileName = '';
    if (normalized.includes('/images/farm/')) {
      fileName = normalized.split('/images/farm/').pop() || '';
    } else if (normalized.includes('/farm/')) {
      fileName = normalized.split('/farm/').pop() || '';
    } else {
      fileName = normalized.split('/').filter(Boolean).pop() || '';
    }
    
    if (!fileName) return '';

    // 4. 去除可能存在的 URL 参数或锚点
    fileName = fileName.split(/[?#]/)[0];

    // 5. 根据后缀判断类型并映射到正确的 API 接口
    const lowerName = fileName.toLowerCase();
    const isVideo = ['.mp4', '.mov', '.avi', '.mkv', '.wmv'].some(ext => lowerName.endsWith(ext));
    
    return isVideo
      ? `${baseUrl}/api/file/video/${fileName}`
      : `${baseUrl}/api/file/image/${fileName}`;
  }
};

module.exports = {
  storage,
  time,
  validate,
  number,
  string,
  array,
  media
};

