const share = require('../../utils/share');
Page({
  data: {
    keyword: '',
    searching: false,
    hasResults: false,
    searchResults: [],
    history: []
  },
  
  // 防抖计时器
  searchTimer: null,

  onLoad: function(options) {
    // 加载搜索历史
    this.loadSearchHistory();
    
    // 如果 URL 中有 keyword 参数，自动搜索
    if (options && options.keyword) {
      const keyword = decodeURIComponent(options.keyword);
      this.setData({ keyword: keyword });
      if (keyword.trim()) {
        this.search(false); // false 表示不保存到历史（因为是从首页跳转过来的）
      }
    }
  },

  // 加载搜索历史
  loadSearchHistory: function() {
    const history = wx.getStorageSync('searchHistory') || [];
    // 只显示最近的6条历史记录
    this.setData({
      history: history.slice(0, 6)
    });
  },

  // 保存搜索历史
  saveSearchHistory: function(keyword) {
    if (!keyword || keyword.trim() === '') return;
    
    let history = wx.getStorageSync('searchHistory') || [];
    // 移除重复的关键词
    history = history.filter(item => item !== keyword);
    // 添加到历史记录开头
    history.unshift(keyword);
    // 限制历史记录数量为10个
    if (history.length > 10) {
      history = history.slice(0, 10);
    }
    // 保存到本地存储
    wx.setStorageSync('searchHistory', history);
    this.setData({
      history: history
    });
  },

  // 清除搜索历史
  clearHistory: function() {
    wx.removeStorageSync('searchHistory');
    this.setData({
      history: []
    });
  },

  // 输入框输入事件
  onInputChange: function(e) {
    const keyword = e.detail.value;
    this.setData({
      keyword: keyword
    });
    
    // 清除之前的计时器
    if (this.searchTimer) {
      clearTimeout(this.searchTimer);
    }
    
    // 防抖处理，300毫秒后执行搜索
    this.searchTimer = setTimeout(() => {
      if (keyword.trim()) {
        this.search(true);
      } else {
        // 输入框为空时，清空搜索结果
        this.setData({
          hasResults: false,
          searchResults: []
        });
      }
    }, 300);
  },

  // 处理图片路径，确保使用正确的基础 URL
  processImageUrl: function (imageUrl) {
    const utils = require('../../utils/utils');
    return utils.media.processUrl(imageUrl);
  },

  // 搜索函数
  search: function(saveHistory = true) {
    const keyword = this.data.keyword.trim();
    if (!keyword) {
      wx.showToast({
        title: '请输入搜索关键词',
        icon: 'none'
      });
      return;
    }

    // 保存搜索历史（只有当saveHistory为true时）
    if (saveHistory) {
      this.saveSearchHistory(keyword);
    }

    // 显示搜索中状态
    this.setData({
      searching: true,
      hasResults: false
    });

    // 使用首页全局搜索接口，同时搜索商品、菜品、活动、认购
    const api = require('../../utils/api');
    api.request({
      url: '/api/home/search',
      method: 'GET',
      data: {
        keyword: keyword,
        page: 1,
        pageSize: 20
      }
    })
    .then(data => {
      // 处理搜索结果，兼容多个字段名
      const items = data.items || data.list || [];
      
      // 清理数据中的图片路径
      const searchResults = items.map(item => ({
        ...item,
        image: this.processImageUrl(item.image),
        price: item.price ? item.price.toString().replace(/[¥￥]/g, '') : item.price,
        originalPrice: item.originalPrice ? item.originalPrice.toString().replace(/[¥￥]/g, '') : item.originalPrice
      }));

      this.setData({
        searching: false,
        hasResults: searchResults.length > 0,
        searchResults: searchResults
      });
    })
    .catch(err => {
      // 降级处理：全局搜索失败时，使用商品搜索接口
      this.searchWithFallback(keyword);
    });
  },

  // 降级搜索：使用商品搜索接口
  searchWithFallback: function(keyword) {
    const api = require('../../utils/api');
    api.request({
      url: '/api/goods/search',
      method: 'GET',
      data: {
        keyword: keyword,
        page: 1,
        pageSize: 20
      }
    })
    .then(data => {
      // 处理搜索结果，兼容多个字段名
      const items = data.goods || data.goodsList || data.items || [];
      
      // 清理数据中的图片路径，添加类型标识
      const searchResults = items.map(item => ({
        ...item,
        type: 'goods',
        typeName: '农场优选',
        image: this.processImageUrl(item.image),
        price: item.price ? item.price.toString().replace(/[¥￥]/g, '') : item.price,
        originalPrice: item.originalPrice ? item.originalPrice.toString().replace(/[¥￥]/g, '') : item.originalPrice
      }));

      this.setData({
        searching: false,
        hasResults: searchResults.length > 0,
        searchResults: searchResults
      });
    })
    .catch(err => {
      this.setData({
        searching: false,
        hasResults: false
      });
      wx.showToast({
        title: '搜索失败，请稍后重试',
        icon: 'none'
      });
    });
  },

  // 使用历史记录搜索
  searchWithHistory: function(e) {
    const keyword = e.currentTarget.dataset.keyword;
    this.setData({
      keyword: keyword
    });
    this.search(true);
  },

  // 跳转到详情页（根据类型跳转）
  navigateToDetail: function(e) {
    const id = e.currentTarget.dataset.id;
    const type = e.currentTarget.dataset.type;
    
    let url = '';
    switch (type) {
      case 'goods':
        url = '/user-pages/goods-detail/goods-detail?id=' + id;
        break;
      case 'dish':
        url = '/user-pages/order-foods-detail/order-foods-detail?id=' + id;
        break;
      case 'activity':
        url = '/user-pages/activity-detail/activity-detail?id=' + id;
        break;
      case 'acre':
        url = '/user-pages/acre-detail/acre-detail?id=' + id;
        break;
      default:
        url = '/user-pages/goods-detail/goods-detail?id=' + id;
    }
    
    wx.navigateTo({ url: url });
  },

  // 返回按钮点击事件
  goBack: function() {
    wx.navigateBack();
  },

  onShareAppMessage: share.onShareAppMessage,
  onShareTimeline: share.onShareTimeline,

});

