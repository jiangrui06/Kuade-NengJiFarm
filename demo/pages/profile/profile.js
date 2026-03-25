const api = require('../../utils/api');

Page({
  data: {
    profileItems: [
      { id: 1, name: '个人信息' },
      { id: 2, name: '订单管理' },
      { id: 3, name: '收货地址' },
      { id: 4, name: '设置' }
    ],
    userInfo: {
      nickname: '李银河',
      avatar: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=user%20avatar&image_size=square',
      email: '',
      balance: 8888.88,
      reward: 2000.00
    },
    orderCounts: {
      pending: 1,
      paid: 0,
      shipping: 0,
      refund: 0
    },
    loading: true
  },

  onLoad: function () {
    console.log('个人中心加载')
    this.getUserProfilePreview();
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
            nickname: data.nickname || '用户',
            avatar: data.avatar || 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=user%20avatar&image_size=square',
            email: data.email || '',
            balance: data.balance || 0,
            reward: data.reward || 0
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
            nickname: data.nickname || '用户',
            avatar: data.avatar || 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=user%20avatar&image_size=square',
            email: data.email || '',
            balance: data.balance || 0,
            reward: data.reward || 0
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
    const userInfo = this.data.userInfo;
    
    wx.showModal({
      title: '编辑个人资料',
      content: '是否要修改昵称和头像？',
      success: (res) => {
        if (res.confirm) {
          this.updateProfile(userInfo.nickname, userInfo.avatar, userInfo.email);
        }
      }
    });
  },

  navigateToOrders(e) {
    const tab = e.currentTarget.dataset.tab;
    wx.navigateTo({
      url: `/pages/orders/orders?tab=${tab}`
    });
  },

  navigateToAddress() {
    wx.showToast({
      title: '功能开发中',
      icon: 'none'
    });
  },

  navigateToPayment() {
    wx.showToast({
      title: '功能开发中',
      icon: 'none'
    });
  },

  navigateToFavorites() {
    wx.showToast({
      title: '功能开发中',
      icon: 'none'
    });
  },

  navigateToService() {
    wx.showToast({
      title: '功能开发中',
      icon: 'none'
    });
  },

  navigateToFeedback() {
    wx.showToast({
      title: '功能开发中',
      icon: 'none'
    });
  }
})