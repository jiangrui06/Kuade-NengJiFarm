const api = require('../../utils/api');

Page({
  data: {
    isLogging: false,
    statusBarHeight: 20,
    bgImage: '',
    username: '',
    password: '',
    showPassword: false
  },

  onLoad() {
    // 获取状态栏高度
    const sysInfo = wx.getWindowInfo ? wx.getWindowInfo() : wx.getSystemInfoSync();
    this.setData({ statusBarHeight: sysInfo.statusBarHeight });

    // 加载背景图
    this.getBackgroundImage();

    // 已登录的员工直接跳转
    const token = wx.getStorageSync('token');
    const role = wx.getStorageSync('user_role');
    if (token && role === 'staff') {
      wx.redirectTo({ url: '/staff-pages/staff-home/staff-home' });
    }
  },

  getBackgroundImage() {
    // 暂时不设置背景图，避免404
    this.setData({ bgImage: '' });
  },

  // 用户名输入
  onUsernameInput(e) {
    this.setData({ username: e.detail.value });
  },

  // 密码输入
  onPasswordInput(e) {
    this.setData({ password: e.detail.value });
  },

  // 切换密码显示
  togglePassword() {
    this.setData({ showPassword: !this.data.showPassword });
  },

  // 员工登录
  staffLogin() {
    if (this.data.isLogging) return;

    // 验证输入
    if (!this.data.username.trim()) {
      wx.showToast({ title: '请输入用户名', icon: 'none' });
      return;
    }
    if (!this.data.password.trim()) {
      wx.showToast({ title: '请输入密码', icon: 'none' });
      return;
    }

    this.setData({ isLogging: true });
    wx.showLoading({ title: '登录中...', mask: true });

    // 调用员工登录API
    api.staff.login(this.data.username, this.data.password)
      .then(loginData => {
        console.log('员工登录成功:', loginData);

        // 存储登录信息
        wx.setStorageSync('token', loginData.token);
        wx.setStorageSync('hasLogin', true);
        wx.setStorageSync('user_id', loginData.user_id);
        wx.setStorageSync('user_role', 'staff');
        wx.setStorageSync('phone_number', loginData.phone);

        // 存储用户信息到缓存
        const profile = {
          nickname: loginData.nickname,
          avatar: loginData.avatar,
          phone: loginData.phone,
          role: 'staff'
        };
        wx.setStorageSync('user_profile_cache', profile);

        wx.hideLoading();
        wx.showToast({ title: '登录成功', icon: 'success' });

        setTimeout(() => {
          wx.redirectTo({ url: '/staff-pages/staff-home/staff-home' });
        }, 800);
      })
      .catch(err => {
        console.error('员工登录失败:', err);
        this.setData({ isLogging: false });
        wx.hideLoading();
        wx.showToast({ title: err.message || '登录失败，请重试', icon: 'none', duration: 2000 });
      });
  },

  // 返回用户登录
  goToUserLogin() {
    wx.redirectTo({ url: '/pages/login/login' });
  }
});