const apiModule = require('./api');
const api = apiModule.api;

const ORDER_TIMEOUT_MINUTES = 0.3;
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
      this.handleTimeout(orderId);
      if (onTimeout) {
        onTimeout(orderId);
      }
      return 0;
    }
    
    this.timers[orderId] = setTimeout(() => {
      this.handleTimeout(orderId);
      if (onTimeout) {
        onTimeout(orderId);
      }
    }, remaining);
    
    return remaining;
  }

  parseCreateTime(createTime) {
    if (!createTime) return Date.now();
    
    let date = new Date(createTime);
    if (!isNaN(date.getTime())) {
      const parts = createTime.match(/(\d{4})-(\d{2})-(\d{2})\s+(\d{2}):(\d{2}):(\d{2})/);
      if (parts) {
        date = new Date(parts[1], parts[2] - 1, parts[3], parts[4], parts[5], parts[6]);
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

  handleTimeout(orderId) {
    console.log(`订单 ${orderId} 超时，自动取消`);
    api.order.updateStatus(orderId, 'cancelled')
      .then(() => {
        console.log(`订单 ${orderId} 自动取消成功`);
        // 尝试删除订单，但即使删除失败也没关系
        return api.order.delete(orderId)
          .then(() => {
            console.log(`订单 ${orderId} 自动删除成功`);
          })
          .catch((deleteErr) => {
            console.warn(`订单 ${orderId} 删除失败，但已成功取消:`, deleteErr);
            // 删除失败不算错误，因为订单已经取消了
          });
      })
      .catch((err) => {
        // 如果订单不存在（404），说明已经被处理了，不算错误
        if (err && err.code === 404) {
          console.log(`订单 ${orderId} 不存在，可能已被删除`);
        } else {
          console.error(`订单 ${orderId} 取消失败:`, err);
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
}

const orderTimer = new OrderTimer();

module.exports = {
  orderTimer,
  ORDER_TIMEOUT_MINUTES,
  ORDER_TIMEOUT_MS
};
