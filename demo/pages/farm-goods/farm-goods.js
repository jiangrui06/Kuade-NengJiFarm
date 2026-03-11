Page({
  data: {
    showCategory: false,
    showCategoryView: false,
    currentCategory: 'all',
    // 轮播图数据
    swiperList: [
      {
        id: 1,
        image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20corn%20field%20farm&image_size=landscape_16_9'
      },
      {
        id: 2,
        image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20vegetables%20farm&image_size=landscape_16_9'
      },
      {
        id: 3,
        image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=organic%20farm%20products&image_size=landscape_16_9'
      }
    ],
    // 分类数据
    categories: [
      { id: 'all', name: '推荐', icon: '🍎', color: '#FF6B6B' },
      { id: 'new', name: '新品', icon: '🥬', color: '#4ECDC4' },
      { id: 'leaf', name: '叶菜', icon: '🍊', color: '#45B7D1' },
      { id: 'tomato', name: '番茄', icon: '🌾', color: '#96CEB4' },
      { id: 'potato', name: '土豆', icon: '🥩', color: '#FECA57' },
      { id: 'tofu', name: '豆制品', icon: '🥛', color: '#FF9FF3' },
      { id: 'mushroom', name: '营养菌菇', icon: '🐟', color: '#54A0FF' },
      { id: 'bean', name: '豆类', icon: '🍪', color: '#5F27CD' },
      { id: 'organic', name: '有机菜', icon: '🥤', color: '#00D2D3' },
      { id: 'salad', name: '净菜沙拉', icon: '📦', color: '#5352ED' },
      { id: 'duck', name: '鸭苗', icon: '📦', color: '#5352ED' },
      { id: 'poultry', name: '鸡鸭鹅', icon: '📦', color: '#5352ED' },
      { id: 'fruit', name: '瓜果', icon: '📦', color: '#5352ED' },
      { id: 'dairy', name: '乳制品', icon: '📦', color: '#5352ED' }
    ],
    // 今日优选商品数据
    todayGoods: [
      {
        id: 1,
        name: '甜腻玉米500g',
        image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20corn%20on%20the%20cob&image_size=square',
        price: 8.9,
        originalPrice: 9.9,
        stock: 464646,
        tags: ['软糯香甜', '颗粒饱满']
      },
      {
        id: 2,
        name: '新鲜番茄500g',
        image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20tomatoes&image_size=square',
        price: 5.9,
        originalPrice: 6.9,
        stock: 1800,
        tags: ['当季', '新鲜']
      },
      {
        id: 3,
        name: '有机胡萝卜500g',
        image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=organic%20carrots&image_size=square',
        price: 7.9,
        originalPrice: 8.9,
        stock: 600,
        tags: ['有机认证', '无农药']
      },
      {
        id: 4,
        name: '土豆500g',
        image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20potatoes&image_size=square',
        price: 2.9,
        originalPrice: 3.9,
        stock: 3000,
        tags: ['淀粉含量高', '软糯']
      }
    ],
    // 热门商品数据
    hotGoods: [
      {
        id: 5,
        name: '鸡蛋800g',
        image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20eggs&image_size=square',
        price: 9.9,
        originalPrice: 10.9,
        stock: 1500,
        tags: ['新鲜', '营养']
      },
      {
        id: 6,
        name: '嫩豆腐400g',
        image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=tofu&image_size=square',
        price: 3.5,
        originalPrice: 4.5,
        stock: 800,
        tags: ['嫩', '新鲜']
      },
      {
        id: 7,
        name: '香菇200g',
        image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20shiitake%20mushrooms&image_size=square',
        price: 8.9,
        originalPrice: 9.9,
        stock: 500,
        tags: ['鲜香', '营养']
      },
      {
        id: 8,
        name: '苹果500g',
        image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20apples&image_size=square',
        price: 7.9,
        originalPrice: 8.9,
        stock: 1500,
        tags: ['脆甜', '多汁']
      }
    ],
    // 分类商品数据
    categoryGoods: {
      'all': [
        { id: 1, name: '白糯玉米 800g', image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20corn%20on%20the%20cob&image_size=square', price: 9.4, tags: ['农场直供', '新鲜采摘'] },
        { id: 2, name: '白糯玉米 800g', image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20corn%20on%20the%20cob&image_size=square', price: 9.4, tags: ['农场直供', '新鲜采摘'] },
        { id: 3, name: '鸡蛋 800g', image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20eggs&image_size=square', price: 9.4, tags: ['农场直供', '散养'] },
        { id: 4, name: '白糯玉米 800g', image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20corn%20on%20the%20cob&image_size=square', price: 9.4, tags: ['农场直供', '新鲜采摘'] },
        { id: 5, name: '白糯玉米 800g', image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20corn%20on%20the%20cob&image_size=square', price: 9.4, tags: ['农场直供', '新鲜采摘'] },
        { id: 6, name: '白糯玉米 800g', image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20corn%20on%20the%20cob&image_size=square', price: 9.4, tags: ['农场直供', '新鲜采摘'] }
      ],
      'new': [
        { id: 7, name: '新品玉米 800g', image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20corn%20on%20the%20cob&image_size=square', price: 10.4, tags: ['农场直供', '新品上市'] }
      ],
      'leaf': [
        { id: 8, name: '新鲜菠菜 500g', image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20spinach&image_size=square', price: 6.4, tags: ['农场直供', '新鲜采摘'] }
      ],
      'tomato': [
        { id: 9, name: '番茄 500g', image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20tomatoes&image_size=square', price: 5.9, tags: ['农场直供', '当季新鲜'] }
      ],
      'potato': [
        { id: 10, name: '土豆 500g', image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20potatoes&image_size=square', price: 2.9, tags: ['农场直供', '新鲜'] }
      ],
      'tofu': [
        { id: 11, name: '嫩豆腐 400g', image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=tofu&image_size=square', price: 3.5, tags: ['农场直供', '新鲜制作'] }
      ],
      'mushroom': [
        { id: 12, name: '香菇 200g', image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20shiitake%20mushrooms&image_size=square', price: 8.9, tags: ['农场直供', '新鲜'] }
      ],
      'bean': [
        { id: 13, name: '黄豆 500g', image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20soybeans&image_size=square', price: 4.9, tags: ['农场直供', '优质'] }
      ],
      'organic': [
        { id: 14, name: '有机菜 500g', image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=organic%20vegetables&image_size=square', price: 12.9, tags: ['农场直供', '有机认证'] }
      ],
      'salad': [
        { id: 15, name: '净菜沙拉 300g', image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20salad&image_size=square', price: 15.9, tags: ['农场直供', '新鲜'] }
      ],
      'duck': [
        { id: 16, name: '鸭苗', image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=duck%20lings&image_size=square', price: 19.9, tags: ['农场直供', '鲜活'] }
      ],
      'poultry': [
        { id: 17, name: '土鸡', image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=free%20range%20chicken&image_size=square', price: 59.9, tags: ['农场直供', '散养'] }
      ],
      'fruit': [
        { id: 18, name: '苹果 500g', image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20apples&image_size=square', price: 7.9, tags: ['农场直供', '新鲜'] }
      ],
      'dairy': [
        { id: 19, name: '牛奶 250ml', image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20milk&image_size=square', price: 3.5, tags: ['农场直供', '新鲜'] }
      ]
    },
    currentCategoryGoods: []
  },
  
  onLoad: function() {
    console.log('农场优选页面加载');
    // 初始化加载全部商品
    this.setData({
      currentCategoryGoods: this.data.categoryGoods['all']
    });
  },
  
  // 搜索功能
  search: function() {
    wx.showToast({
      title: '搜索功能开发中',
      icon: 'none'
    });
  },
  
  // 切换分类界面
  toggleCategory: function() {
    this.setData({
      showCategoryView: !this.data.showCategoryView
    });
  },
  
  // 选择分类
  selectCategory: function(e) {
    const categoryId = e.currentTarget.dataset.id;
    this.setData({
      currentCategory: categoryId,
      currentCategoryGoods: this.data.categoryGoods[categoryId]
    });
  },
  
  // 获取当前分类名称
  getCurrentCategoryName: function() {
    const category = this.data.categories.find(item => item.id === this.data.currentCategory);
    return category ? category.name : '商品分类';
  },
  
  // 查看更多
  viewMore: function() {
    wx.showToast({
      title: '查看更多功能开发中',
      icon: 'none'
    });
  },
  
  // 查看商品详情
  viewGoodsDetail: function(e) {
    const goodsId = e.currentTarget.dataset.id;
    wx.navigateTo({
      url: '/pages/goods-detail/goods-detail?id=' + goodsId
    });
  }
});