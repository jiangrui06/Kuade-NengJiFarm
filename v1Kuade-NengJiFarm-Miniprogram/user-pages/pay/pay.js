const { api, request } = require('../../utils/api');

Page({
  data: {
    // 订单ID
    orderId: '',
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
    // 初始化页面状态
    this.initPageState();

    const orderId = options.orderId || '';
    const totalPrice = Number(options.totalPrice || 0);
    const activityId = options.activityId || '';
    // 订单类型：从参数获取，默认自动识别
    const orderType = options.type || '';
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
      orderType,
      clearCartList
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

    api.order.getDetail(this.data.orderId)
      .then((orderData) => {
        console.log('[支付页] 订单详情:', orderData);

        // 兼容响应包装：如果返回 { code, data } 结构，取 data
        const order = orderData.data || orderData;
        console.log('[支付页] 解析后订单:', order, 'status:', order.status, 'type:', order.type);

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
        console.error('获取订单信息失败:', err);
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
    if (!this.data.orderId) {
      this.setData({ loading: false, payStatus: 'failed' });
      wx.showToast({ title: '支付参数错误', icon: 'none' });
      return;
    }

    // 防止重复调用
    if (this.data.loading) {
      console.log('支付正在进行中，忽略重复调用');
      return;
    }

    this.setData({ loading: true });

    // 调用后端 JSAPI 支付接口
    api.pay.createJsapi({
      orderId: this.data.orderId,
      id: this.data.orderId,
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
              // 微信支付成功，查询支付状态并更新订单
              api.pay.queryStatus({ orderId: this.data.orderId })
                .then(() => {
                  console.log('支付状态查询成功');
                  this.handlePaySuccess();
                })
                .catch((err) => {
                  console.error('支付状态查询失败:', err);
                  // 即使查询失败，也视为支付成功（用户已付款）
                  this.handlePaySuccess();
                });
            },
            fail: (err) => {
              console.error('微信支付失败:', err);

              // 用户取消支付时，保持"待支付"状态
              if (err && (err.errMsg === 'requestPayment:fail cancel' || err.errMsg === 'requestPayment:fail user cancel')) {
                console.log('用户取消支付');

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
          console.log('没有返回支付参数，订单可能已支付');
          this.setData({
            loading: false,
            payStatus: 'failed',
            failReason: '订单可能已支付成功'
          });
          wx.showToast({ title: '订单可能已支付成功', icon: 'none' });
        }
      })
      .catch((err) => {
        console.error('获取支付参数失败:', err);

        // 如果是"订单正在支付中"错误，直接提示用户
        if (err && err.code === 400 && err.message && err.message.includes('支付中')) {
          console.log('订单正在支付中');

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

  // 支付成功后的处理
  afterPaySuccess: function() {
    // 清空购物车中已选中的商品
    this.clearCartAfterPay();
    
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

  // 支付成功后清空购物车中已选中的商品
  clearCartAfterPay: function() {
    try {
      // 获取当前购物车数据
      const rawCartList = wx.getStorageSync('cartList') || [];
      const cartList = Array.isArray(rawCartList) ? rawCartList : Object.values(rawCartList);
      
      // 记录已购买的农场优选商品ID
      const purchasedFarmGoods = wx.getStorageSync('purchasedFarmGoods') || [];
      const newPurchased = [...purchasedFarmGoods];
      
      // 过滤掉已选中的商品，并记录农场优选商品
      const remainingItems = cartList.filter(item => {
        if (item && item.type === 'goods' && item.checked) {
          // 如果是农场优选商品，记录到已购买列表
          if (item.isFarmGood) {
            const itemId = String(item.id);
            if (!newPurchased.includes(itemId)) {
              newPurchased.push(itemId);
            }
          }
          return false; // 移除已选中的商品
        }
        return true; // 保留未选中的商品
      });
      
      // 保存更新后的购物车和已购买列表
      wx.setStorageSync('cartList', remainingItems);
      wx.setStorageSync('purchasedFarmGoods', newPurchased);
      
      console.log('[pay] 支付成功，已清空购物车中选中的商品');
    } catch (err) {
      console.error('[pay] 清空购物车失败:', err);
    }
  },

  // 查看订单列表（支付成功后）
  goOrders: function () {
    wx.redirectTo({
      url: '/user-pages/orders/orders?tab=paid'
    });
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
    // 防止重复调用
    if (this.data.loading) {
      console.log('支付正在进行中，忽略重复调用');
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
  }
});

