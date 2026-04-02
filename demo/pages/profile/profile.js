const api = require('../../utils/api');

Page({
  data: {
    recommendImage: ''
  },

  onLoad: function () {
    console.log('个人中心加载')
    this.getUserProfilePreview();
    this.getRecommendImage();
  },
  
  // 获取推荐图片
  getRecommendImage: function() {
    // 使用新的图片路径获取图片
    const BASE_URL = 'http://192.168.203.56';
    const imagePath = '/api/file/image/farm_0000000000007.jpg';
    const recommendImageUrl = BASE_URL + imagePath;
    this.setData({
      recommendImage: recommendImageUrl
    });
  },

  getUserProfilePreview() {
    wx.showLoading({ title: '加载中...' });

    api.request({
      url: '/api/user/profile-preview',
      method: 'GET'
    })
      .then(data => {
        this.setData({
          userInfo: {
            nickname: data.nickname,
            avatar: data.avatar ,
            email: data.email ,
            balance: data.balance ,
            reward: data.reward 
          },
          orderCounts: data.orderCounts || {
            pending: 0,
            paid: 0,
            shipping: 0,
            refund: 0
          },
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

  getUserProfile() {
    wx.showLoading({ title: '加载中...' });

    api.request({
      url: '/api/user/profile',
      method: 'GET'
    })
      .then(data => {
        this.setData({
          userInfo: {
            nickname: data.nickname,
            avatar: data.avatar ,
            email: data.email,
            balance: data.balance ,
            reward: data.reward 
          },
          orderCounts: data.orderCounts || {
            pending: 0,
            paid: 0,
            shipping: 0,
            refund: 0
          }
        });
      })
      .catch(err => {
        console.error('获取个人资料失败:', err);
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

    api.request({
      url: '/api/user/profile',
      method: 'PUT',
      data: {
        nickname: nickname,
        avatar: avatar,
        email: email
      }
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