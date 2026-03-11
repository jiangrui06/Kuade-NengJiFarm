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
    
    // 模拟后台API请求
    setTimeout(() => {
      // 模拟后台返回的数据
      const data = {
        swiperList: [
          {
            id: 1,
            image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=delicious%20roast%20chicken%20with%20vegetables&image_size=landscape_16_9'
          },
          {
            id: 2,
            image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20vegetables%20and%20fruits&image_size=landscape_16_9'
          },
          {
            id: 3,
            image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=organic%20farm%20products&image_size=landscape_16_9'
          }
        ],
        functionButtons: [
          {
            id: 1,
            name: '认购一亩田',
            color: '#4CAF50',
            path: '/pages/activity/activity'
          },
          {
            id: 2,
            name: '农场优选',
            color: '#FF9800',
            path: '/pages/index/index'
          },
          {
            id: 3,
            name: '点餐',
            color: '#F44336',
            path: '/pages/index/index'
          },
          {
            id: 4,
            name: '活动中心',
            color: '#2196F3',
            path: '/pages/activity/activity'
          }
        ],
        farmGoods: [
          {
            id: 1,
            name: '甜腻玉米500g',
            image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20sweet%20corn&image_size=square',
            price: 8.9,
            originalPrice: 9.9,
            tags: ['软糯香甜', '颗粒饱满'],
            stock: 464646
          },
          {
            id: 2,
            name: '甜腻玉米500g',
            image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20potatoes&image_size=square',
            price: 8.9,
            originalPrice: 9.9,
            tags: ['软糯香甜', '颗粒饱满'],
            stock: 464646
          },
          {
            id: 3,
            name: '甜腻玉米500g',
            image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20apples%20and%20oranges&image_size=square',
            price: 8.9,
            originalPrice: 9.9,
            tags: ['软糯香甜', '颗粒饱满'],
            stock: 464646
          },
          {
            id: 4,
            name: '甜腻玉米500g',
            image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20tomatoes&image_size=square',
            price: 8.9,
            originalPrice: 9.9,
            tags: ['软糯香甜', '颗粒饱满'],
            stock: 464646
          }
        ],
        hotDishes: [
          {
            id: 1,
            name: '剁椒鱼头',
            image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=spicy%20fish%20head%20dish&image_size=square',
            price: 8.9,
            tags: ['月销10000份']
          },
          {
            id: 2,
            name: '剁椒鱼头',
            image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=spicy%20fish%20head%20dish&image_size=square',
            price: 8.9,
            tags: ['月销10000份']
          },
          {
            id: 3,
            name: '剁椒鱼头',
            image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=spicy%20fish%20head%20dish&image_size=square',
            price: 8.9,
            tags: ['月销10000份']
          },
          {
            id: 4,
            name: '剁椒鱼头',
            image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=spicy%20fish%20head%20dish&image_size=square',
            price: 8.9,
            tags: ['月销10000份']
          }
        ]
      }
      
      // 更新数据
      this.setData({
        swiperList: data.swiperList,
        functionButtons: data.functionButtons,
        farmGoods: data.farmGoods,
        hotDishes: data.hotDishes,
        loading: false
      })
      
      wx.hideLoading()
    }, 1000)
  },

  // 搜索功能
  search: function () {
    wx.showToast({
      title: '搜索功能开发中',
      icon: 'none'
    })
  },

  // 功能按钮点击事件
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