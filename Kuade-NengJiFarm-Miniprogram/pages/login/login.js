Page({
  data: {
    isLogging: false,
    bgImage: ''
  },

  onLoad() {
    this.checkLoginStatus();
    this.getBackgroundImage();
  },

  onUnload() {
    // 页面卸载时重置状态，防止登录成功后页面销毁前被重新进入
    this.setData({ isLogging: false });
  },

  // 获取背景图片
  getBackgroundImage: function() {
    const bgImageUrl = 'http://192.168.203.56/api/file/image/farm_0000000000012.jpg';
    this.setData({
      bgImage: bgImageUrl
    });
  },

  checkLoginStatus() {
    const token = wx.getStorageSync('token');
    if (token) {
      wx.switchTab({
        url: '/pages/index/index'
      });
    }
  },

  oneClickLogin() {
    if (this.data.isLogging) return;
    this.wechatLogin();
  },

  // 阻止登录按钮点击
  preventLoginClick() {
    // 什么都不做，只是阻止点击事件冒泡
  },

  // 微信一键登录
  wechatLogin() {
    if (this.data.isLogging) return;

    const api = require('../../utils/api');

    this.setData({ isLogging: true });

    wx.showLoading({
      title: '微信登录中...',
      mask: true
    });

    wx.login({
      success: (loginRes) => {
        if (!loginRes.code) {
          wx.hideLoading();
          this.setData({ isLogging: false });
          wx.showToast({
            title: '获取code失败',
            icon: 'none'
          });
          return;
        }

        api.request({
          url: '/api/Auth/wxlogin',
          method: 'POST',
          data: {
            code: loginRes.code
          }
        })
        .then(loginData => {
          console.log('登录成功：', loginData);
          this.handleLoginSuccess(loginData);
          // 成功后不重置 isLogging，保持禁用状态直到页面卸载或跳转
        })
        .catch(err => {
          console.error('微信登录失败：', err);
          this.setData({ isLogging: false });
          wx.showToast({
            title: err.Message || err.message || '登录失败',
            icon: 'none'
          });
        })
        .finally(() => {
          wx.hideLoading();
          // 不在此处重置 isLogging，防止登录成功后 1 秒内再次点击
        });
      },
      fail: (err) => {
        console.error('wx.login失败：', err);
        wx.hideLoading();
        this.setData({ isLogging: false });
        wx.showToast({
          title: '微信登录失败',
          icon: 'none'
        });
      }
    });
  },

  // 登录成功后的公共处理
  handleLoginSuccess(loginData) {
    wx.setStorageSync('token', loginData.token || '');
    wx.setStorageSync('hasLogin', true);
    wx.setStorageSync('user_id', loginData.user_id || '');
    wx.setStorageSync('user_guid', loginData.user_guid || '');
    wx.setStorageSync('openid', loginData.openid || '');
    wx.setStorageSync('register_time', loginData.register_time || '');
    if (loginData.phone_number) {
      wx.setStorageSync('phone_number', loginData.phone_number);
    }

    // 延迟跳转，期间保持按钮禁用
    setTimeout(() => {
      wx.switchTab({
        url: '/pages/index/index'
      });
    }, 1000);
  },

  viewAgreement() {
    wx.showModal({
      title: '用户协议',
      content: '欢迎使用能记农场，登录即表示您已阅读并同意用户协议。',
      showCancel: false,
      confirmText: '我知道了'
    });
  },

  viewPrivacy() {
    wx.showModal({
      title: '隐私政策',
      content: '我们重视您的隐私，并会依法保护您的个人信息安全。',
      showCancel: false,
      confirmText: '我知道了'
    });
  }
});
