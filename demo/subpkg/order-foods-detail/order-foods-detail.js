const api = require('../../utils/api');

Page({
  data: {
    goods: {},
    loading: true,
    cart: {},
    cartCount: 0,
    totalPrice: 0
  },

  onLoad(options) {
    const { id } = options;
    if (id) {
      this.getGoodsDetail(id);
    }
    // 恢复购物车
    this.restoreCart();
  },

  onShow() {
    // 页面显示时更新购物车数据
    this.restoreCart();
  },

  getGoodsDetail(id) {
    wx.showLoading({ title: '加载中...' });
    // 模拟获取商品详情数据
    setTimeout(() => {
      const mockGoods = {
        id: id,
        name: '农家番茄',
        price: 9.9,
        image: '../../images/NengJi1.jpg',
        desc: '500g/袋，新鲜番茄',
        weight: '500g',
        storage: '冷藏',
        sold: 123,
        stock: 50
      };
      this.setData({
        goods: mockGoods,
        loading: false
      });
      wx.hideLoading();
    }, 500);
  },



  addToCart() {
    const goods = this.data.goods;
    if (!goods.id) return;
    
    // 检查是否选择了桌台
    const tableNumber = wx.getStorageSync('tableNumber');
    if (!tableNumber) {
      wx.showToast({ title: '请选择桌台号码', icon: 'none' });
      return;
    }

    const key = String(goods.id);
    const newCart = { ...this.data.cart };

    if (newCart[key]) {
      if (newCart[key].quantity >= goods.stock) {
        wx.showToast({ title: '库存不足', icon: 'none' });
        return;
      }
      newCart[key].quantity += 1;
    } else {
      newCart[key] = { ...goods, quantity: 1 };
    }
    this.syncCartState(newCart);
    wx.showToast({ title: '已加入购物车', icon: 'success' });
  },

  buyNow() {
    const goods = this.data.goods;
    if (!goods.id) return;
    
    // 检查是否选择了桌台
    const tableNumber = wx.getStorageSync('tableNumber');
    if (!tableNumber) {
      wx.showToast({ title: '请选择桌台号码', icon: 'none' });
      return;
    }

    // 将当前商品加入购物车
    const key = String(goods.id);
    const newCart = { ...this.data.cart };
    newCart[key] = { ...goods, quantity: 1 };
    this.syncCartState(newCart);
    
    // 跳转到确认订单页面
    wx.navigateTo({ url: '/subpkg/confirm-order/confirm-order' });
  },

  viewCart() {
    // 点击购物车图标返回点餐页面
    wx.navigateBack();
  },



  syncCartState(newCart) {
    let count = 0, total = 0;
    Object.values(newCart).forEach(item => {
      count += item.quantity;
      total += item.price * item.quantity;
    });
    this.setData({
      cart: newCart,
      cartCount: count,
      totalPrice: parseFloat(total.toFixed(2))
    });
    try { wx.setStorageSync('orderCart', newCart) } catch (e) {}
  },

  restoreCart() {
    try {
      const cart = wx.getStorageSync('orderCart') || {};
      let count = 0, total = 0;
      Object.values(cart || {}).forEach(item => {
        count += item.quantity || 0;
        total += (item.price || 0) * (item.quantity || 0);
      });
      this.setData({
        cart: cart || {},
        cartCount: count,
        totalPrice: parseFloat(total.toFixed(2))
      });
    } catch (e) {}
  },


});