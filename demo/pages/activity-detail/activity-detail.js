const api = require('../../utils/api');

Page({
  data: {
    activity: {},
    loading: true
  },

  onLoad: function(options) {
    const activityId = options.id;
    if (activityId) {
      this.getActivityDetail(activityId);
    }
  },

  getActivityDetail: function(activityId) {
    wx.showLoading({ title: '加载中...', mask: true });

    api.request({
      url: '/api/activity/detail',
      method: 'GET',
      data: {
        id: activityId
      }
    })
      .then(data => {
        // 确保活动有图片，如果没有image字段但有images数组，使用第一张图片
        if (data && !data.image && data.images && data.images.length > 0) {
          data.image = data.images[0];
        }
        this.setData({
          activity: data || {},
          loading: false
        });
      })
      .catch(err => {
        wx.showToast({
          title: err.message || '活动详情加载失败',
          icon: 'none'
        });
        this.setData({ loading: false });
      })
      .finally(() => {
        wx.hideLoading();
      });
  },

  contactService: function() {
    wx.showToast({
      title: '联系客服功能开发中',
      icon: 'none'
    });
  },

  registerActivity: function() {
    wx.navigateTo({
      url: '/pages/buy/buy'
    });
  },

  previewImage: function() {
    const image = this.data.activity.image;
    if (image) {
      wx.previewImage({
        current: image,
        urls: [image]
      });
    }
  }
});
