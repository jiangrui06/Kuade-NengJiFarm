const apiModule = require('./api');
const api = apiModule.api;

const ORDER_TIMEOUT_MINUTES = 1;
const ORDER_TIMEOUT_MS = ORDER_TIMEOUT_MINUTES * 60 * 1000;


class OrderTimer {
  constructor() {
    this.timers = {};
    this.callbacks = {};
  }

  startTimer(orderId, createTime, onTimeout) {
    this.clearTimer(orderId);
    
    const now = Date.now();
    const orderCreateTime = this.parseCreateTime(createTime);
    const elapsed = now - orderCreateTime;
    const remaining = Math.max(0, ORDER_TIMEOUT_MS - elapsed);
    
    if (remaining <= 0) {
      // 接口更新完成后再通知页面刷新，避免竞态
      this.handleTimeout(orderId, onTimeout);
      return 0;
    }
    
    this.timers[orderId] = setTimeout(() => {
      // 接口更新完成后再通知页面刷新，避免竞态
      this.handleTimeout(orderId, onTimeout);
    }, remaining);
    
    return remaining;
  }

  parseCreateTime(createTime) {
    if (!createTime) return Date.now();
    
    // 如果是数字类型，直接返回
    if (typeof createTime === 'number') {
      return createTime;
    }
    
    let date = new Date(createTime);
    if (!isNaN(date.getTime())) {
      // 只有当是字符串时才尝试 match
      if (typeof createTime === 'string') {
        const parts = createTime.match(/(\d{4})-(\d{2})-(\d{2})\s+(\d{2}):(\d{2}):(\d{2})/);
        if (parts) {
          date = new Date(parts[1], parts[2] - 1, parts[3], parts[4], parts[5], parts[6]);
        }
      }
    }
    return date.getTime();
  }

  getRemainingTime(createTime) {
    const now = Date.now();
    const orderCreateTime = this.parseCreateTime(createTime);
    const elapsed = now - orderCreateTime;
    return Math.max(0, ORDER_TIMEOUT_MS - elapsed);
  }

  formatTime(ms) {
    if (ms <= 0) {
      return '00:00';
    }
    
    const totalSeconds = Math.floor(ms / 1000);
    const minutes = Math.floor(totalSeconds / 60);
    const seconds = totalSeconds % 60;
    
    return `${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`;
  }

  handleTimeout(orderId, onTimeout) {
    console.log(`订单 ${orderId} 超时，自动取消`);
    
    api.order.updateStatus(orderId, 'cancelled')
      .then(() => {
        console.log(`订单 ${orderId} 自动取消成功`);
        // 记录取消时间到本地 Storage
        this.saveCancelledTime(orderId, Date.now());
        // 状态更新成功后再通知页面刷新，避免页面读到旧状态
        if (onTimeout) {
          onTimeout(orderId);
        }
      })
      .catch((err) => {
        // 如果订单不存在（404），说明已经被处理了，不算错误
        if (err && err.code === 404) {
          console.log(`订单 ${orderId} 不存在，可能已被处理`);
        } else {
          console.error(`订单 ${orderId} 取消失败:`, err);
        }
        // 即使失败也通知页面刷新，让页面展示最新状态
        if (onTimeout) {
          onTimeout(orderId);
        }
      });
  }

  // ========== 本地取消时间存储（Storage）==========

  static get STORAGE_KEY() { return 'order_cancelled_times'; }

  // 获取所有已记录的取消时间 { orderId: timestamp }
  getCancelledTimesFromStorage() {
    try {
      const data = wx.getStorageSync(OrderTimer.STORAGE_KEY);
      return data ? JSON.parse(data) : {};
    } catch (e) {
      console.warn('读取取消时间缓存失败:', e);
      return {};
    }
  }

  // 记录某个订单的取消时间
  saveCancelledTime(orderId, timestamp) {
    try {
      const times = this.getCancelledTimesFromStorage();
      times[String(orderId)] = timestamp;
      wx.setStorageSync(OrderTimer.STORAGE_KEY, JSON.stringify(times));
      console.log(`已记录订单 ${orderId} 的取消时间:`, new Date(timestamp).toLocaleString());
    } catch (e) {
      console.warn('保存取消时间到缓存失败:', e);
    }
  }

  // 获取某个订单的本地记录的取消时间
  getLocalCancelledTime(orderId) {
    const times = this.getCancelledTimesFromStorage();
    return times[String(orderId)] || null;
  }

  // 删除某个订单的取消时间记录（订单被删除后清理）
  removeCancelledTime(orderId) {
    try {
      const times = this.getCancelledTimesFromStorage();
      delete times[String(orderId)];
      wx.setStorageSync(OrderTimer.STORAGE_KEY, JSON.stringify(times));
    } catch (e) {
      console.warn('清除取消时间缓存失败:', e);
    }
  }

  // 清理所有过期的取消时间记录（防止 Storage 无限增长）
  cleanExpiredRecords() {
    try {
      const times = this.getCancelledTimesFromStorage();
      const now = Date.now();
      let changed = false;
      Object.keys(times).forEach(orderId => {
        // 记录超过 48 小时就清理掉（已经是安全余量了）
        if (now - times[orderId] > 48 * 3600 * 1000) {
          delete times[orderId];
          changed = true;
        }
      });
      if (changed) {
        wx.setStorageSync(OrderTimer.STORAGE_KEY, JSON.stringify(times));
      }
    } catch (e) {
      // 忽略
    }
  }

  clearTimer(orderId) {
    if (this.timers[orderId]) {
      clearTimeout(this.timers[orderId]);
      delete this.timers[orderId];
    }
  }

  clearAllTimers() {
    Object.keys(this.timers).forEach(orderId => {
      this.clearTimer(orderId);
    });
  }

}

const orderTimer = new OrderTimer();

module.exports = {
  orderTimer,
  ORDER_TIMEOUT_MINUTES,
  ORDER_TIMEOUT_MS
};
