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
    if (this.data.order && this.data.order.id && this.data.order.status === 'pending') {
      this.startCountdown();
      this.startGlobalTimer(this.data.order);
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
      return imageUrl.replace('http://127.0.0.1:5000', 'http://192.168.203.56');
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
            id: orderId, type: '', typeText: '', status: '', statusText: '',
            createTime: '', paymentTime: null, shippingTime: null, completeTime: null,
            totalPrice: 0, shippingAddress: { name: '', phone: '', address: '' },
            items: [], paymentMethod: null, transactionId: null, logistics: []
          };
        }

        orderData.items = (orderData.items || []).map(item => ({
          ...item,
          image: this.processImageUrl(item.image),
          price: item.price ? item.price.toString().replace(/[¥￥]/g, '') : item.price
        }));
        orderData.totalPrice = orderData.totalPrice ? orderData.totalPrice.toString().replace(/[¥￥]/g, '') : orderData.totalPrice;
        orderData.isActivityOrder = orderData.type === 'activity';
        orderData.isAcreOrder = orderData.type === 'acre';

        this.setData({ order: orderData, loading: false });

        if (orderData.status === 'pending') {
          this.initCountdown(orderData);
          this.startGlobalTimer(orderData);
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
    if (order && order.status === 'pending') {
      const remaining = orderTimer.getRemainingTime(order.createTime);
      this.setData({ remainingTime: remaining, countdownText: orderTimer.formatTime(remaining) });
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
    wx.navigateTo({ url: `/subpkg/pay/pay?orderId=${this.data.order.id}` });
  },

  goToOrders() {
    wx.navigateTo({ url: '/subpkg/orders/orders' });
  },

  goBack() {
    wx.navigateBack();
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
  },

  markShipping() {
    wx.showModal({
      title: '确认发货',
      content: '确认将订单更新为待收货吗？',
      success: (res) => {
        if (!res.confirm) return;
        wx.showLoading({ title: '处理中...' });
        api.order.updateStatus(this.data.order.id, 'shipping')
          .then(() => {
            wx.showToast({ title: '已更新为待收货', icon: 'success' });
            this.getOrderDetail(this.data.order.id);
          })
          .catch(() => {
            wx.showToast({ title: '更新失败', icon: 'none' });
          })
          .finally(() => {
            wx.hideLoading();
          });
      }
    });
  },

  applyRefund() {
    wx.showModal({
      title: '请联系能记家庭农场客服进行退款',
      content: '手机号：15876534944\n     微信号：njjtnc15876534944',
      showCancel: false
    });
  },

  contactService() {
    wx.showModal({
      title: '能记家庭农场客服',
      content: '手机号：15876534944\n     微信号：njjtnc15876534944',
      showCancel: false
    });
  },

  // 保存二维码到相册
  saveQrcode() {
    const qrcodeUrl = this.data.order.qrcode;
    if (!qrcodeUrl) {
      wx.showToast({ title: '二维码未加载', icon: 'none' });
      return;
    }

    wx.showLoading({ title: '保存中...' });

    wx.downloadFile({
      url: qrcodeUrl,
      success: (res) => {
        if (res.statusCode !== 200) {
          wx.hideLoading();
          wx.showToast({ title: '下载失败', icon: 'none' });
          return;
        }

        wx.saveImageToPhotosAlbum({
          filePath: res.tempFilePath,
          success: () => {
            wx.hideLoading();
            wx.showToast({ title: '已保存到相册', icon: 'success' });
          },
          fail: (err) => {
            wx.hideLoading();
            if (err.errMsg && err.errMsg.includes('auth deny')) {
              wx.showModal({
                title: '需要授权',
                content: '请允许保存图片到相册',
                success: (modalRes) => {
                  if (modalRes.confirm) {
                    wx.openSetting();
                  }
                }
              });
            } else {
              wx.showToast({ title: '保存失败', icon: 'none' });
            }
          }
        });
      },
      fail: () => {
        wx.hideLoading();
        wx.showToast({ title: '下载失败', icon: 'none' });
      }
    });
  },

  // 跳转到物流详情页
  goToLogisticsDetail() {
    const orderId = this.data.order.id;
    if (!orderId) {
      wx.showToast({ title: '订单ID获取失败', icon: 'none' });
      return;
    }
    wx.navigateTo({ url: `/subpkg/logistics-detail/logistics-detail?orderId=${orderId}` });
  }
});
