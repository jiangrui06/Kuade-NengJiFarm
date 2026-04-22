const { request, api } = require('../../utils/api');

Page({
  data: {
    orderInfo: {
      items: [],
      totalPrice: 0,
      totalCount: 0
    },
    paymentMethods: [
      { id: 'wechat', name: '微信支付', icon: '💳' }
    ],
    selectedPayment: 'wechat',
    loading: false,
    isCreatingOrder: false
  },

  onLoad: function () {
    const cart = wx.getStorageSync('orderCart') || {};
    const cartItems = Object.values(cart);

    let totalPrice = 0;
    let totalCount = 0;
    cartItems.forEach(item => {
      const price = Number((item.price || 0).toString().replace(/[¥￥]/g, ''));
      totalPrice += price * Number(item.quantity || 0);
      totalCount += Number(item.quantity || 0);
    });
    totalPrice = Number(totalPrice.toFixed(2));

    const tableNumber = wx.getStorageSync('tableNumber');

    this.setData({
      orderInfo: {
        items: cartItems,
        totalPrice,
        totalCount
      },
      tableNumber: tableNumber || '未选择'
    });
  },

  selectPayment: function (e) {
    const paymentId = e.currentTarget.dataset.id;
    if (!paymentId) return;
    this.setData({ selectedPayment: paymentId });
  },

  // 确认订单
  confirmOrder: function () {
    if (this.data.isCreatingOrder) return;

    const items = this.data.orderInfo.items || [];
    if (!items.length) {
      wx.showToast({ title: '购物车为空', icon: 'none' });
      return;
    }

    const totalPrice = Number(this.data.orderInfo.totalPrice || 0);
    if (totalPrice <= 0) {
      wx.showToast({ title: '金额异常', icon: 'none' });
      return;
    }

    this.setData({ loading: true, isCreatingOrder: true });

    // 直接创建订单，允许用户有多个待支付订单
    this.createOrder();
  },



  // 创建订单
  createOrder: function () {
    const items = this.data.orderInfo.items || [];
    const totalPrice = Number(this.data.orderInfo.totalPrice || 0);
    const tableNumber = Number(wx.getStorageSync('tableNumber') || 0);

    const payload = {
      sourceType: 'food',
      sourceName: '点餐',
      quantity: this.data.orderInfo.totalCount || 1,
      tableNumber: tableNumber > 0 ? tableNumber : 0,
      totalPrice,
      items: items.map(item => ({
        id: String(item.id || ''),
        name: item.name || '餐品',
        price: Number((item.price || 0).toString().replace(/[¥￥]/g, '')),
        quantity: Number(item.quantity || 1),
        image: item.image || ''
      }))
    };

    request({
      url: '/api/OrderDetails/create',
      method: 'POST',
      data: payload,
      showLoading: false
    })
      .then((data) => {
        const orderId = data.orderId || data.id;
        if (!orderId) {
          wx.showToast({ title: '创建订单失败', icon: 'none' });
          this.setData({ loading: false, isCreatingOrder: false });
          return;
        }
        // 清空购物车
        wx.removeStorageSync('orderCart');
        // 用 redirectTo 替换当前页，避免页面栈过深，同时订单页 onLoad 会自动刷新
        wx.redirectTo({
          url: '/subpkg/orders/orders?tab=pending'
        });
      })
      .catch(() => {
        wx.showToast({ title: '下单失败', icon: 'none' });
      })
      .finally(() => {
        this.setData({ loading: false, isCreatingOrder: false });
      });
  },

  goBack: function () {
    wx.navigateBack();
  }
});
