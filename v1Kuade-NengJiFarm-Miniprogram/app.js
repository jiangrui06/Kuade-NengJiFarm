App({
  onLaunch: function () {
  },

  onShow: function () {
    // 监听隐私授权需求（微信小程序隐私保护指引）
    if (wx.onNeedPrivacyAuthorization) {
      wx.onNeedPrivacyAuthorization((resolve) => {
        wx.showModal({
          title: '隐私授权提示',
          content: '需要使用您的头像信息，请同意隐私授权',
          confirmText: '同意',
          cancelText: '拒绝',
          success: (res) => {
            resolve({ buttonId: res.confirm ? 'agree-btn' : 'disagree-btn' });
          }
        });
      });
    }
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

