const { request } = require('../../utils/api');

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

  onLoad: function () {
    // 从本地存储获取购物车数据
    const cart = wx.getStorageSync('orderCart') || {};
    const cartItems = Object.values(cart);

    // 计算总价格和总数量
    let totalPrice = 0;
    let totalCount = 0;
    cartItems.forEach(item => {
      const price = Number((item.price || 0).toString().replace(/[¥￥]/g, ''));
      totalPrice += price * Number(item.quantity || 0);
      totalCount += Number(item.quantity || 0);
    });
    totalPrice = Number(totalPrice.toFixed(2));

    // 获取桌台号码
    const tableNumber = wx.getStorageSync('tableNumber');

    // 更新订单信息
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
    if (!paymentId) {
      return;
    }

    this.setData({
      selectedPayment: paymentId
    });
  },

  // 确认订单
  confirmOrder: function () {
    const items = this.data.orderInfo.items || [];
    if (!items.length) {
      wx.showToast({
        title: '购物车为空',
        icon: 'none'
      });
      return;
    }

    const totalPrice = Number(this.data.orderInfo.totalPrice || 0);
    if (totalPrice <= 0) {
      wx.showToast({
        title: '金额异常',
        icon: 'none'
      });
      return;
    }

    this.setData({ loading: true });

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
          wx.showToast({
            title: '创建订单失败',
            icon: 'none'
          });
          return;
        }

        wx.navigateTo({
          url: '/subpkg/orders/orders?tab=pending'
        });
      })
      .catch((err) => {
        console.error('创建点餐订单失败:', err);
        wx.showToast({
          title: '下单失败',
          icon: 'none'
        });
      })
      .finally(() => {
        this.setData({ loading: false });
      });
  },

  // 返回购物车
  goBack: function () {
    wx.navigateBack();
  }
});
