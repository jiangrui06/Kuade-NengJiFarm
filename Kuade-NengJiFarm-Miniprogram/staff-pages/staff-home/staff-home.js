const api = require('../../utils/api');

Page({
  data: {
    staffName: '',
    todayVerifyCount: 0
  },

  onLoad() {
    // 检查角色
    const role = wx.getStorageSync('user_role');
    if (role !== 'staff') {
      wx.showModal({
        title: '无权限',
        content: '仅员工账号可访问',
        showCancel: false,
        success: () => { wx.navigateBack(); }
      });
      return;
    }
  },

  onShow() {
    this.loadStaffInfo();
    this.loadTodayCount();
  },

  loadStaffInfo() {
    const cache = wx.getStorageSync('user_profile_cache');
    if (cache && cache.nickname) {
      this.setData({ staffName: cache.nickname });
    }
  },

  loadTodayCount() {
    const token = wx.getStorageSync('token');
    // 测试模式下没有 token，跳过接口请求
    if (!token) {
      this.setData({ todayVerifyCount: 0 });
      return;
    }
    api.api.staff.getVerifyHistory()
      .then(list => {
        this.setData({ todayVerifyCount: (list || []).length });
      })
      .catch(() => {});
  },

  // 跳转核销页面
  goToVerify() {
    wx.navigateTo({ url: '/staff-pages/staff-verify/staff-verify' });
  },

  // 跳转我的
  goToProfile() {
    wx.switchTab({ url: '/pages/profile/profile' });
  },

  // 退出登录
  logout() {
    wx.showModal({
      title: '退出登录',
      content: '确定要退出登录吗？',
      success: (res) => {
        if (res.confirm) {
          wx.clearStorage();
          wx.reLaunch({ url: '/pages/login/login' });
        }
      }
    });
  }
});
