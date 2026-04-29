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
    // 初始化页面状态
    this.initPageState();
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

  onShow: function () {
    // 每次显示页面时确保状态正常
    console.log('Pay page onShow');
  },

  onHide: function () {
    // 页面隐藏时清理临时状态
    console.log('Pay page onHide');
  },

  onUnload: function () {
    // 页面卸载时清理
    console.log('Pay page onUnload');
  },

  // 初始化页面状态
  initPageState: function () {
    this.setData({
      loading: false,
      payStatus: 'pending'
    });
    console.log('Pay page state initialized');
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
          throw new Error('订单金额异常');
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

    // 模拟支付逻辑 (实际项目中调用 api.pay.createJsapi)
    // 这里为了演示，直接调用后端模拟支付接口
    api.order.pay(this.data.orderId, {
      paymentMethod: 'wechat'
    })
      .then((data) => {
        this.setData({ 
          loading: false,
          payStatus: 'success'
        });
        wx.showToast({
          title: '支付成功',
          icon: 'success'
        });
        
        // 支付成功后的逻辑
        this.afterPaySuccess();
      })
      .catch((err) => {
        console.error('支付失败:', err);
        this.setData({
          loading: false,
          payStatus: 'failed'
        });
        wx.showToast({
          title: err.message || '支付失败',
          icon: 'none'
        });
      });
  },

  // 支付成功后的处理
  afterPaySuccess: function() {
    // 如果是活动订单，跳转回活动详情并显示二维码
    if (this.data.activityId) {
      setTimeout(() => {
        wx.redirectTo({
          url: `/user-pages/activity-detail/activity-detail?id=${this.data.activityId}&paid=true&orderId=${this.data.orderId}`
        });
      }, 1500);
    } else {
      // 普通订单跳转到订单列表
      setTimeout(() => {
        wx.redirectTo({
          url: '/user-pages/orders/orders?tab=paid'
        });
      }, 1500);
    }
  },

  // 返回上一页或订单列表
  goBack: function () {
    wx.navigateBack({
      fail: () => {
        wx.switchTab({
          url: '/pages/index/index'
        });
      }
    });
  },

  // 重新支付
  retryPay: function () {
    this.setData({ payStatus: 'pending' });
    this.startPayment();
  }
});

