Page({
  data: {
    farmInfo: null,
    loading: true
  },

  onLoad: function (options) {
    console.log('农场介绍页面加载');
    this.getFarmIntro();
  },

  // 获取农场介绍信息
  getFarmIntro: function () {
    wx.showLoading({ title: '加载中...' });
    
    const api = require('../../utils/api');
    api.request({
      url: '/api/farm/intro',
      method: 'GET'
    })
    .then(data => {
      this.setData({
        farmInfo: data,
        loading: false
      });
      wx.hideLoading();
    })
    .catch(err => {
      console.error('获取农场介绍信息失败:', err);
      wx.hideLoading();
      wx.showToast({ title: '加载失败，请重试', icon: 'none' });
      this.setData({ loading: false });
    });
  }
});