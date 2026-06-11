const api = require('../../utils/api');
const share = require('../../utils/share');

Page({
  data: {
    isLogging: false,
    bgImage: '',
    statusBarHeight: 20,
    bgLoaded: false,
    bgError: false
  },

  onLoad() {
    // 获取状态栏高度（适配刘海屏）
    const sysInfo = wx.getWindowInfo ? wx.getWindowInfo() : wx.getSystemInfoSync();
    this.setData({ statusBarHeight: sysInfo.statusBarHeight });

    // 加载背景图
    this.getBackgroundImage();

    // 已登录用户直接跳转到首页（员工和用户都到首页）
    const token = wx.getStorageSync('token');
    if (token) {
      wx.switchTab({ url: '/pages/index/index' });
    }
  },

  onUnload() {
    this.setData({ isLogging: false });
  },

  // 获取背景图片
  getBackgroundImage: function () {
    // 使用远程背景图
    const bgImageUrl = (getApp().globalData.baseUrl || 'https://api.nengjifarm.com') + '/api/file/image/farm_0000000000012.jpg';
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

  // 返回上一页
  goBack() {
    wx.navigateBack({
      fail: () => {
        wx.switchTab({ url: '/pages/index/index' });
      }
    });
  },

  // 阻止登录按钮点击（防重复）
  preventLoginClick() {
    // 空函数，仅阻止事件冒泡
  },

  // ========== 微信手机号一键登录 ==========
  
  // 用户点击手机号授权按钮的回调
  onGetPhoneNumber(e) {

    // 用户拒绝授权
    if (!e.detail.code) {
      wx.showToast({ title: '您取消了授权', icon: 'none' });
      return;
    }

    if (this.data.isLogging) return;

    const phoneCode = e.detail.code;
    this.setData({ isLogging: true });

    // 使用自定义动画，isLogging 已在上方设置为 true

    // 先拿 wx.login 的 code，再和 phoneCode 一起发给后端
    wx.login({
      success: (loginRes) => {
        if (!loginRes.code) {
          this.setData({ isLogging: false });
          wx.showToast({ title: '获取登录凭证失败', icon: 'none' });
          return;
        }


        // 调用后端微信手机号登录接口
        api.post('/api/Auth/wx-phone-login', {
          code: loginRes.code,
          phoneCode: phoneCode
        }, { showLoading: false })
        .then(loginData => {
          
          // 本地存储手机号验证
          if (loginData.phone_number) {
            wx.setStorageSync('phone_number', loginData.phone_number);
          }

          this.handleLoginSuccess(loginData);
        })
        .catch(err => {
          this.setData({ isLogging: false });

          // 根据错误码给友好提示
          if (err && err.code === 409) {
            wx.showToast({ title: '该手机号已绑定其他账号', icon: 'none' });
          } else {
            const errMsg = (err && err.message) || '登录失败，请重试';
            wx.showToast({ title: errMsg, icon: 'none' });
          }
        });
      },
      fail: () => {
        this.setData({ isLogging: false });
        wx.showToast({ title: '微信登录失败', icon: 'none' });
      }
    });
  },

  // 登录成功后的公共处理
  handleLoginSuccess(loginData) {
    // 存储token和用户信息
    wx.setStorageSync('token', loginData.token || '');
    wx.setStorageSync('hasLogin', true);
    wx.setStorageSync('user_id', loginData.user_id || '');
    wx.setStorageSync('user_guid', loginData.user_guid || '');
    wx.setStorageSync('openid', loginData.openid || '');
    wx.setStorageSync('register_time', loginData.register_time || '');

    // 角色标识：user = 普通用户, staff = 员工（后端返回 role 字段，默认 user）
    const userRole = loginData.role || 'user';
    wx.setStorageSync('user_role', userRole);

    // 手机号存储到本地（用于profile-edit页面读取）
    if (loginData.phone_number) {
      wx.setStorageSync('phone_number', loginData.phone_number);
    }

    // 存储用户信息到缓存
    const profile = {
      nickname: '',
      avatar: '',
      phone: loginData.phone_number || '',
      role: userRole
    };
    wx.setStorageSync('user_profile_cache', profile);

    // 延迟跳转到首页（员工和用户都到首页）
    setTimeout(() => {
      this.setData({ isLogging: false });
      wx.switchTab({ url: '/pages/index/index' });
    }, 800);
  },

  processImageUrl(imageUrl) {
    if (!imageUrl) return '';
    imageUrl = String(imageUrl).replace(/[`\s]/g, '');
    if (imageUrl.startsWith('http://') || imageUrl.startsWith('https://')) {
      if (imageUrl.includes('127.0.0.1:5000')) {
        return imageUrl.replace('http://127.0.0.1:5000', getApp().globalData.baseUrl || 'https://api.nengjifarm.com');
      }
      return imageUrl;
    }
    const baseUrl = getApp().globalData.baseUrl || 'https://api.nengjifarm.com';
    if (!imageUrl.startsWith('/')) imageUrl = '/' + imageUrl;
    return baseUrl + imageUrl;
  },

  viewAgreement() {
    wx.showModal({
      title: '用户协议',
      content: '欢迎使用稻田时光农场，登录即表示您已阅读并同意用户协议。',
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
  },

  // 背景图加载成功
  onBgImageLoad() {
    this.setData({ bgLoaded: true });
  },

  // 背景图加载失败
  onBgImageError(err) {
    this.setData({ 
      bgError: true,
      bgImage: ''
    });
  },

  onShareAppMessage: share.onShareAppMessage,
  onShareTimeline: share.onShareTimeline,

});
