Page({
  data: {
    // 轮播图数据
    swiperList: [],
    // 农场优选商品数据
    farmGoods: [],
    // 热销菜品数据
    hotDishes: [],
    // 认购一亩田项目数据
    acreProjects: [],

    videos:[],
    // 加载状态
    loading: true,
    
    // 购物车数据
    cart: {},
    cartCount: 0
  },

  onLoad: function () {
    console.log('首页加载')
    // 调用后台API获取数据
    this.getHomeData()
  },

  // 处理图片路径，确保使用正确的基础 URL
  processImageUrl: function (imageUrl) {
    if (!imageUrl) return '';
    
    // 去除反引号和空格
    imageUrl = imageUrl.replace(/[`\s]/g, '');
    
    // 如果是完整的 URL，替换基础 URL
    if (imageUrl.startsWith('http://') || imageUrl.startsWith('https://')) {
      // 替换 127.0.0.1:5000 为 192.168.203.56
      return imageUrl.replace('http://127.0.0.1:5000', 'http://192.168.101.47');
    }
    
    // 如果是相对路径，添加基础 URL
    return 'http://192.168.101.47' + imageUrl;
  },

  // 获取首页数据
  getHomeData: function () {
    wx.showLoading({ title: '加载中...' })
    
    const api = require('../../utils/api')
    api.request({ 
      url: '/api/home', 
      method: 'GET',
      data: {
        page: 1,
        pageSize: 4
      }
    })
    .then(data => {
      // 清理数据中的图片路径
      const cleanData = {
        swiperList: (data.swiperList || []).map(item => ({
          ...item,
          image: this.processImageUrl(item.image)
        })),
        farmGoods: (data.farmGoods || []).map(item => ({
          ...item,
          image: this.processImageUrl(item.image),
          price: typeof item.price === 'string' ? item.price.replace(/[¥￥]/g, '') : item.price,
          originalPrice: typeof item.originalPrice === 'string' ? item.originalPrice.replace(/[¥￥]/g, '') : item.originalPrice
        })),
        hotDishes: (data.hotDishes || []).map(item => ({
          ...item,
          image: this.processImageUrl(item.image),
          price: typeof item.price === 'string' ? item.price.replace(/[¥￥]/g, '') : item.price
        })),
        acreProjects: (data.acreProjects || []).map(item => ({
          ...item,
          image: this.processImageUrl(item.image),
          price: typeof item.price === 'string' ? item.price.replace(/[¥￥]/g, '') : item.price
        }))
      }
      
      this.setData({
        swiperList: cleanData.swiperList,
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


    const BASE_URL = 'http://192.168.101.47';
    const videos = [{
      id: 1,
      title: '农场航拍',
      description: '农场视频',
      coverImage: BASE_URL + '/api/file/image/farm_0000000000001.jpg',
      videoUrl: BASE_URL + '/api/file/video/farm_intro.mp4'
    }];
    
    this.setData({
      videos: videos
    });
      
  },

  

  // 跳转到搜索页面
  navigateToSearch: function () {
    wx.navigateTo({
      url: '/subpkg/search/search'
    });
  },

  // 搜索功能
  search: function () {
    wx.showToast({
      title: '搜索功能开发中',
      icon: 'none'
    })
  },

  // 跳转到认购一亩田列表页面
  navigateToAcre: function() {
    wx.navigateTo({
      url: '/subpkg/acre/acre'
    });
  },

  // 跳转到认购一亩田详情页面
  navigateToAcreDetail: function(e) {
    const id = e.currentTarget.dataset.id;
    wx.navigateTo({
      url: '/subpkg/acre-detail/acre-detail?id=' + id
    });
  },

  // 跳转到农场优选页面
  navigateToFarmGoods: function() {
    wx.navigateTo({
      url: '/subpkg/farm-goods/farm-goods'
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
      url: '/subpkg/order/order'
    });
  },

  // 跳转到商品详情页面
  navigateToGoodsDetail: function(e) {
    const id = e.currentTarget.dataset.id;
    wx.navigateTo({
      url: '/subpkg/goods-detail/goods-detail?id=' + id
    });
  },

  // 跳转到菜品详情页面
  navigateToOrderFoodsDetail: function(e) {
    const id = e.currentTarget.dataset.id;
    wx.navigateTo({
      url: '/subpkg/order-foods-detail/order-foods-detail?id=' + id
    });
  },

  // 跳转到农场介绍页面
  navigateToFarmIntro: function() {
    wx.navigateTo({
      url: '/subpkg/farm-intro/farm-intro'
    });
  },

  // 视频全屏状态变化处理
  onFullscreenChange: function(e) {
    const fullScreen = e.detail.fullScreen;
    console.log('视频全屏状态变化:', fullScreen);
    // 可以在这里添加全屏状态变化的逻辑
  },

  // 添加商品到购物车
  addToCart(e) {
    const { id, name, price, image, type, stock } = e.currentTarget.dataset;
    
    // 获取当前购物车数据
    const cartList = wx.getStorageSync('cartList') || [];
    
    // 查找是否已存在该商品
    const existingIndex = cartList.findIndex(item => String(item.id) === String(id));
    
    if (existingIndex >= 0) {
      // 如果已存在，增加数量
      const newQuantity = cartList[existingIndex].count + 1;
      if (stock && newQuantity > stock) {
        wx.showToast({ title: '库存不足', icon: 'none' });
        return;
      }
      cartList[existingIndex].count = newQuantity;
    } else {
      // 如果不存在，添加新商品
      cartList.push({
        id: String(id),
        name: name,
        price: Number((price || 0).toString().replace(/[¥￥]/g, '')),
        image: image,
        count: 1,
        type: type || 'goods', // goods: 商品, food: 点餐
        checked: false,
        stock: stock || 999
      });
    }
    
    // 保存到本地存储
    wx.setStorageSync('cartList', cartList);
    
    // 更新购物车计数
    this.updateCartCount();
    
    // 显示添加成功提示
    wx.showToast({ title: '已添加到购物车', icon: 'success' });
  },

  // 更新购物车计数
  updateCartCount() {
    const cartList = wx.getStorageSync('cartList') || [];
    const totalCount = cartList.reduce((sum, item) => sum + (item.count || 0), 0);
    this.setData({ cartCount: totalCount });
  },

  // 页面显示时更新购物车计数
  onShow() {
    this.updateCartCount();
  },

  // 下拉刷新
  onPullDownRefresh() {
    console.log('下拉刷新首页');
    this.getHomeData();
    // 刷新完成后停止下拉刷新
    setTimeout(() => {
      wx.stopPullDownRefresh();
    }, 1000);
  }
})
