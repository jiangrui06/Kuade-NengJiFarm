const api = require('../../utils/api');

Page({
  data: {
    activeTab: 'all',
    searchKeyword: '',
    originalActivities: {
      all: [],
      picking: [],
      camping: []
    },
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
        const activities = data.activities || {
          all: [],
          picking: [],
          camping: []
        };
        this.setData({
          originalActivities: activities,
          activities: activities
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

  onSearchInput: function(e) {
    this.setData({
      searchKeyword: e.detail.value
    });
  },

  onSearch: function() {
    const keyword = this.data.searchKeyword.trim();
    const originalActivities = this.data.originalActivities;

    if (!keyword) {
      this.setData({
        activities: originalActivities
      });
      return;
    }

    // 过滤活动列表
    const filteredActivities = {
      all: originalActivities.all.filter(item => item.title && item.title.includes(keyword)),
      picking: originalActivities.picking.filter(item => item.title && item.title.includes(keyword)),
      camping: originalActivities.camping.filter(item => item.title && item.title.includes(keyword))
    };

    this.setData({
      activities: filteredActivities
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
