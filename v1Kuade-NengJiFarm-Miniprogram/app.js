App({
  onLaunch: function () {
  },
  
  onShow: function () {
  },
  
  onHide: function () {
  },
  
  onUnload: function () {
    // 清理所有订单计时器
    try {
      const { orderTimer } = require('./utils/order-timer')
      if (orderTimer && orderTimer.clearAllTimers) {
        orderTimer.clearAllTimers()
      }
    } catch (e) {
    }
  },
  
  globalData: {
    userInfo: null,
    baseUrl: 'https://api.nengjifarm.com'
  }
  
})

