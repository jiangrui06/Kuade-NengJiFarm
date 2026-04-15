const { api, request } = require('../../utils/api');

Page({
  data: {
    // 订单ID
    orderId: '',
    // 支付金额
    totalPrice: 0,
    // 加载状态
    loading: false,
    // 支付状态
    payStatus: 'pending', // pending, success, failed
    activityId: '',
    source: '',
    clearCartList: false
  },

  onLoad: function (options) {
    // 获取订单ID和支付金额
    const orderId = options.orderId || '';
    const totalPrice = Number(options.totalPrice || 0);
    const activityId = options.activityId || '';
    const source = options.source || '';
    const clearCartList = options.clearCartList === '1';

    if (!orderId) {
      wx.showToast({
        title: '缺少订单ID',
        icon: 'none'
      });
      this.setData({ payStatus: 'failed' });
      return;
    }

    this.setData({
      orderId,
      totalPrice,
      activityId,
      source,
      clearCartList
    });

    // 自动开始支付
    this.ensurePayAmountAndStart();
  },

  ensurePayAmountAndStart: function () {
    if (this.data.totalPrice > 0) {
      this.startPayment();
      return;
    }

    wx.showLoading({ title: '加载订单中...' });
    api.order.getDetail(this.data.orderId)
      .then((orderData) => {
        const amount = Number((orderData.totalPrice || 0).toString().replace(/[¥￥]/g, ''));
        if (amount <= 0) {
          throw new Error('order amount invalid');
        }

        this.setData(
          { totalPrice: Number(amount.toFixed(2)) },
          () => this.startPayment()
        );
      })
      .catch((err) => {
        console.error('获取订单金额失败:', err);
        this.setData({ payStatus: 'failed' });
        wx.showToast({
          title: '订单金额获取失败',
          icon: 'none'
        });
      })
      .finally(() => {
        wx.hideLoading();
      });
  },

  // 开始支付
  startPayment: function () {
    if (!this.data.orderId || this.data.totalPrice <= 0) {
      this.setData({
        loading: false,
        payStatus: 'failed'
      });
      wx.showToast({
        title: '支付参数错误',
        icon: 'none'
      });
      return;
    }

    this.setData({ loading: true });

    // 构建支付请求参数
    const requestData = {
      paymentMethod: 'wechat',
      amount: this.data.totalPrice
    };

    // 调用支付API
    api.order.pay(this.data.orderId, requestData)
      .then(() => {
        this.setData({ loading: false });
        this.setData({ payStatus: 'success' });
      })
      .catch((err) => {
        console.error('发起支付失败:', err);
        this.setData({
          loading: false,
          payStatus: 'failed'
        });
        wx.showToast({
          title: '发起支付失败',
          icon: 'none'
        });
      });
  },

  // 重新支付
  retryPayment: function () {
    this.setData({ payStatus: 'pending' });
    this.ensurePayAmountAndStart();
  },

  // 查看订单
  viewOrder: function () {
    // 清空购物车数据
    this.clearCart();
    wx.navigateTo({
      url: `/subpkg/orders-detail/orders-detail?id=${this.data.orderId}`
    });
  },

  // 完成支付
  completePayment: function () {
    // 清空购物车数据
    this.clearCart();

    // 根据来源决定跳转方向
    if (this.data.source === 'activity' && this.data.activityId) {
      // 跳转到活动详情页面，使用redirectTo替换当前页面，避免导航栈问题
      wx.redirectTo({
        url: `/subpkg/activity-detail/activity-detail?id=${this.data.activityId}&paid=true&orderId=${this.data.orderId}`
      });
    } else {
      // 跳转到首页
      wx.switchTab({
        url: '/pages/index/index'
      });
    }
  },

  // 清空购物车
  clearCart: function () {
    // 清空本地存储中的购物车数据
    wx.removeStorageSync('orderCart');
    if (this.data.clearCartList) {
      wx.removeStorageSync('cartList');
    }
  }
});
