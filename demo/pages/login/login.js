Page({
  data: {
    // 登录状态
    isLogging: false
  },

  onLoad: function() {
    console.log('登录页面加载');
    // 检查是否已登录
    this.checkLoginStatus();
  },

  // 检查登录状态
  checkLoginStatus: function() {
    // 模拟检查登录状态
    const hasLogin = wx.getStorageSync('hasLogin');
    if (hasLogin) {
      // 已登录，跳转到首页
      wx.switchTab({
        url: '/pages/index/index'
      });
    }
  },

  // 一键登录
  oneClickLogin: function() {
    if (this.data.isLogging) return;
    
    this.setData({ isLogging: true });
    
    // 模拟登录过程
    wx.showLoading({
      title: '登录中...',
      mask: true
    });
    
    // 模拟网络请求
    setTimeout(() => {
      // 登录成功
      wx.hideLoading();
      
      // 存储登录状态
      wx.setStorageSync('hasLogin', true);
      
      // 显示登录成功提示
      wx.showToast({
        title: '登录成功',
        icon: 'success',
        duration: 1500
      });
      
      // 跳转到首页
      setTimeout(() => {
        wx.switchTab({
          url: '/pages/index/index'
        });
      }, 1500);
      
      this.setData({ isLogging: false });
    }, 1500);
  },

  // 微信登录
  wechatLogin: function() {
    wx.showToast({
      title: '微信登录功能开发中',
      icon: 'none'
    });
  },

  // 手机号登录
  phoneLogin: function() {
    wx.showToast({
      title: '手机号登录功能开发中',
      icon: 'none'
    });
  },

  // 查看用户协议
  viewAgreement: function() {
    wx.showModal({
      title: '用户协议',
      content: '欢迎使用农场优选！本协议旨在保护您的权益，请您仔细阅读。',
      showCancel: false,
      confirmText: '我知道了'
    });
  },

  // 查看隐私政策
  viewPrivacy: function() {
    wx.showModal({
      title: '隐私政策',
      content: '我们重视您的隐私，将严格保护您的个人信息。',
      showCancel: false,
      confirmText: '我知道了'
    });
  }
});