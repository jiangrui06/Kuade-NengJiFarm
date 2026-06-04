const api = require('../../utils/api');

Page({
  data: {
    addressList: []
  },

  onLoad: function (options) {
    // 登录检查
    const { checkLogin } = require('../../utils/api');
    if (!checkLogin()) return;

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
      wx.showToast({ title: '加载失败', icon: 'none' });
    })
    .finally(() => {
      wx.hideLoading();
    });
  },

  selectAddress: function (e) {
    const addressId = e.currentTarget.dataset.id;

    // 如果是从购买页面跳转过来的，选择地址后返回购买页
    if (this.data.from === 'buy') {
      // 将选中的地址ID存储到storage，供上一个页面读取
      wx.setStorageSync('selectedAddressId', addressId);
      wx.navigateBack({
        delta: 1
      });
    } else {
      // 否则跳转到编辑页
      wx.navigateTo({ url: `/user-pages/address-edit/address-edit?id=${addressId}` });
    }
  },

  addAddress: function () {
    wx.navigateTo({ url: '/user-pages/address-edit/address-edit' });
  },

  editAddress: function (e) {
    const addressId = e.currentTarget.dataset.id;
    wx.navigateTo({ url: `/user-pages/address-edit/address-edit?id=${addressId}` });
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
            url: '/api/user/address-delete',
            method: 'POST',
            data: { Id: Number(addressId) },
            showLoading: false
          })
          .then(() => {
            wx.hideLoading();
            wx.showToast({ title: '删除成功', icon: 'success' });
            this.getAddressList();
          })
          .catch(err => {
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

