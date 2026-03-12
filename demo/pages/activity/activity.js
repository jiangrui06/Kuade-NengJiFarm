const api = require('../../utils/api');

Page({
  data: {
    activeTab: 'all',
    activities: {
      all: [],
      picking: [],
      camping: []
    }
  },

  onLoad: function() {
    this.getActivities();
  },

  getActivities: function() {
    wx.showLoading({ title: '加载中...', mask: true });

    api.request({
      url: '/api/activity/list',
      method: 'GET'
    })
      .then(data => {
        this.setData({
          activities: data.activities || {
            all: [],
            picking: [],
            camping: []
          }
        });
      })
      .catch(err => {
        wx.showToast({
          title: err.message || '活动加载失败',
          icon: 'none'
        });
      })
      .finally(() => {
        wx.hideLoading();
      });
  },

  switchTab: function(e) {
    const tab = e.currentTarget.dataset.tab;
    this.setData({
      activeTab: tab
    });
  },

  navigateToActivityDetail: function(e) {
    const activityId = e.currentTarget.dataset.id;
    wx.navigateTo({
      url: '/pages/activity-detail/activity-detail?id=' + activityId
    });
  }
});
