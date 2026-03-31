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
    const keyword = e.detail.value;
    this.setData({ searchKeyword: keyword });

    if (keyword.trim()) {
      this.performSearch(keyword.trim());
    } else {
      // 使用原始数据的深拷贝，避免引用问题
      const originalActivities = this.data.originalActivities || {};
      this.setData({
        activities: {
          all: Array.isArray(originalActivities.all) ? [...originalActivities.all] : [],
          picking: Array.isArray(originalActivities.picking) ? [...originalActivities.picking] : [],
          camping: Array.isArray(originalActivities.camping) ? [...originalActivities.camping] : []
        }
      });
    }
  },

  onSearch: function() {
    const keyword = this.data.searchKeyword.trim();
    if (keyword) {
      this.performSearch(keyword);
    } else {
      this.setData({
        activities: this.data.originalActivities
      });
    }
  },

  performSearch: function(keyword) {
    wx.showLoading({ title: '搜索中...' });

    const originalActivities = this.data.originalActivities || {};

    // 过滤活动列表
    const filteredActivities = {
      all: (Array.isArray(originalActivities.all) ? originalActivities.all : []).filter(item => {
        const title = item.title || '';
        return title.includes(keyword);
      }),
      picking: (Array.isArray(originalActivities.picking) ? originalActivities.picking : []).filter(item => {
        const title = item.title || '';
        return title.includes(keyword);
      }),
      camping: (Array.isArray(originalActivities.camping) ? originalActivities.camping : []).filter(item => {
        const title = item.title || '';
        return title.includes(keyword);
      })
    };

    this.setData({
      activities: filteredActivities
    });

    wx.hideLoading();

    // 如果搜索结果为空，显示提示信息
    const currentTabActivities = filteredActivities[this.data.activeTab];
    if (currentTabActivities && currentTabActivities.length === 0) {
      wx.showToast({
        title: '没有找到相关活动',
        icon: 'none'
      });
    }
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
      url: '/subpkg/activity-detail/activity-detail?id=' + activityId
    });
  }
});
