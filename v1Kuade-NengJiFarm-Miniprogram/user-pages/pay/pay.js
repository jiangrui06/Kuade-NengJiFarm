const { api, request } = require('../../utils/api');
const share = require('../../utils/share');

Page({
  data: {
    // 订单ID
    orderNo: '',
    // 订单类型 (goods/food/activity)
    orderType: '',
    // 支付金额
    totalPrice: 0,
    // 加载状态
    loading: false,
    // 支付状态
    payStatus: 'pending', // pending, success, failed
    // 失败原因（用于显示友好提示）
    failReason: '',
    // 失败详情
    failDetail: '',
    activityId: '',
    clearCartList: false
  },

  onLoad: function (options) {
    // 登录检查
    const { checkLogin } = require('../../utils/api');
    if (!checkLogin()) return;

    // 初始化页面状态
    this.initPageState();

    const orderNo = options.orderNo || '';
    const totalPrice = Number(options.totalPrice || 0);
    const activityId = options.activityId || '';
    // 订单类型：从参数获取，默认自动识别
    const orderType = options.type || '';
    const clearCartList = options.clearCartList === '1';
    const from = options.from || '';

    if (!orderNo) {
      wx.showToast({
        title: '缺少订单号',
        icon: 'none'
      });
      this.setData({ payStatus: 'failed' });
      return;
    }

    this.setData({
      orderNo,
      totalPrice,
      activityId,
      orderType,
      clearCartList,
      from
    });

    // 自动开始支付
    this.ensurePayAmountAndStart();
  },

  // 初始化页面状态
  initPageState: function () {
    this.setData({
      loading: false,
      payStatus: 'pending'
    });
  },

  // 验证订单信息并开始支付
  ensurePayAmountAndStart: function () {
    wx.showLoading({ title: '加载订单中...' });

    api.order.getDetail(this.data.orderNo)
      .then((orderData) => {

        // 兼容响应包装：如果返回 { code, data } 结构，取 data
        const order = orderData.data || orderData;

        // 验证订单状态（只能支付待付款订单）
        // 兼容数字和字符串状态：1/'pending'/'pending_payment' 都视为待付款
        const pendingStatuses = ['pending', 'pending_payment', 1, '1'];
        if (!pendingStatuses.includes(order.status)) {
          // 根据状态设置友好提示
          const statusTips = {
            'cancelled': '订单已取消，请重新下单',
            'paid': '订单已支付，无需重复支付',
            'ordered': '订单已支付，无需重复支付',
            'shipping': '订单已发货，无法支付',
            'completed': '订单已完成，无法支付',
            'refunding': '订单退款中，无法支付',
            'refunded': '订单已退款，请重新下单'
          };
          const tip = statusTips[order.status] || `订单状态异常(${order.status})，无法支付`;
          this.setData({
            failReason: tip,
            failDetail: `当前状态：${order.status}`
          });
          throw new Error(tip);
        }

        // 从订单数据中提取金额
        const amount = Number(order.totalPrice || order.totalAmount || 0);
        if (amount <= 0) {
          throw new Error('订单金额异常');
        }

        // 如果未指定类型，从订单数据中获取
        if (!this.data.orderType && order.type) {
          this.setData({ orderType: order.type });
        }

        this.setData(
          { totalPrice: Number(amount.toFixed(2)) },
          () => this.startPayment()
        );
      })
      .catch((err) => {
        const reason = err.message || '订单信息获取失败';
        // 如果还没有设置 failReason（状态异常时已设置），则使用错误消息
        if (!this.data.failReason) {
          this.setData({ failReason: reason });
        }
        this.setData({ payStatus: 'failed' });
        wx.showToast({
          title: reason,
          icon: 'none',
          duration: 3000
        });
      })
      .finally(() => {
        wx.hideLoading();
      });
  },

  // 开始支付
  startPayment: function () {
    if (!this.data.orderNo) {
      this.setData({ loading: false, payStatus: 'failed' });
      wx.showToast({ title: '支付参数错误', icon: 'none' });
      return;
    }

    // 防止重复调用
    if (this.data.loading) {
      return;
    }

    this.setData({ loading: true });

    // 调用后端 JSAPI 支付接口
    api.pay.createJsapi({
      orderNo: this.data.orderNo,
      type: this.data.orderType || undefined
    })
      .then((payParams) => {
        // 检查是否返回了微信支付参数
        if (payParams && payParams.timeStamp && payParams.nonceStr && payParams.package && payParams.paySign) {
          // 调用微信小程序支付
          wx.requestPayment({
            timeStamp: payParams.timeStamp,
            nonceStr: payParams.nonceStr,
            package: payParams.package,
            signType: payParams.signType || 'HMAC-SHA256',
            paySign: payParams.paySign,
            success: () => {
              // 微信支付成功，由后端 notify 异步更新订单状态
              this.handlePaySuccess();
            },
            fail: (err) => {

              // 用户取消支付时，保持"待支付"状态
              if (err && (err.errMsg === 'requestPayment:fail cancel' || err.errMsg === 'requestPayment:fail user cancel')) {

                this.setData({
                  loading: false,
                  payStatus: 'failed',
                  failReason: '支付已取消'
                });
                wx.showToast({ title: '支付已取消', icon: 'none' });
              } else {
                // 其他支付失败情况
                this.handlePayFail(err);
              }
            }
          });
        } else {
          // 没有返回支付参数（已支付等情况）
          this.setData({
            loading: false,
            payStatus: 'failed',
            failReason: '订单可能已支付成功'
          });
          wx.showToast({ title: '订单可能已支付成功', icon: 'none' });
        }
      })
      .catch((err) => {

        // 如果是"订单正在支付中"错误，直接提示用户
        if (err && err.code === 400 && err.message && err.message.includes('支付中')) {

          this.setData({
            loading: false,
            payStatus: 'failed',
            failReason: '订单正在支付中，请稍后重试'
          });
          wx.showToast({ title: '订单正在支付中，请稍后重试', icon: 'none' });
        } else {
          this.handlePayFail(err);
        }
      });
  },

  // 处理支付成功
  handlePaySuccess: function () {
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
  },

  // 处理支付失败
  handlePayFail: function (err) {
    const failReason = err && err.message ? err.message : '支付失败';
    this.setData({
      loading: false,
      payStatus: 'failed',
      failReason: failReason
    });
    wx.showToast({
      title: failReason,
      icon: 'none'
    });
  },

  // 支付成功后的处理（不自动跳转，用户手动操作）
  afterPaySuccess: function() {
    console.log('[pay] afterPaySuccess 被调用，orderType:', this.data.orderType);
    
    // 注意：购物车已在创建订单时清理（confirm-order.js），此处不再重复清理
    // 避免支付成功后重复清理导致数据异常

    // 不再自动跳转，让用户手动点击按钮导航
    wx.showToast({
      title: '支付成功',
      icon: 'success',
      duration: 2000
    });
  },

  // 查看订单列表（支付成功后）
  goOrders: function () {
    wx.redirectTo({
      url: '/user-pages/orders/orders?tab=paid'
    });
  },

  // 返回上一页或订单列表
  goBack: function () {
    const { orderType } = this.data;

    // 点餐订单 → 返回点餐界面
    if (orderType === 'food') {
      wx.redirectTo({
        url: '/user-pages/order/order'
      });
      return;
    }

    // 从购物车过来的 → 返回到购物车
    if (this.data.from === 'cart') {
      wx.switchTab({
        url: '/pages/cart/cart'
      });
    } else {
      // 订单支付 → 返回订单列表
      wx.redirectTo({
        url: '/user-pages/orders/orders'
      });
    }
  },

  // 重新支付
  retryPay: function () {
    // 防止重复调用
    if (this.data.loading) {
      return;
    }

    this.setData({ payStatus: 'pending' });
    this.startPayment();
  },

  // 重新下单
  goOrderAgain: function () {
    wx.switchTab({
      url: '/pages/index/index'
    });
  },

  onShareAppMessage: share.onShareAppMessage,
  onShareTimeline: share.onShareTimeline,

});

