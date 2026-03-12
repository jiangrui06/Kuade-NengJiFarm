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
    loading: true
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
      method: 'GET' 
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
        loading: false
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
  }
})