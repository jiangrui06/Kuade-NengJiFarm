const api = require('../../utils/api');

Page({
  data: {
    activeTab: 'all',
    searchKeyword: '',
    originalActivities: {
      all: []
    },
    activities: {
      all: []
    },
    categories: [] // 存储动态分类
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
        // 处理API返回的数据
        let allActivities = [];
        let categories = [];
        
        // 检查API返回的数据结构
        if (data.activities && data.activities.all) {
          // 旧结构：activities.all 是活动列表
          allActivities = data.activities.all;
        } else if (Array.isArray(data.activities)) {
          // 新结构：activities 直接是活动列表
          allActivities = data.activities;
        } else if (Array.isArray(data)) {
          // 更简单的结构：直接返回活动列表
          allActivities = data;
        }
        
        // 根据 categoryName 分类
        const categorizedActivities = {
          all: allActivities
        };
        
        // 提取所有唯一的分类名称
        const categorySet = new Set();
        allActivities.forEach(activity => {
          // 清理价格中的符号
          if (typeof activity.price === 'string') {
            activity.price = activity.price.replace(/[¥￥]/g, '');
          }
          if (activity.categoryName) {
            categorySet.add(activity.categoryName);
            // 按分类名称分组
            if (!categorizedActivities[activity.categoryName]) {
              categorizedActivities[activity.categoryName] = [];
            }
            categorizedActivities[activity.categoryName].push(activity);
          }
        });
        
        // 转换为分类数组
        categories = Array.from(categorySet).map(categoryName => ({
          id: categoryName,
          name: categoryName
        }));
        
        this.setData({
          originalActivities: categorizedActivities,
          activities: categorizedActivities,
          categories: categories
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
      // 恢复原始数据
      this.setData({
        activities: this.data.originalActivities
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
      })
    };

    // 过滤每个分类
    this.data.categories.forEach(category => {
      const categoryActivities = Array.isArray(originalActivities[category.id]) ? originalActivities[category.id] : [];
      filteredActivities[category.id] = categoryActivities.filter(item => {
        const title = item.title || '';
        return title.includes(keyword);
      });
    });

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
