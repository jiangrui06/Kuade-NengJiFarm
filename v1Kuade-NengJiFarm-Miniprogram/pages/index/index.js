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
    cartCount: 0,

    // 员工标识
    isStaff: false,
    // 搜索关键词
    searchKeyword: ''
  },

  onLoad: function () {
    console.log('首页加载')
    
    // 清理旧的购物车数据，确保格式正确
    try {
      const rawCartList = wx.getStorageSync('cartList');
      if (!Array.isArray(rawCartList)) {
        console.log('清理旧购物车数据')
        wx.removeStorageSync('cartList');
      }
    } catch (e) {}
    
    // 调用后台API获取数据
    this.getHomeData()
  },

  // 处理图片路径，确保使用正确的基础 URL
  processImageUrl: function (imageUrl) {
    const utils = require('../../utils/utils');
    return utils.media.processUrl(imageUrl);
  },

  // 获取首页数据
  getHomeData: function () {
    wx.showLoading({ title: '加载中...' })
    
    const api = require('../../utils/api')
    api.get('/api/home', { page: 1, pageSize: 4 })
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
      wx.showToast({ title: '加载失败，请重试', icon: 'none' })
      this.setData({ loading: false })
    })

    const BASE_URL = 'http://192.168.203.56';
    const videos = [{
      id: 1,
      title: '农场航拍',
      description: '农场视频',
      coverImage: '',  // 暂时不用封面图
      videoUrl: BASE_URL + '/api/file/video/farm_intro.mp4'
    }];
    
    this.setData({
      videos: videos
    });
  },

  

  // 搜索输入事件
  onSearchInput: function(e) {
    console.log('搜索输入:', e.detail.value);
    this.setData({
      searchKeyword: e.detail.value
    });
  },

  // 阻止 input 点击事件冒泡（防止重复跳转）
  stopPropagation: function(e) {
    // 空函数，仅阻止事件冒泡
  },

  // 搜索确认事件（回车）
  onSearchConfirm: function() {
    console.log('搜索确认（回车）');
    this.navigateToSearch();
  },

  // 搜索按钮点击
  onSearchClick: function() {
    console.log('搜索按钮点击');
    this.navigateToSearch();
  },

  // 跳转到搜索页面
  navigateToSearch: function () {
    const keyword = this.data.searchKeyword.trim();
    console.log('跳转到搜索页面，关键词:', keyword);
    wx.navigateTo({
      url: '/user-pages/search/search?keyword=' + encodeURIComponent(keyword),
      success: function() {
        console.log('页面跳转成功');
      },
      fail: function(err) {
        console.error('页面跳转失败:', err);
        wx.showToast({
          title: '跳转失败，请重试',
          icon: 'none'
        });
      }
    });
  },

  // 跳转到认购一亩田列表页面
  navigateToAcre: function() {
    wx.navigateTo({
      url: '/user-pages/acre/acre'
    });
  },

  // 跳转到认购一亩田详情页面
  navigateToAcreDetail: function(e) {
    const id = e.currentTarget.dataset.id;
    wx.navigateTo({
      url: '/user-pages/acre-detail/acre-detail?id=' + id
    });
  },

  // 跳转到农场优选页面
  navigateToFarmGoods: function() {
    wx.navigateTo({
      url: '/user-pages/farm-goods/farm-goods'
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
      url: '/user-pages/order/order'
    });
  },

  // 跳转到商品详情页面
  navigateToGoodsDetail: function(e) {
    const id = e.currentTarget.dataset.id;
    wx.navigateTo({
      url: '/user-pages/goods-detail/goods-detail?id=' + id
    });
  },

  // 跳转到菜品详情页面
  navigateToOrderFoodsDetail: function(e) {
    const id = e.currentTarget.dataset.id;
    wx.navigateTo({
      url: '/user-pages/order-foods-detail/order-foods-detail?id=' + id
    });
  },

  // 跳转到农场介绍页面
  navigateToFarmIntro: function() {
    wx.navigateTo({
      url: '/user-pages/farm-intro/farm-intro'
    });
  },

  // 视频全屏状态变化处理
  onFullscreenChange: function(e) {
    const fullScreen = e.detail.fullScreen;
    console.log('视频全屏状态变化', fullScreen);
    // 可以在这里添加全屏状态变化的逻辑
  },

  // 添加商品到购物车
  addToCart(e) {
    const { id, name, price, image, type, stock } = e.currentTarget.dataset;
    
    // 获取当前购物车列表
    const cartList = wx.getStorageSync('cartList') || [];
    
    // 查找是否已存在该商品
    const existingIndex = cartList.findIndex(item => String(item.id) === String(id));
    
    if (existingIndex >= 0) {
      // 如果已存在，增加数量
      const newQuantity = (cartList[existingIndex].count || cartList[existingIndex].quantity || 0) + 1;
      if (stock && newQuantity > stock) {
        wx.showToast({ title: '库存不足', icon: 'none' });
        return;
      }
      cartList[existingIndex].count = newQuantity;
      cartList[existingIndex].quantity = newQuantity;
    } else {
      // 如果不存在，添加新商品
      cartList.push({
        id: String(id),
        name: name,
        price: Number((price || 0).toString().replace(/[¥￥]/g, '')),
        image: image,
        count: 1,
        quantity: 1,
        type: type || 'goods', // goods: 商品, food: 点餐
        checked: true,
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
    try {
      const rawCartList = wx.getStorageSync('cartList');
      let cartList = [];
      
      // 强制确保是数组
      if (Array.isArray(rawCartList)) {
        cartList = rawCartList;
      } else if (rawCartList && typeof rawCartList === 'object') {
        cartList = Object.values(rawCartList);
      } else {
        cartList = [];
      }
      
      // 再次验证
      if (!Array.isArray(cartList)) {
        cartList = [];
      }
      
      let totalCount = 0;
      for (let i = 0; i < cartList.length; i++) {
        const item = cartList[i];
        if (item) {
          totalCount += (item.count || item.quantity || 0);
        }
      }
      
      this.setData({ cartCount: totalCount });
    } catch (error) {
      console.log('更新购物车计数出错:', error);
      this.setData({ cartCount: 0 });
    }
  },

  // 页面显示时更新购物车计数 + 初始化自定义 tabBar
  onShow() {
    this.updateCartCount();
    // 检查员工角色
    const role = wx.getStorageSync('user_role');
    this.setData({ isStaff: role === 'staff' });
    if (typeof this.getTabBar === 'function' && this.getTabBar()) {
      this.getTabBar().init();
    }
  },

  // 跳转员工核销页面
  goToStaffVerify() {
    wx.navigateTo({ url: '/staff-pages/staff-verify/staff-verify' });
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
