Page({
  data: {
    isLogging: false,
    bgImage: ''
  },

  onLoad() {
    this.checkLoginStatus();
    this.getBackgroundImage();
  },
  
  // 获取背景图片
  getBackgroundImage: function() {
    // 使用API返回的图片URL，拼接基础URL
    const BASE_URL = 'http://192.168.203.56';
    const imagePath = '/api/file/image/farm_0000000000012.jpg';
    const bgImageUrl = BASE_URL + imagePath;
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
    this.wechatLogin();
  },

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

          wx.setStorageSync('token', loginData.token || '');
          wx.setStorageSync('hasLogin', true);
          wx.setStorageSync('user_id', loginData.user_id || '');
          wx.setStorageSync('user_guid', loginData.user_guid || '');
          wx.setStorageSync('openid', loginData.openid || '');
          wx.setStorageSync('register_time', loginData.register_time || '');

          wx.showToast({
            title: '登录成功',
            icon: 'success'
          });

          setTimeout(() => {
            wx.switchTab({
              url: '/pages/index/index'
            });
          }, 1000);
        })
        .catch(err => {
          console.error('微信登录失败：', err);
          wx.showToast({
            title: err.Message || err.message || '登录失败',
            icon: 'none'
          });
        })
        .finally(() => {
          wx.hideLoading();
          this.setData({ isLogging: false });
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