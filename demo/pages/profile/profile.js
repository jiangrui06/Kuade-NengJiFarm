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
        avatar: cache.avatar || this.data.userInfo.avatar || '',
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

  getUserProfilePreview() {
    wx.showLoading({ title: '加载中...' });

    api.api.user.getInfo()
      .then(data => {
        const nextProfile = {
          nickname: data.nickname || '',
          avatar: data.avatar || '',
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
  }
})
