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
    recommendImage: ''
  },

  onLoad: function () {
    console.log('个人中心加载')
    this.getUserProfilePreview();
    this.getRecommendImage();
  },

  onShow: function () {
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
      // 只替换 127.0.0.1:5000 为 192.168.203.56，不影响其他URL
      if (imageUrl.includes('127.0.0.1:5000')) {
        imageUrl = imageUrl.replace('127.0.0.1:5000', '192.168.203.56');
      }
      // 如果已经是正确的URL格式，直接返回
      return imageUrl;
    }
    
    // 如果是相对路径，添加基础 URL
    // 确保基础 URL 后面有斜杠
    const baseUrl = 'http://192.168.203.56';
    // 确保图片路径以斜杠开头
    if (!imageUrl.startsWith('/')) {
      imageUrl = '/' + imageUrl;
    }
    return baseUrl + imageUrl;
  },

  getUserProfilePreview() {
    wx.showLoading({ title: '加载中...' });

    api.api.user.getInfo()
      .then(data => {
        const nextProfile = {
          nickname: data.nickname || '',
          avatar: this.processImageUrl(data.avatar || ''),
          email: data.email || '',
          balance: Number(data.balance || 0),
          reward: Number(data.reward || 0)
        };

        wx.setStorageSync('user_profile_cache', nextProfile);

        this.setData({
          userInfo: nextProfile,
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

    api.api.user.updateInfo({
      nickname: nickname,
      avatar: avatar,
      email: email
    })
      .then(data => {
        this.setData({
          'userInfo.nickname': data.nickname,
          'userInfo.avatar': data.avatar,
          'userInfo.email': data.email
        });
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
      url: '/subpkg/profile-edit/profile-edit'
    });
  },

  navigateToOrders(e) {
    const tab = e.currentTarget.dataset.tab;
    wx.navigateTo({
      url: `/subpkg/orders/orders?tab=${tab}`
    });
  },

  navigateToAddress() {
    wx.navigateTo({
      url: '/subpkg/address/address'
    });
  },

  navigateToPayment() {
    wx.showToast({
      title: '功能开发中',
      icon: 'none'
    });
  },

  navigateToService() {
    wx.showModal({
      title: '能记家庭农场客服',
      content: '手机号：15876534944\n     微信号：njjtnc15876534944',
      showCancel: false
    });
  },

  navigateToFarmIntro() {
    wx.navigateTo({
      url: '/subpkg/farm-intro/farm-intro'
    });
  },

  // 选择头像（使用微信官方组件）
  onChooseAvatar: function (e) {
    const avatarUrl = e.detail.avatarUrl;
    
    // 上传图片到服务器
    wx.uploadFile({
      url: 'http://192.168.203.56/api/upload',
      filePath: avatarUrl,
      name: 'file',
      success: (res) => {
        try {
          // 检查响应数据是否为空
          if (!res.data || res.data.trim() === '') {
            console.error('服务器返回空响应');
            // 直接使用临时路径作为 fallback
            this.setData({
              'userInfo.avatar': avatarUrl
            });
            return;
          }
          
          const data = JSON.parse(res.data);
          if (data.code === 0) {
            // 使用服务器返回的图片URL
            this.setData({
              'userInfo.avatar': this.processImageUrl(data.data.url)
            });
            // 更新到服务器
            this.updateProfile(this.data.userInfo.nickname, this.processImageUrl(data.data.url), this.data.userInfo.email);
          } else {
            console.error('上传头像失败:', data.message);
            wx.showToast({ title: '上传头像失败', icon: 'none' });
            // 失败时使用临时路径作为 fallback
            this.setData({
              'userInfo.avatar': avatarUrl
            });
          }
        } catch (e) {
          console.error('解析上传结果失败:', e);
          wx.showToast({ title: '上传头像失败', icon: 'none' });
          // 失败时使用临时路径作为 fallback
          this.setData({
            'userInfo.avatar': avatarUrl
          });
        }
      },
      fail: (err) => {
        console.error('上传头像失败:', err);
        wx.showToast({ title: '上传头像失败', icon: 'none' });
        // 失败时使用临时路径作为 fallback
        this.setData({
          'userInfo.avatar': avatarUrl
        });
      }
    });
  },

  // 退出登录
  logout() {
    wx.showModal({
      title: '退出登录',
      content: '确定要退出登录吗？',
      success: (res) => {
        if (res.confirm) {
          // 清除所有与登录相关的本地存储
          wx.removeStorageSync('user_profile_cache');
          wx.removeStorageSync('token');
          wx.removeStorageSync('hasLogin');
          wx.removeStorageSync('user_id');
          wx.removeStorageSync('user_guid');
          wx.removeStorageSync('openid');
          wx.removeStorageSync('register_time');
          // 跳转到登录页面
          wx.redirectTo({
            url: '/pages/login/login'
          });
        }
      }
    });
  }
})
