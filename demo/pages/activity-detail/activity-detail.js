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
    wx.showToast({
      title: '报名功能开发中',
      icon: 'none'
    });
  },

  previewImage: function(e) {
    const index = e.currentTarget.dataset.index;
    const images = this.data.activity.images || [];
    if (images.length === 0) {
      return;
    }

    wx.previewImage({
      current: images[index],
      urls: images
    });
  }
});
