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
    if (!this.data.orderId) {
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

    // 调用新的微信支付 API 创建 JSAPI 支付参数
    api.pay.createJsapi(this.data.orderId, {
      description: `能记农场订单 ${this.data.orderId}`
    })
      .then((data) => {
        // 检查订单是否已经支付
        if (data.paymentStatus === 1 && !data.payParams) {
          this.setData({ loading: false });
          this.setData({ payStatus: 'success' });
          wx.showToast({
            title: '订单已支付',
            icon: 'success'
          });
          return;
        }

        // 检查是否有 payParams
        if (!data.payParams) {
          throw new Error('支付参数获取失败');
        }

        // 调起微信支付
        return this.requestWeChatPayment(data.payParams);
      })
      .then(() => {
        // 微信支付成功，查询支付状态
        return api.pay.queryStatus(this.data.orderId);
      })
      .then((status) => {
        if (status && status.paid) {
          this.setData({ loading: false });
          this.setData({ payStatus: 'success' });
          wx.showToast({
            title: '支付成功',
            icon: 'success'
          });
        } else {
          throw new Error('支付状态异常');
        }
      })
      .catch((err) => {
        console.error('支付失败:', err);
        this.setData({
          loading: false,
          payStatus: 'failed'
        });
        if (err.errMsg && err.errMsg.indexOf('requestPayment:fail') !== -1) {
          if (err.errMsg.indexOf('cancel') !== -1) {
            wx.showToast({
              title: '支付已取消',
              icon: 'none'
            });
          } else {
            wx.showToast({
              title: '支付失败',
              icon: 'none'
            });
          }
        } else {
          wx.showToast({
            title: err.message || '支付失败',
            icon: 'none'
          });
        }
      });
  },

  // 调起微信支付
  requestWeChatPayment: function (payParams) {
    return new Promise((resolve, reject) => {
      wx.requestPayment({
        timeStamp: String(payParams.timeStamp),
        nonceStr: payParams.nonceStr,
        package: payParams.package,
        signType: payParams.signType,
        paySign: payParams.paySign,
        success: resolve,
        fail: reject
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
