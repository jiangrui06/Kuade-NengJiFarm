const { api } = require('../../utils/api');
const { orderTimer } = require('../../utils/order-timer');

Page({
  data: {
    order: {
      id: '',
      status: '',
      statusText: '',
      createTime: '',
      paymentTime: null,
      shippingTime: null,
      completeTime: null,
      totalPrice: 0,
      shippingAddress: {
        name: '',
        phone: '',
        address: ''
      },
      items: [],
      paymentMethod: null,
      transactionId: null,
      logistics: []
    },
    loading: true,
    countdownText: '',
    remainingTime: 0
  },

  countdownTimer: null,
  globalTimerStarted: false,

  onLoad(options) {
    const orderId = options.id;
    if (!orderId) {
      this.setData({ loading: false });
      wx.showToast({ title: '缺少订单ID', icon: 'none' });
      return;
    }
    this.getOrderDetail(orderId);
  },

  onShow() {
    if (this.data.order && this.data.order.id) {
      if (this.data.order.status === 'pending') {
        this.startCountdown();
        this.startGlobalTimer(this.data.order);
      }
    }
  },

  onHide() {
    this.stopCountdown();
    this.stopGlobalTimer();
  },

  onUnload() {
    this.stopCountdown();
    this.stopGlobalTimer();
  },

  processImageUrl: function (imageUrl) {
    if (!imageUrl) return '';
    imageUrl = imageUrl.replace(/[`\s]/g, '');
    if (imageUrl.startsWith('http://') || imageUrl.startsWith('https://')) {
      return imageUrl.replace('http://192.168.203.56', 'http://192.168.203.56');
    }
    return 'http://192.168.203.56' + imageUrl;
  },

  getOrderDetail(orderId) {
    wx.showLoading({ title: '加载中...' });

    api.order.getDetail(orderId)
      .then((orderData) => {
        console.log('获取订单详情成功，数据:', orderData);
        if (!orderData) {
          orderData = {
            id: orderId, orderId: '', type: '', typeText: '', status: '', statusText: '',
            createTime: '', paymentTime: null, shippingTime: null, completeTime: null,
            totalPrice: 0, totalQuantity: 0, tableNumber: 0,
            shippingAddress: { name: '', phone: '', address: '' },
            items: [], paymentMethod: null, transactionId: null, logistics: []
          };
        }

        // 统一订单ID字段
        orderData.id = orderData.id || orderData.orderNumber || orderId;
        orderData.orderId = orderData.orderId || orderData.id;
        
        // 统一类型文本
        if (!orderData.typeText) {
          const typeMap = { goods: '商品订单', food: '点餐订单', activity: '活动订单', acre: '认购订单' };
          orderData.typeText = typeMap[orderData.type] || '订单';
        }

        // 处理订单明细
        orderData.items = (orderData.items || []).map(item => {
          const price = item.price ? item.price.toString().replace(/[¥￥]/g, '') : item.price;
          const quantity = item.quantity || 1;
          const subtotal = (parseFloat(price) * quantity).toFixed(2);
          return {
            ...item,
            image: this.processImageUrl(item.image),
            price: price,
            subtotal: subtotal
          };
        });
        
        // 处理订单金额
        orderData.totalPrice = orderData.totalPrice ? orderData.totalPrice.toString().replace(/[¥￥]/g, '') : orderData.totalPrice;
        
        // 标记订单类型
        orderData.isActivityOrder = orderData.type === 'activity';
        orderData.isAcreOrder = orderData.type === 'acre';
        orderData.isFoodOrder = orderData.type === 'food';
        orderData.isGoodsOrder = orderData.type === 'goods';
        orderData.isCancelledOrder = orderData.status === 'cancelled';

        // 已取消订单：获取取消时间
        if (orderData.isCancelledOrder) {
          if (orderData.cancelTime || orderData.cancelledTime) {
            orderData.cancelTime = orderData.cancelTime || orderData.cancelledTime;
          } else if (orderData.updateTime) {
            orderData.cancelTime = orderData.updateTime;
          } else {
            const localCancelTime = orderTimer.getCancelledTime(orderData.id);
            if (localCancelTime) {
              const d = new Date(localCancelTime);
              orderData.cancelTime = `${d.getFullYear()}-${String(d.getMonth()+1).padStart(2,'0')}-${String(d.getDate()).padStart(2,'0')} ${String(d.getHours()).padStart(2,'0')}:${String(d.getMinutes()).padStart(2,'0')}:${String(d.getSeconds()).padStart(2,'0')}`;
            }
          }
        }

        this.setData({ order: orderData, loading: false });

        if (orderData.status === 'pending') {
          this.initCountdown(orderData);
          this.startGlobalTimer(orderData);
        } else {
          // 其他状态：停止所有计时器
          this.stopCountdown();
          this.stopGlobalTimer();
          this.setData({ countdownText: '', remainingTime: 0 });
        }

        if (orderData.isActivityOrder && orderData.status !== 'pending' && orderData.status !== 'cancelled') {
          this.getActivityOrderQrcode(orderId, orderData);
        }
      })
      .catch((err) => {
        console.error('获取订单详情失败:', err);
        this.setData({ loading: false });
        wx.showToast({ title: '获取订单详情失败', icon: 'none' });
      })
      .finally(() => {
        wx.hideLoading();
      });
  },

  startGlobalTimer(orderData) {
    if (!this.globalTimerStarted) {
      orderTimer.startTimer(orderData.id, orderData.createTime, (orderId) => {
        this.handleOrderTimeout(orderId);
      });
      this.globalTimerStarted = true;
    }
  },

  stopGlobalTimer() {
    if (this.globalTimerStarted && this.data.order && this.data.order.id) {
      orderTimer.clearTimer(this.data.order.id);
      this.globalTimerStarted = false;
    }
  },

  initCountdown(order) {
    const remaining = orderTimer.getRemainingTime(order.createTime);
    this.setData({ remainingTime: remaining, countdownText: orderTimer.formatTime(remaining) });
    this.startCountdown();
  },

  startCountdown() {
    this.stopCountdown();
    this.countdownTimer = setInterval(() => { this.updateCountdown(); }, 1000);
  },

  stopCountdown() {
    if (this.countdownTimer) {
      clearInterval(this.countdownTimer);
      this.countdownTimer = null;
    }
  },

  updateCountdown() {
    const { order } = this.data;
    if (!order || !order.id) return;

    if (order.status === 'pending') {
      const remaining = orderTimer.getRemainingTime(order.createTime);
      this.setData({ remainingTime: remaining, countdownText: orderTimer.formatTime(remaining) });
      if (remaining <= 0) {
        this.stopCountdown();
        this.setData({ countdownText: '00:00' });
        setTimeout(() => { this.getOrderDetail(order.id); }, 500);
      }
    } else {
      this.stopCountdown();
    }
  },

  handleOrderTimeout(orderId) {
    wx.showToast({ title: '订单已超时取消', icon: 'none', duration: 2000 });
    this.getOrderDetail(orderId);
  },

  getActivityOrderQrcode(orderId, orderData) {
    api.order.getQrcode(orderId)
      .then((qrcodeData) => {
        if (qrcodeData && qrcodeData.qrCodeUrl) {
          orderData.qrcode = qrcodeData.qrCodeUrl;
          orderData.verifyCode = qrcodeData.verifyCode;
        } else {
          orderData.qrcode = 'http://192.168.203.56/api/file/image/farm_000000000007.jpg';
        }
        this.setData({ order: orderData, loading: false });
      })
      .catch(() => {
        orderData.qrcode = 'http://192.168.203.56/api/file/image/farm_000000000007.jpg';
        this.setData({ order: orderData, loading: false });
      });
  },

  payOrder() {
    wx.navigateTo({ url: `/user-pages/pay/pay?orderId=${this.data.order.id}` });
  },

  goToOrders() {
    wx.navigateTo({ url: '/user-pages/orders/orders' });
  },

  goBack() {
    wx.navigateBack();
  },

  // 删除已取消的订单
  deleteOrder() {
    const { order } = this.data;
    wx.showModal({
      title: '确认删除',
      content: '确定要删除这个已取消的订单吗？删除后将无法恢复。',
      confirmText: '删除',
      cancelText: '取消',
      confirmColor: '#e64340',
      success: (res) => {
        if (res.confirm) {
          wx.showLoading({ title: '删除中...' });
          api.order.delete(order.id)
            .then(() => {
              wx.hideLoading();
              wx.showToast({ title: '订单已删除', icon: 'success' });
              setTimeout(() => { wx.navigateBack(); }, 1500);
            })
            .catch(() => {
              wx.hideLoading();
              wx.showToast({ title: '删除失败，请重试', icon: 'none' });
            });
        }
      }
    });
  },

  cancelOrder() {
    wx.showModal({
      title: '确认取消',
      content: '确定要取消这个订单吗？',
      success: (res) => {
        if (res.confirm) {
          wx.showLoading({ title: '取消中...' });
          api.order.updateStatus(this.data.order.id, 'cancelled')
            .then(() => {
              wx.hideLoading();
              // 立即停止倒计时和全局计时器，清空倒计时显示
              this.stopCountdown();
              this.stopGlobalTimer();
              this.setData({ countdownText: '', remainingTime: 0 });
              wx.showToast({ title: '订单已取消', icon: 'success' });
              this.getOrderDetail(this.data.order.id);
            })
            .catch(() => {
              wx.hideLoading();
              wx.showToast({ title: '取消订单失败', icon: 'none' });
            });
        }
      }
    });
  },

  confirmReceipt() {
    wx.showModal({
      title: '确认收货',
      content: '确定已收到商品吗？',
      success: (res) => {
        if (res.confirm) {
          wx.showLoading({ title: '确认中...' });
          api.order.updateStatus(this.data.order.id, 'completed')
            .then(() => {
              wx.hideLoading();
              wx.showToast({ title: '收货成功', icon: 'success' });
              this.getOrderDetail(this.data.order.id);
            })
            .catch(() => {
              wx.hideLoading();
              wx.showToast({ title: '确认收货失败', icon: 'none' });
            });
        }
      }
    });
  }
});

