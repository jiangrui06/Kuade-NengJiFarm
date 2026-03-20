const api = require('../../utils/api');

Page({
  data: {
    goods: {
      id: '',
      name: '',
      price: 0,
      image: '',
      detailImage: '',
      description: '',
      weight: '',
      storage: ''
    },
    loading: true,
    cartCount: 0
  },

  onLoad(options) {
    const goodsId = options.id;

    if (!goodsId) {
      this.setData({ loading: false });
      wx.showToast({
        title: '缺少商品ID',
        icon: 'none'
      });
      return;
    }

    this.getGoodsDetail(goodsId);
    this.updateCartCount();
  },

  onShow() {
    this.updateCartCount();
  },

  getGoodsDetail(goodsId) {
    wx.showLoading({ title: '加载中...' });

    api.request({
      url: `/api/DemoApi/goods/${goodsId}`,
      method: 'GET'
    })
      .then((data) => {
        this.setData({
          goods: {
            id: data.id || goodsId,
            name: data.name || '',
            price: Number(data.price || 0),
            image: data.image || '',
            detailImage: data.detailImage || data.image || '',
            description: data.description || '',
            weight: data.weight || '',
            storage: data.storage || ''
          },
          loading: false
        });
      })
      .catch((err) => {
        console.error('获取商品详情失败:', err);
        this.setData({ loading: false });
      })
      .finally(() => {
        wx.hideLoading();
      });
  },

  updateCartCount() {
    const cartList = wx.getStorageSync('cartList') || [];
    let totalCount = 0;
    cartList.forEach(item => {
      totalCount += item.count || 0;
    });
    this.setData({ cartCount: totalCount });
  },

  addToCart(e) {
    // 获取当前商品信息
    const goods = this.data.goods;
    
    // 获取购物车数据
    let cartList = wx.getStorageSync('cartList') || [];
    
    // 检查商品是否已在购物车中
    const existingItemIndex = cartList.findIndex(item => item.id === goods.id);
    
    if (existingItemIndex >= 0) {
      // 商品已存在，增加数量
      cartList[existingItemIndex].count = (cartList[existingItemIndex].count || 0) + 1;
    } else {
      // 商品不存在，添加到购物车
      cartList.push({
        id: goods.id,
        name: goods.name,
        price: goods.price,
        image: goods.image,
        count: 1,
        checked: true
      });
    }
    
    // 保存到缓存
    wx.setStorageSync('cartList', cartList);
    
    // 更新购物车数量
    this.updateCartCount();
    
    // 调用API更新后端数据
    this.updateCartAPI(cartList);
    
    // 显示成功提示
    wx.showToast({
      title: '已加入购物车',
      icon: 'success'
    });
  },

  updateCartAPI(cartList) {
    api.request({
      url: '/api/DemoApi/Appcart',
      method: 'POST',
      data: { cartList: cartList }
    })
    .then(res => {
      console.log('更新购物车数据成功:', res);
    })
    .catch(err => {
      console.error('更新购物车数据失败:', err);
    });
  },

  goToCart() {
    wx.switchTab({
      url: '/pages/cart/cart'
    });
  },

  buyNow() {
    wx.showToast({
      title: '购买功能开发中',
      icon: 'none'
    });
  }
});