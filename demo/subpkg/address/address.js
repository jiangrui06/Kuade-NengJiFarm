const api = require('../../utils/api');

Page({
  data: {
    addressList: []
  },

  onLoad: function (options) {
    this.setData({
      from: options.from || ''
    });
    this.getAddressList();
  },
  
  onShow: function () {
    // 页面显示时自动刷新地址列表
    this.getAddressList();
  },

  // ─────────────────────────────────────────
  // 地址管理逻辑
  // ─────────────────────────────────────────
  getAddressList: function () {
    wx.showLoading({ title: '加载中...' });
    api.request({
      url: '/api/user/address',
      method: 'GET'
    })
    .then(data => {
      this.setData({ addressList: data || [] });
    })
    .catch(err => {
      console.error('获取地址列表失败:', err);
      wx.showToast({ title: '加载失败', icon: 'none' });
    })
    .finally(() => {
      wx.hideLoading();
    });
  },

  selectAddress: function (e) {
    const addressId = e.currentTarget.dataset.id;
    
    // 如果是从购买页面跳转过来的，选择地址后返回购买页面
    if (this.data.from === 'buy') {
      wx.navigateBack({
        delta: 1,
        success: () => {
          // 触发上一个页面的地址选择事件
          const pages = getCurrentPages();
          const prevPage = pages[pages.length - 2];
          if (prevPage) {
            prevPage.setData({ selectedAddress: addressId });
          }
        }
      });
    } else {
      // 否则跳转到编辑页面
      wx.navigateTo({ url: `/subpkg/address-edit/address-edit?id=${addressId}` });
    }
  },

  addAddress: function () {
    wx.navigateTo({ url: '/subpkg/address-edit/address-edit' });
  },

  editAddress: function (e) {
    const addressId = e.currentTarget.dataset.id;
    wx.navigateTo({ url: `/subpkg/address-edit/address-edit?id=${addressId}` });
  },

  deleteAddress: function (e) {
    const addressId = e.currentTarget.dataset.id;
    wx.showModal({
      title: '确认删除',
      content: '确定要删除这个收货地址吗？',
      success: (res) => {
        if (res.confirm) {
          wx.showLoading({ title: '删除中...' });
          api.request({
            url: '/api/user/address',
            method: 'DELETE',
            data: { id: addressId }
          })
          .then(() => {
            wx.showToast({ title: '删除成功', icon: 'success' });
            this.getAddressList();
          })
          .catch(err => {
            console.error('删除地址失败:', err);
            wx.showToast({ title: '删除失败', icon: 'none' });
          })
          .finally(() => {
            wx.hideLoading();
          });
        }
      }
    });
  }
});
