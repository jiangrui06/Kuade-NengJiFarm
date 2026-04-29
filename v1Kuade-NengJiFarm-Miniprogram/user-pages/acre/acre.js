Page({
  data: {
    acreList: [],
    swiperList: []
  },

  onLoad: function () {
    // 从后台加载数据
    this.loadAcreData();
  },

  loadAcreData: function () {
    const api = require('../../utils/api');

    wx.showLoading({
      title: '加载中...',
    });

    api.request({
      url: '/api/acres/index',
      method: 'GET'
    })
      .then(res => {
        wx.hideLoading();
        // 清理价格中的符号
        const cleanedList = (res.list || []).map(item => ({
          ...item,
          price: typeof item.price === 'string' ? item.price.replace(/[¥￥]/g, '') : item.price
        }));
        this.setData({
          acreList: cleanedList,
          swiperList: res.swiperList
        });
      })
      .catch(err => {
        wx.hideLoading();
        wx.showToast({ title: '加载失败', icon: 'none' });
      });
  },

  navigateToDetail: function (e) {
    const id = e.currentTarget.dataset.id;
    wx.navigateTo({
      url: '/user-pages/acre-detail/acre-detail?id=' + id
    });
  }
});

