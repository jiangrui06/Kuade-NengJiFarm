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
  },

  // 加载搜索历史
  loadSearchHistory: function() {
    const history = wx.getStorageSync('searchHistory') || [];
    this.setData({
      history: history
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
    // 限制历史记录数量为10条
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
        // 自动搜索时不保存搜索历史
        this.search(false);
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
    if (!imageUrl) return '';
    
    // 去除反引号和空格
    imageUrl = imageUrl.replace(/[`\s]/g, '');
    
    // 如果是完整的 URL，替换基础 URL
    if (imageUrl.startsWith('http://') || imageUrl.startsWith('https://')) {
      // 替换 127.0.0.1:5000 为 192.168.203.56
      return imageUrl.replace('http://127.0.0.1:5000', 'http://192.168.203.56');
    }
    
    // 如果是相对路径，添加基础 URL
    return 'http://192.168.203.56' + imageUrl;
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

    // 模拟搜索请求
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
      // 清理数据中的图片路径
      const searchResults = (data.goods || []).map(item => ({
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
      console.error('搜索失败:', err);
      this.setData({
        searching: false,
        hasResults: false
      });
      wx.showToast({
        title: '搜索失败，请重试',
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

  // 跳转到商品详情页面
  navigateToGoodsDetail: function(e) {
    const id = e.currentTarget.dataset.id;
    wx.navigateTo({
      url: '/subpkg/goods-detail/goods-detail?id=' + id
    });
  },

  // 返回按钮点击事件
  goBack: function() {
    wx.navigateBack();
  }
});
