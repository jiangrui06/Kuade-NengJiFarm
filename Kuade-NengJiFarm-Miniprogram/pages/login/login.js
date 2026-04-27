const api = require('../../utils/api');

Page({
  data: {
    isLogging: false,
    bgImage: '',
    statusBarHeight: 20
  },

  onLoad() {
    // 获取状态栏高度（适配刘海屏）
    const sysInfo = wx.getWindowInfo ? wx.getWindowInfo() : wx.getSystemInfoSync();
    this.setData({ statusBarHeight: sysInfo.statusBarHeight });

    this.checkLoginStatus();
    this.getBackgroundImage();
  },

  onUnload() {
    this.setData({ isLogging: false });
  },

  // 获取背景图片
  getBackgroundImage: function() {
    const bgImageUrl = 'http://192.168.101.47/api/file/image/farm_0000000000012.jpg';
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

  // 阻止登录按钮点击（防重复）
  preventLoginClick() {
    // 空函数，仅阻止事件冒泡
  },

  // ========== 微信手机号一键登录 ==========
  
  // 用户点击手机号授权按钮的回调
  onGetPhoneNumber(e) {
    console.log('获取手机号回调:', e);

    // 用户拒绝授权
    if (!e.detail.code) {
      wx.showToast({ title: '您取消了授权', icon: 'none' });
      return;
    }

    if (this.data.isLogging) return;

    const phoneCode = e.detail.code;
    this.setData({ isLogging: true });

    wx.showLoading({ title: '登录中...', mask: true });

    // 先拿 wx.login 的 code，再和 phoneCode 一起发给后端
    wx.login({
      success: (loginRes) => {
        if (!loginRes.code) {
          wx.hideLoading();
          this.setData({ isLogging: false });
          wx.showToast({ title: '获取登录凭证失败', icon: 'none' });
          return;
        }

        console.log('调用手机号登录接口, code:', loginRes.code);

        // 调用后端微信手机号登录接口（与 profile-edit 一致）
        api.request({
          url: '/api/Auth/wx-phone-login',
          method: 'POST',
          data: {
            code: loginRes.code,
            phoneCode: phoneCode
          },
          showLoading: false
        })
        .then(loginData => {
          console.log('手机号登录成功:', loginData);
          
          // 本地存储手机号验证
          if (loginData.phone_number) {
            wx.setStorageSync('phone_number', loginData.phone_number);
          }

          this.handleLoginSuccess(loginData);
        })
        .catch(err => {
          console.error('手机号登录失败:', err);
          this.setData({ isLogging: false });

          // 根据错误码给友好提示
          if (err && err.code === 409) {
            wx.showToast({ title: '该手机号已绑定其他账号', icon: 'none' });
          } else {
            const errMsg = (err && err.message) || '登录失败，请重试';
            wx.showToast({ title: errMsg, icon: 'none' });
          }
        })
        .finally(() => {
          wx.hideLoading();
        });
      },
      fail: () => {
        wx.hideLoading();
        this.setData({ isLogging: false });
        wx.showToast({ title: '微信登录失败', icon: 'none' });
      }
    });
  },

  // 登录成功后的公共处理
  handleLoginSuccess(loginData) {
    // 存储 token 和用户信息
    wx.setStorageSync('token', loginData.token || '');
    wx.setStorageSync('hasLogin', true);
    wx.setStorageSync('user_id', loginData.user_id || '');
    wx.setStorageSync('user_guid', loginData.user_guid || '');
    wx.setStorageSync('openid', loginData.openid || '');
    wx.setStorageSync('register_time', loginData.register_time || '');
    
    // 手机号存储到本地（用于 profile-edit 页面读取）
    if (loginData.phone_number) {
      wx.setStorageSync('phone_number', loginData.phone_number);
    }

    // 预取用户信息并缓存（让个人中心页面秒显）
    this.preloadUserProfile();

    // 延迟跳转首页
    setTimeout(() => {
      wx.switchTab({
        url: '/pages/index/index'
      });
    }, 800);
  },

  // 登录后预取用户信息写入本地缓存
  preloadUserProfile() {
    api.request({
      url: '/api/user/profile',
      method: 'GET',
      showLoading: false,
      showError: false
    })
    .then(data => {
      const profile = {
        nickname: data.nickname || '',
        avatar: data.avatar ? this.processImageUrl(data.avatar) : '',
        email: data.email || '',
        balance: Number(data.balance || 0),
        reward: Number(data.reward || 0)
      };
      wx.setStorageSync('user_profile_cache', profile);
      console.log('预取用户信息成功:', profile);
    })
    .catch(err => {
      console.warn('预取用户信息失败（不影响登录）:', err);
    });
  },

  processImageUrl(imageUrl) {
    if (!imageUrl) return '';
    imageUrl = String(imageUrl).replace(/[`\s]/g, '');
    if (imageUrl.startsWith('http://') || imageUrl.startsWith('https://')) {
      if (imageUrl.includes('127.0.0.1:5000')) {
        return imageUrl.replace('127.0.0.1:5000', '192.168.203.56');
      }
      return imageUrl;
    }
    const baseUrl = 'http://192.168.101.47';
    if (!imageUrl.startsWith('/')) imageUrl = '/' + imageUrl;
    return baseUrl + imageUrl;
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
