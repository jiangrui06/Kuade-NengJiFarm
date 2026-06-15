const api = require('../../utils/api');
const share = require('../../utils/share');

Page({
  data: {
    loading: false,
    userInfo: {
      nickname: '',
      avatar: '',
      email: '',
      balance: 0,
      reward: 0
    },
    recommendImage: '',
    isStaff: false,
    isLoggedIn: false
  },

  onLoad: function () {
    const token = wx.getStorageSync('token');
    if (token) {
      this.getUserProfilePreview();
    }
    this.getRecommendImage();
  },

  onShow: function () {
    const token = wx.getStorageSync('token');
    const role = wx.getStorageSync('user_role');
    const isLoggedIn = !!token;

    this.setData({
      isLoggedIn,
      isStaff: role === 'staff'
    });

    // 初始化自定义 tabBar
    if (typeof this.getTabBar === 'function' && this.getTabBar()) {
      this.getTabBar().init();
    }

    if (isLoggedIn) {
      this.syncUserProfileFromCache();
      this.getUserProfilePreview();
    }
  },

  syncUserProfileFromCache() {
    const cache = wx.getStorageSync('user_profile_cache') || null;
    if (!cache) {
      return;
    }

    this.setData({
      userInfo: {
        nickname: cache.nickname || this.data.userInfo.nickname || '',
        avatar: this.processImageUrl(cache.avatar || this.data.userInfo.avatar || ''),
        email: cache.email || this.data.userInfo.email || '',
        balance: this.data.userInfo.balance || 0,
        reward: this.data.userInfo.reward || 0
      }
    });
  },
  
  // 获取推荐图片
  getRecommendImage: function() {
    const utils = require('../../utils/utils');
    this.setData({
      recommendImage: utils.media.processUrl('farm_0000000000007.jpg')
    });
  },

  // 处理图片路径，确保使用正确的基础 URL
  processImageUrl: function (imageUrl) {
    const utils = require('../../utils/utils');
    return utils.media.processUrl(imageUrl);
  },

  getUserProfilePreview() {
    const token = wx.getStorageSync('token');
    // 测试模式下没有 token，跳过接口请求
    if (!token) {
      this.setData({ loading: false });
      return;
    }
    this.setData({ loading: true });

    Promise.all([
      api.user.getProfile(null, { showLoading: false }),
      new Promise(resolve => setTimeout(resolve, 1000))
    ])
      .then(([data]) => {
        const nextProfile = {
          nickname: data.nickname || '',
          avatar: this.processImageUrl(data.avatar || ''),
          email: data.email || '',
          balance: Number(data.balance || 0),
          reward: Number(data.reward || 0)
        };

        const role = data.role || wx.getStorageSync('user_role') || 'user';
        wx.setStorageSync('user_role', role);

        wx.setStorageSync('user_profile_cache', nextProfile);

        this.setData({
          userInfo: nextProfile,
          isStaff: role === 'staff',
          loading: false
        });
      })
      .catch(err => {
        this.setData({ loading: false });
        // 401 场景 api.js 已经处理了跳登录，不需要再弹 toast
        if (err && err.code === 401) return;
        wx.showToast({
          title: '加载失败',
          icon: 'none'
        });
      });
  },



  updateProfile(nickname, avatar, email) {
    this.setData({ loading: true });

    api.user.updateProfile({
      nickname: nickname,
      avatar: avatar,
      email: email
    })
      .then(data => {
        // 检查数据是否存在
        if (data) {
          this.setData({
            'userInfo.nickname': data.nickname || this.data.userInfo.nickname,
            'userInfo.avatar': data.avatar ? this.processImageUrl(data.avatar) : this.data.userInfo.avatar,
            'userInfo.email': data.email || this.data.userInfo.email
          });
        }
        wx.showToast({
          title: '保存成功',
          icon: 'success'
        });
      })
      .catch(err => {
        wx.showToast({
          title: '保存失败',
          icon: 'none'
        });
      })
      .finally(() => {
        this.setData({ loading: false });
      });
  },

  editProfile() {
    wx.navigateTo({
      url: '/user-pages/profile-edit/profile-edit',
      events: {
        // 监听 profile-edit 页面发来的更新事件
        profileUpdated: (data) => {
          if (data) {
            this.setData({
              'userInfo.nickname': data.nickname || this.data.userInfo.nickname,
              'userInfo.avatar': this.processImageUrl(data.avatar || ''),
            });
          }
        }
      }
    });
  },

  // 未登录时跳转登录页
  goToLogin() {
    wx.navigateTo({
      url: '/pages/login/login'
    });
  },

  // 检查登录状态，未登录则跳转
  checkLogin() {
    if (this.data.isLoggedIn) return true;
    wx.showToast({ title: '请先登录', icon: 'none' });
    setTimeout(() => {
      wx.navigateTo({ url: '/pages/login/login' });
    }, 500);
    return false;
  },

  navigateToOrders(e) {
    if (!this.checkLogin()) return;
    const tab = e.currentTarget.dataset.tab;
    wx.navigateTo({
      url: `/user-pages/orders/orders?tab=${tab}`
    });
  },

  navigateToAddress() {
    if (!this.checkLogin()) return;
    wx.navigateTo({
      url: '/user-pages/address/address'
    });
  },

  navigateToPoints() {
    const { checkLogin } = require('../../utils/api');
    if (!checkLogin()) return;
    wx.navigateTo({
      url: '/user-pages/points-mall/points-mall'
    });
  },

  navigateToPayment() {
    wx.showToast({
      title: '功能开发中',
      icon: 'none'
    });
  },


  navigateToFarmIntro() {
    wx.navigateTo({
      url: '/user-pages/farm-intro/farm-intro'
    });
  },

  // 员工跳转核销工作台
  goToStaffVerify() {
    wx.navigateTo({
      url: '/staff-pages/staff-verify/staff-verify'
    });
  },

  // 扫码核销
  scanVerify() {
    wx.scanCode({
      success: (res) => {
        const result = res.result;
        if (result) {
          try {
            const data = JSON.parse(result);
            if (data.orderId || data.id || data.code) {
              wx.navigateTo({
                url: `/staff-pages/staff-verify/staff-verify?orderId=${data.orderId || data.id || data.code}`
              });
            } else {
              wx.showToast({ title: '无效的核销码', icon: 'none' });
            }
          } catch (e) {
            wx.showToast({ title: '无效的核销码', icon: 'none' });
          }
        }
      },
      fail: (err) => {
        wx.showToast({ title: '扫码失败', icon: 'none' });
      }
    });
  },

  // 选择头像（使用微信官方组件）
  onChooseAvatar: function (e) {
    const avatarUrl = e.detail.avatarUrl;
    const app = getApp();
    const baseUrl = app.globalData.baseUrl || 'https://api.nengjifarm.com';

    // 获取 token
    const token = wx.getStorageSync('token');

    // 上传图片到服务器
    wx.uploadFile({
      url: baseUrl + '/api/file/upload/avatar',
      filePath: avatarUrl,
      name: 'file',
      header: {
        Authorization: 'Bearer ' + token
      },
      success: (res) => {
        try {
          // 检查响应数据是否为空
          if (!res.data || res.data.trim() === '') {
            wx.showToast({ title: '上传失败', icon: 'none' });
            return;
          }
          
          const data = JSON.parse(res.data);
          if (data.code === 0) {
            // 使用服务器返回的图片URL（永久地址）
            const newAvatarUrl = data.data.url;
            this.setData({
              'userInfo.avatar': newAvatarUrl
            });
            // 同步更新本地缓存
            const cache = wx.getStorageSync('user_profile_cache') || {};
            cache.avatar = newAvatarUrl;
            wx.setStorageSync('user_profile_cache', cache);
            // 更新到服务器
            this.updateProfile(this.data.userInfo.nickname, newAvatarUrl, this.data.userInfo.email);
            wx.showToast({ title: '上传成功', icon: 'success' });
          } else {
            wx.showToast({ title: data.message || '上传失败', icon: 'none' });
          }
        } catch (e) {
          wx.showToast({ title: '上传失败', icon: 'none' });
        }
      },
      fail: (err) => {
        wx.showToast({ title: '上传失败', icon: 'none' });
      }
    });
  },

  // 跳转个人设置
  navigateToSettings() {
    wx.navigateTo({
      url: '/user-pages/profile-edit/profile-edit'
    });
  },

  // 退出登录，清空全部本地数据
  logout() {
    wx.showModal({
      title: '退出登录',
      content: '确定要退出登录吗？将清空所有本地数据。',
      success: (res) => {
        if (res.confirm) {
          // 清空全部本地存储（包括 user_role）
          wx.clearStorage();

          // 跳转到首页
          wx.switchTab({ url: '/pages/index/index' });
        }
      }
    });
  },

  // 下拉刷新
  onPullDownRefresh() {
    this.setData({ loading: true });
    this.getUserProfilePreview();
  },

  onShareAppMessage: share.onShareAppMessage,
  onShareTimeline: share.onShareTimeline,

})

