const api = require('../../utils/api');

Page({
  data: {
    // 订单信息
    orderInfo: {
      items: [],
      totalPrice: 0,
      totalCount: 0
    },
    // 支付方式
    paymentMethods: [
      { id: 'wechat', name: '微信支付', icon: '💳' }
    ],
    // 选中的支付方式
    selectedPayment: 'wechat',
    // 加载状态
    loading: false
  },

  onLoad: function (options) {
    // 从本地存储获取购物车数据
    const cart = wx.getStorageSync('orderCart') || {};
    const cartItems = Object.values(cart);
    
    // 计算总价格和总数量
    let totalPrice = 0;
    let totalCount = 0;
    cartItems.forEach(item => {
      totalPrice += item.price * item.quantity;
      totalCount += item.quantity;
    });
    // 保留两位小数
    totalPrice = parseFloat(totalPrice.toFixed(2));
    
    // 更新订单信息
    this.setData({
      orderInfo: {
        items: cartItems,
        totalPrice: totalPrice,
        totalCount: totalCount
      }
    });
  },



  // 确认订单
  confirmOrder: function() {
    this.setData({ loading: true });
    
    // 模拟创建订单
    setTimeout(() => {
      this.setData({ loading: false });
      // 跳转到支付页面
      wx.navigateTo({
        url: '/subpkg/pay/pay?totalPrice=' + this.data.orderInfo.totalPrice
      });
    }, 500);
  },

  // 返回购物车
  goBack: function() {
    wx.navigateBack();
  }
});