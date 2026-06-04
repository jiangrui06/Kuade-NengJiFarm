const api = require('../../utils/api');
const share = require('../../utils/share');

Page({
  data: {
    activeTab: 'all',
    searchKeyword: '',
    originalActivities: { all: [] },
    activities: { all: [] },
    categories: [],
    // 分页参数
    page: 1,
    pageSize: 10,
    hasMore: true
  },

  onLoad: function() {
    this.getActivities();
    if (typeof this.getTabBar === 'function' && this.getTabBar()) {
      this.getTabBar().init();
    }
  },

  onShow() {
    if (typeof this.getTabBar === 'function' && this.getTabBar()) {
      this.getTabBar().init();
    }
  },

  /** 下拉刷新（仅刷新，不加载更多） */
  onPullDownRefresh() {
    this.setData({ page: 1, hasMore: true });
    this.getActivities(1);
    setTimeout(() => {
      wx.stopPullDownRefresh();
    }, 1000);
  },

  getActivities: function(page = 1) {
    const isFirstLoad = page === 1;

    if (isFirstLoad) {
      wx.showLoading({ title: '加载中...', mask: true });
    }

    const utils = require('../../utils/utils');

    api.request({
      url: '/api/activity/list',
      method: 'GET',
      data: { page, pageSize: this.data.pageSize },
      skipAuthCheck: true
    })
      .then(data => {
        // 处理API返回的数据
        let rawActivities = [];

        if (data.activities && data.activities.all) {
          rawActivities = data.activities.all;
        } else if (Array.isArray(data.activities)) {
          rawActivities = data.activities;
        } else if (Array.isArray(data)) {
          rawActivities = data;
        }

        // 处理图片路径和价格
        rawActivities = rawActivities.map(activity => ({
          ...activity,
          image: utils.media.processUrl(activity.image),
          price: typeof activity.price === 'string'
            ? activity.price.replace(/[¥￥]/g, '')
            : activity.price
        }));

        // 首次加载或下拉刷新 → 直接用全部数据；加载更多 → 去重过滤
        let newActivities;
        if (isFirstLoad) {
          newActivities = rawActivities;
        } else {
          const loadedIds = new Set((this.data.activities.all || []).map(a => a.id));
          newActivities = rawActivities.filter(a => !loadedIds.has(a.id));
        }

        // 如果没有新数据，说明已经全部加载完毕
        if (newActivities.length === 0) {
          this.setData({ hasMore: false });
          wx.hideLoading();
          return;
        }

        const hasMore = page * this.data.pageSize < (data.total || 0);

        if (isFirstLoad) {
          // 首次加载 → 完整分类
          const categorized = { all: [...newActivities] };
          const categorySet = new Set();

          newActivities.forEach(activity => {
            if (activity.categoryName) {
              categorySet.add(activity.categoryName);
              if (!categorized[activity.categoryName]) {
                categorized[activity.categoryName] = [];
              }
              categorized[activity.categoryName].push(activity);
            }
          });

          const categories = Array.from(categorySet).map(name => ({ id: name, name }));

          this.setData({
            originalActivities: categorized,
            activities: categorized,
            categories,
            page,
            hasMore
          });
        } else {
          // 追加加载 → 合并到已有分类
          const currentActivities = { ...this.data.activities };
          currentActivities.all = [...(currentActivities.all || []), ...newActivities];

          // 合并分类
          newActivities.forEach(activity => {
            if (activity.categoryName) {
              if (!currentActivities[activity.categoryName]) {
                currentActivities[activity.categoryName] = [];
              }
              currentActivities[activity.categoryName].push(activity);
            }
          });

          // 更新分类列表（可能新增了分类）
          const categorySet = new Set(this.data.categories.map(c => c.id));
          newActivities.forEach(activity => {
            if (activity.categoryName) {
              categorySet.add(activity.categoryName);
            }
          });
          const categories = Array.from(categorySet).map(name => ({ id: name, name }));

          // 同时更新 originalActivities 用于搜索
          const currentOriginal = { ...this.data.originalActivities };
          currentOriginal.all = [...(currentOriginal.all || []), ...newActivities];
          newActivities.forEach(activity => {
            if (activity.categoryName) {
              if (!currentOriginal[activity.categoryName]) {
                currentOriginal[activity.categoryName] = [];
              }
              currentOriginal[activity.categoryName].push(activity);
            }
          });

          this.setData({
            originalActivities: currentOriginal,
            activities: currentActivities,
            categories,
            page,
            hasMore
          });
        }
      })
      .catch(err => {
        if (page === 1) {
          wx.showToast({
            title: err.message || '活动加载失败',
            icon: 'none'
          });
        }
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

    const filteredActivities = {
      all: (Array.isArray(originalActivities.all) ? originalActivities.all : []).filter(item => {
        const title = item.title || '';
        return title.includes(keyword);
      })
    };

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
      url: '/user-pages/activity-detail/activity-detail?id=' + activityId
    });
  },

  onShareAppMessage: share.onShareAppMessage,
  onShareTimeline: share.onShareTimeline,

});
