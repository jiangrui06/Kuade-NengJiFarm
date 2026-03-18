Page({
  data: {
    // 轮播图数据
    swiperList: [],
    // 功能按钮数据
    functionButtons: [],
    // 农场优选商品数据
    farmGoods: [],
    // 热销菜品数据
    hotDishes: [],
    // 认购一亩田项目数据
    acreProjects: [],
    // 加载状态
    loading: true,
    // 加载更多状态
    loadingMore: false,
    // 分页参数
    page: 1,
    pageSize: 10,
    // 是否还有更多数据
    hasMore: true
  },

  onLoad: function () {
    console.log('首页加载')
    // 调用后台API获取数据
    this.getHomeData()
  },

  // 获取首页数据
  getHomeData: function () {
    wx.showLoading({ title: '加载中...' })
    
    const api = require('../../utils/api')
    api.request({ 
      url: '/api/DemoApi/home', 
      method: 'GET',
      data: {
        page: 1,
        pageSize: this.data.pageSize
      }
    })
    .then(data => {
      // 清理数据中的图片路径（去除反引号和空格）
      const cleanData = {
        swiperList: (data.swiperList || []).map(item => ({
          ...item,
          image: item.image ? item.image.replace(/[`\s]/g, '') : ''
        })),
        functionButtons: data.functionButtons || [],
        farmGoods: (data.farmGoods || []).map(item => ({
          ...item,
          image: item.image ? item.image.replace(/[`\s]/g, '') : ''
        })),
        hotDishes: (data.hotDishes || []).map(item => ({
          ...item,
          image: item.image ? item.image.replace(/[`\s]/g, '') : ''
        })),
        acreProjects: (data.acreProjects || []).map(item => ({
          ...item,
          image: item.image ? item.image.replace(/[`\s]/g, '') : ''
        }))
      }
      
      this.setData({
        swiperList: cleanData.swiperList,
        functionButtons: cleanData.functionButtons,
        farmGoods: cleanData.farmGoods,
        hotDishes: cleanData.hotDishes,
        acreProjects: cleanData.acreProjects,
        loading: false,
        page: 1,
        hasMore: true
      })
      wx.hideLoading()
    })
    .catch(err => {
      console.error('获取首页数据失败:', err)
      wx.hideLoading()
      wx.showToast({ 
        title: '加载失败，请重试', 
        icon: 'none' 
      })
      this.setData({ loading: false })
    })
  },

  // 搜索功能
  search: function () {
    wx.showToast({
      title: '搜索功能开发中',
      icon: 'none'
    })
  },

  // 功能按钮点击事件
  onFunctionBtnClick: function (e) {
    const item = e.currentTarget.dataset.item
    if (item && item.path) {
      if (item.path.indexOf('/pages/index/index') !== -1 || item.path.indexOf('/pages/activity/activity') !== -1) {
        wx.switchTab({ url: item.path })
      } else {
        wx.navigateTo({ url: item.path })
      }
    }
  },

  functionBtnClick: function (e) {
    const id = e.currentTarget.dataset.id
    wx.showToast({
      title: `点击了${this.data.functionButtons[id - 1].name}`,
      icon: 'none'
    })
  },

  // 商品点击事件
  goodsClick: function (e) {
    const id = e.currentTarget.dataset.id
    wx.showToast({
      title: '商品详情页开发中',
      icon: 'none'
    })
  },

  // 查看更多点击事件
  viewMore: function (e) {
    const type = e.currentTarget.dataset.type
    wx.showToast({
      title: `查看更多${type}`,
      icon: 'none'
    })
  },

  // 跳转到认购一亩田列表页面
  navigateToAcre: function() {
    wx.navigateTo({
      url: '/pages/acre/acre'
    });
  },

  // 跳转到认购一亩田详情页面
  navigateToAcreDetail: function(e) {
    const id = e.currentTarget.dataset.id;
    wx.navigateTo({
      url: '/pages/acre-detail/acre-detail?id=' + id
    });
  },

  // 跳转到农场优选页面
  navigateToFarmGoods: function() {
    wx.navigateTo({
      url: '/pages/farm-goods/farm-goods'
    });
  },
  
  // 跳转到活动页面
  navigateToActivity: function() {
    wx.switchTab({
      url: '/pages/activity/activity'
    });
  },

  // 跳转到点餐页面
  navigateToOrder: function() {
    wx.navigateTo({
      url: '/pages/order/order'
    });
  },

  // 跳转到商品详情页面
  navigateToGoodsDetail: function(e) {
    const id = e.currentTarget.dataset.id;
    wx.navigateTo({
      url: '/pages/goods-detail/goods-detail?id=' + id
    });
  },

  // 滚动到底部加载更多
  onReachBottom: function() {
    console.log('首页触发 onReachBottom');
    if (!this.data.loadingMore && this.data.hasMore) {
      console.log('开始加载更多数据');
      this.loadMoreData();
    } else {
      console.log('跳过加载:', { loadingMore: this.data.loadingMore, hasMore: this.data.hasMore });
    }
  },

  // 加载更多数据
  loadMoreData: function() {
    if (!this.data.hasMore) return;

    this.setData({ loadingMore: true });
    console.log('设置加载状态为 true');

    const api = require('../../utils/api');
    const nextPage = this.data.page + 1;
    console.log('请求第', nextPage, '页数据');
    
    api.request({ 
      url: '/api/DemoApi/home', 
      method: 'GET',
      data: {
        page: nextPage,
        pageSize: this.data.pageSize
      }
    })
    .then(data => {
      console.log('API 返回数据:', data);
      
      // 检查数据是否有效
      if (!data) {
        console.error('API 返回数据为空');
        this.setData({ loadingMore: false });
        return;
      }
      
      // 清理数据中的图片路径
      const cleanData = {
        farmGoods: (data.farmGoods || []).map(item => ({
          ...item,
          image: item.image ? item.image.replace(/[`\s]/g, '') : ''
        })),
        hotDishes: (data.hotDishes || []).map(item => ({
          ...item,
          image: item.image ? item.image.replace(/[`\s]/g, '') : ''
        }))
      };
      
      console.log('清理后的数据:', cleanData);
      
      // 合并数据
      const newFarmGoods = [...this.data.farmGoods, ...cleanData.farmGoods];
      const newHotDishes = [...this.data.hotDishes, ...cleanData.hotDishes];
      
      console.log('合并后的数据长度:', { 
        farmGoods: { old: this.data.farmGoods.length, new: newFarmGoods.length, added: cleanData.farmGoods.length }, 
        hotDishes: { old: this.data.hotDishes.length, new: newHotDishes.length, added: cleanData.hotDishes.length }
      });
      
      // 检查是否还有更多数据
      const hasMore = cleanData.farmGoods.length > 0 || cleanData.hotDishes.length > 0;
      console.log('是否还有更多数据:', hasMore);
      
      this.setData({
        farmGoods: newFarmGoods,
        hotDishes: newHotDishes,
        page: nextPage,
        loadingMore: false,
        hasMore: hasMore
      });
      console.log('更新页面数据成功');
    })
    .catch(err => {
      console.error('加载更多数据失败:', err);
      this.setData({ loadingMore: false });
      console.log('加载失败，设置加载状态为 false');
    });
  }
})