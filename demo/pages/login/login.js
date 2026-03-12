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
    const token = wx.getStorageSync('token');
    if (token) {
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
    wx.showLoading({ title: '登录中...', mask: true });
    const api = require('../../utils/api');
    api.request({ url: '/api/auth/login', method: 'POST', data: {
      deviceId: 'wx-' + Date.now(),
      platform: 'wx小程序',
      version: '1.0.0'
    }})
    .then(res => {
      // res should contain token and userInfo
      if (res.token) {
        wx.setStorageSync('token', res.token);
        wx.setStorageSync('hasLogin', true);
      }
      wx.showToast({ title: '登录成功', icon: 'success' });
      setTimeout(() => {
        wx.switchTab({ url: '/pages/index/index' });
      }, 1200);
    })
    .finally(() => {
      wx.hideLoading();
      this.setData({ isLogging: false });
    });
  },

  // 微信登录
  wechatLogin: function() {
    if (this.data.isLogging) return;
    this.setData({ isLogging: true });

    const api = require('../../utils/api');

    wx.showLoading({ title: '微信登录中...', mask: true });

    wx.login({
      success: (res) => {
        if (!res.code) {
          wx.showToast({ title: '微信登录失败，请重试', icon: 'none' });
          return;
        }

        api.request({
          url: '/auth/wechat',
          method: 'POST',
          data: {
            code: res.code
          }
        })
        .then(data => {
          if (data && data.token) {
            wx.setStorageSync('token', data.token);
            wx.setStorageSync('hasLogin', true);
          }
          wx.showToast({ title: '登录成功', icon: 'success' });
          setTimeout(() => {
            wx.switchTab({ url: '/pages/index/index' });
          }, 1200);
        })
        .finally(() => {
          wx.hideLoading();
          this.setData({ isLogging: false });
        });
      },
      fail: () => {
        wx.hideLoading();
        this.setData({ isLogging: false });
        wx.showToast({ title: '微信登录失败', icon: 'none' });
      }
    });
  },

  // 手机号登录
  getPhoneNumber(e) {
    if (e.detail.errMsg !== 'getPhoneNumber:ok') {
      wx.showToast({ title: '您拒绝了授权', icon: 'none' });
      return;
    }

    const code = e.detail.code;
    wx.request({
      url: 'http://192.168.1.12:5141/api/GetPhone',
      method: 'POST',
      data: { code: code },
      success: (res) => {
        if (res.data.success) {
          wx.showToast({ title: '获取成功', icon: 'success' });
          console.log('手机号：', res.data.purePhoneNumber);
        } else {
          wx.showToast({ title: res.data.msg, icon: 'none' });
        }
      },
      fail: () => {
        wx.showToast({ title: '网络错误', icon: 'none' });
      }
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