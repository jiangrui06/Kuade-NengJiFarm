const apiModule = require('./api');
const api = apiModule.api;

const ORDER_TIMEOUT_MINUTES = 30;
const ORDER_TIMEOUT_MS = ORDER_TIMEOUT_MINUTES * 60 * 1000;

// 注意：修改超时时间后，需要清除本地存储的已取消订单记录
// 否则之前已标记为取消的订单不会重新触发超时
// 清除方法：wx.removeStorageSync('order_cancelled_timers')


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
      // 接口更新完成后再通知页面刷新，避免竞争
      this.handleTimeout(orderId, onTimeout);
      return 0;
    }
    
    this.timers[orderId] = setTimeout(() => {
      // 接口更新完成后再通知页面刷新，避免竞争
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
    
    // iOS 兼容性处理：将 "yyyy-MM-dd HH:mm:ss" 转换为 "yyyy/MM/dd HH:mm:ss"
    let dateStr = createTime;
    if (typeof createTime === 'string') {
      dateStr = createTime.replace(/-/g, '/');
    }
    
    const date = new Date(dateStr);
    if (!isNaN(date.getTime())) {
      return date.getTime();
    }
    
    // 兜底：尝试用正则解析
    if (typeof createTime === 'string') {
      const parts = createTime.match(/(\d{4})-(\d{2})-(\d{2})\s+(\d{2}):(\d{2}):(\d{2})/);
      if (parts) {
        return new Date(parts[1], parts[2] - 1, parts[3], parts[4], parts[5], parts[6]).getTime();
      }
    }
    
    return Date.now();
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
        // 通知后端恢复库存
        this.restoreStock(orderId);
        // 恢复本地库存扣减记录
        try {
          const stockDeduction = require('./stock-deduction');
          stockDeduction.remove(orderId);
        } catch (e) {}
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
          // 404 也尝试恢复库存，防止之前未恢复
          try {
            const stockDeduction = require('./stock-deduction');
            stockDeduction.remove(orderId);
          } catch (e) {}
        } else {
          console.error(`订单 ${orderId} 取消失败:`, err);
        }
        // 即使失败也通知页面刷新，让页面展示最新状态
        if (onTimeout) {
          onTimeout(orderId);
        }
      });
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

  saveCancelledTime(orderId, timestamp) {
    const cancelledTimers = wx.getStorageSync('order_cancelled_timers') || {};
    cancelledTimers[orderId] = timestamp;
    wx.setStorageSync('order_cancelled_timers', cancelledTimers);
  }

  getCancelledTime(orderId) {
    const cancelledTimers = wx.getStorageSync('order_cancelled_timers') || {};
    return cancelledTimers[orderId] || null;
  }

  restoreStock(orderId) {
    try {
      const map = wx.getStorageSync('stock_deduction_map') || {};
      const entry = map[orderId];
      if (!entry || !entry.goodsList || !Array.isArray(entry.goodsList)) return;
      const updates = {};
      entry.goodsList.forEach(item => {
        const goodsId = item.id || item.Id || item.goodsId;
        const qty = item.quantity || item.Quantity || 0;
        if (goodsId && qty > 0) {
          updates[goodsId] = qty; // 正数 = 加回库存
        }
      });
      if (Object.keys(updates).length > 0) {
        api.syncStock.updateQuantity(updates);
      }
    } catch (e) {
      console.error('通知后端恢复库存失败:', e);
    }
  }
}

module.exports = {
  orderTimer: new OrderTimer()
};

