App({
  onLaunch: function () {
    console.log('小程序启动')
  },
  
  onShow: function () {
    console.log('小程序显示')
  },
  
  onHide: function () {
    console.log('小程序隐藏')
  },
  
  onUnload: function () {
    console.log('小程序卸载')
    // 清理所有订单计时器
    try {
      const { orderTimer } = require('./utils/order-timer')
      if (orderTimer && orderTimer.clearAllTimers) {
        orderTimer.clearAllTimers()
      }
    } catch (e) {
      console.error('清理订单计时器失败:', e)
    }
  },
  
  globalData: {
    userInfo: null
  }
})