const api = require('../../utils/api');

Page({
  data: {
    userInfo: {
      nickname: '',
      avatar: '',
      email: '',
      balance: 0,
      reward: 0
    },
    recommendImage: '',
    isStaff: false
  },

  onLoad: function () {
    console.log('个人中心加载')
    this.getUserProfilePreview();
    this.getRecommendImage();
  },

  onShow: function () {
    // 未登录时跳登录页（测试模式下 user_role 也放行）
    const token = wx.getStorageSync('token');
    const role = wx.getStorageSync('user_role');
    if (!token && role !== 'staff') {
      wx.reLaunch({ url: '/pages/login/login' });
      return;
    }
    // 检查角色
    this.setData({ isStaff: role === 'staff' });

    // 初始化自定义 tabBar
    if (typeof this.getTabBar === 'function' && this.getTabBar()) {
      this.getTabBar().init();
    }

    this.syncUserProfileFromCache();
    this.getUserProfilePreview();
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
    // 使用新的图片路径获取图片
    const recommendImageUrl = 'http://192.168.203.56/api/file/image/farm_0000000000007.jpg';
    this.setData({
      recommendImage: recommendImageUrl
    });
  },

  // 处理图片路径，确保使用正确的基础 URL
  processImageUrl: function (imageUrl) {
    if (!imageUrl) return '';
    
    // 去除反引号和空格
    imageUrl = imageUrl.replace(/[`\s]/g, '');
    
    // 如果是完整的 URL，替换基础 URL
    if (imageUrl.startsWith('http://') || imageUrl.startsWith('https://')) {
      return imageUrl;
    }
    
    // 如果是相对路径，添加基础 URL
    const baseUrl = 'http://192.168.203.56';
    if (!imageUrl.startsWith('/')) {
      imageUrl = '/' + imageUrl;
    }
    return baseUrl + imageUrl;
  },

  getUserProfilePreview() {
    const token = wx.getStorageSync('token');
    // 测试模式下没有 token，跳过接口请求
    if (!token) {
      this.setData({ loading: false });
      return;
    }
    wx.showLoading({ title: '加载中...' });

    api.user.getProfile()
      .then(data => {
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
        console.error('获取个人资料预览失败:', err);
        this.setData({ loading: false });
        wx.showToast({
          title: '加载失败',
          icon: 'none'
        });
      })
      .finally(() => {
        wx.hideLoading();
      });
  },



  updateProfile(nickname, avatar, email) {
    wx.showLoading({ title: '保存中...' });

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
        console.error('更新个人资料失败:', err);
        wx.showToast({
          title: '保存失败',
          icon: 'none'
        });
      })
      .finally(() => {
        wx.hideLoading();
      });
  },

  editProfile() {
    wx.navigateTo({
      url: '/user-pages/profile-edit/profile-edit',
      events: {
        // 监听 profile-edit 页面发来的更新事件
        profileUpdated: (data) => {
          console.log('收到资料更新通知:', data);
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

  navigateToOrders(e) {
    const tab = e.currentTarget.dataset.tab;
    wx.navigateTo({
      url: `/user-pages/orders/orders?tab=${tab}`
    });
  },

  navigateToAddress() {
    wx.navigateTo({
      url: '/user-pages/address/address'
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
        console.log('扫码结果:', res);
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
        console.error('扫码失败:', err);
        wx.showToast({ title: '扫码失败', icon: 'none' });
      }
    });
  },

  // 选择头像（使用微信官方组件）
  onChooseAvatar: function (e) {
    const avatarUrl = e.detail.avatarUrl;
    console.log('选择的头像临时路径', avatarUrl);
    
    // 获取 token
    const token = wx.getStorageSync('token');
    
    // 上传图片到服务器
    wx.uploadFile({
      url: 'http://192.168.203.56/api/file/upload/avatar',
      filePath: avatarUrl,
      name: 'file',
      header: {
        Authorization: 'Bearer ' + token
      },
      success: (res) => {
        console.log('上传成功响应:', res);
        try {
          // 检查响应数据是否为空
          if (!res.data || res.data.trim() === '') {
            console.error('服务器返回空响应');
            wx.showToast({ title: '上传失败', icon: 'none' });
            return;
          }
          
          const data = JSON.parse(res.data);
          console.log('解析后的响应数据:', data);
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
            console.error('上传头像失败:', data.message);
            wx.showToast({ title: data.message || '上传失败', icon: 'none' });
          }
        } catch (e) {
          console.error('解析上传结果失败:', e);
          wx.showToast({ title: '上传失败', icon: 'none' });
        }
      },
      fail: (err) => {
        console.error('上传头像失败:', err);
        wx.showToast({ title: '上传失败', icon: 'none' });
      }
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

          // 清空页面栈，跳转到登录页
          wx.reLaunch({
            url: '/pages/login/login'
          });
        }
      }
    });
  },

  // 下拉刷新
  onPullDownRefresh() {
    console.log('下拉刷新个人中心');
    this.getUserProfilePreview();
    // 刷新完成后停止下拉刷新
    setTimeout(() => {
      wx.stopPullDownRefresh();
    }, 1000);
  }
})

