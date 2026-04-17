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
      logistics: [] // 物流信息
    },
    loading: true,
    countdownText: '', // 倒计时显示文本
    remainingTime: 0 // 剩余毫秒数
  },
  
  countdownTimer: null,

  onLoad(options) {
    const orderId = options.id;

    if (!orderId) {
      this.setData({ loading: false });
      wx.showToast({
        title: '缺少订单ID',
        icon: 'none'
      });
      return;
    }

    this.getOrderDetail(orderId);
  },

  onShow() {
    if (this.data.order && this.data.order.id && this.data.order.status === 'pending') {
      this.startCountdown();
    }
  },

  onHide() {
    this.stopCountdown();
  },

  onUnload() {
    this.stopCountdown();
  },

  // 处理图片路径，确保使用正确的基础 URL
  processImageUrl: function (imageUrl) {
    if (!imageUrl) return '';
    
    // 去除反引号和空格
    imageUrl = imageUrl.replace(/[`\s]/g, '');
    
    // 如果是完整的 URL，替换基础 URL
    if (imageUrl.startsWith('http://') || imageUrl.startsWith('https://')) {
      // 替换 127.0.0.1:5000 为 192.168.203.56
      return imageUrl.replace('http://127.0.0.1:5000', 'http://192.168.203.56');
    }
    
    // 如果是相对路径，添加基础 URL
    return 'http://192.168.203.56' + imageUrl;
  },

  getOrderDetail(orderId) {
    wx.showLoading({ title: '加载中...' });

    api.order.getDetail(orderId)
      .then((orderData) => {
        console.log('获取订单详情成功，数据:', orderData);
        // 处理新API返回的数据格式
        if (!orderData) {
          orderData = {
            id: orderId,
            type: '',
            typeText: '',
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
          };
        }
        
        // 处理订单商品图片路径
        orderData.items = (orderData.items || []).map(item => ({
          ...item,
          image: this.processImageUrl(item.image),
          price: item.price ? item.price.toString().replace(/[¥￥]/g, '') : item.price
        }));
        
        // 处理订单总价
        orderData.totalPrice = orderData.totalPrice ? orderData.totalPrice.toString().replace(/[¥￥]/g, '') : orderData.totalPrice;
        
        // 根据后端返回的 type 字段判断订单类型
        orderData.isActivityOrder = orderData.type === 'activity';
        orderData.isAcreOrder = orderData.type === 'acre';
        
        this.setData({
          order: orderData,
          loading: false
        });
        
        // 如果是待付款订单，初始化倒计时
        if (orderData.status === 'pending') {
          this.initCountdown(orderData);
          orderTimer.startTimer(orderData.id, orderData.createTime, (orderId) => {
            this.handleOrderTimeout(orderId);
          });
        }
        
        // 为活动订单获取核销二维码
        if (orderData.isActivityOrder && orderData.status !== 'pending' && orderData.status !== 'cancelled') {
          this.getActivityOrderQrcode(orderId, orderData);
        }
      })
      .catch((err) => {
        console.error('获取订单详情失败:', err);
        this.setData({ loading: false });
        wx.showToast({
          title: '获取订单详情失败',
          icon: 'none'
        });
      })
      .finally(() => {
        wx.hideLoading();
      });
  },

  initCountdown(order) {
    const remaining = orderTimer.getRemainingTime(order.createTime);
    this.setData({
      remainingTime: remaining,
      countdownText: orderTimer.formatTime(remaining)
    });
    this.startCountdown();
  },

  startCountdown() {
    this.stopCountdown();
    this.countdownTimer = setInterval(() => {
      this.updateCountdown();
    }, 1000);
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
      this.setData({
        remainingTime: remaining,
        countdownText: orderTimer.formatTime(remaining)
      });
    } else {
      this.stopCountdown();
    }
  },

  handleOrderTimeout(orderId) {
    wx.showToast({
      title: '订单已超时取消',
      icon: 'none',
      duration: 2000
    });
    
    this.getOrderDetail(orderId);
  },

  // 获取活动订单核销二维码
  getActivityOrderQrcode(orderId, orderData) {
    api.order.getQrcode(orderId)
      .then((qrcodeData) => {
        if (qrcodeData && qrcodeData.qrCodeUrl) {
          orderData.qrcode = qrcodeData.qrCodeUrl;
          orderData.verifyCode = qrcodeData.verifyCode;
        } else {
          // 使用默认二维码图片作为备用
          orderData.qrcode = 'http://192.168.203.56/api/file/image/farm_000000000007.jpg';
        }
        
        this.setData({
          order: orderData,
          loading: false
        });
      })
      .catch((err) => {
        console.error('获取活动订单二维码失败:', err);
        // 失败时使用默认二维码图片
        orderData.qrcode = 'http://192.168.203.56/api/file/image/farm_000000000007.jpg';
        
        this.setData({
          order: orderData,
          loading: false
        });
      });
  },

  payOrder() {
    const orderId = this.data.order.id;
    wx.navigateTo({
      url: `/subpkg/pay/pay?orderId=${orderId}`
    });
  },

  goToOrders() {
    wx.navigateTo({
      url: '/subpkg/orders/orders'
    });
  },

  goBack() {
    wx.navigateBack();
  },

  // 取消订单
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
          .catch((err) => {
            console.error('取消订单失败:', err);
            wx.hideLoading();
            wx.showToast({ title: '取消订单失败', icon: 'none' });
          });
        }
      }
    });
  },

  // 确认收货
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
          .catch((err) => {
            console.error('确认收货失败:', err);
            wx.hideLoading();
            wx.showToast({ title: '确认收货失败', icon: 'none' });
          });
        }
      }
    });
  },

  // 模拟发货（联调用）
  markShipping() {
    wx.showModal({
      title: '确认发货',
      content: '确认将订单更新为待收货吗？',
      success: (res) => {
        if (!res.confirm) {
          return;
        }

        wx.showLoading({ title: '处理中...' });
        api.order.updateStatus(this.data.order.id, 'shipping')
          .then(() => {
            wx.showToast({ title: '已更新为待收货', icon: 'success' });
            this.getOrderDetail(this.data.order.id);
          })
          .catch((err) => {
            console.error('更新发货状态失败:', err);
            wx.showToast({ title: '更新失败', icon: 'none' });
          })
          .finally(() => {
            wx.hideLoading();
          });
      }
    });
  },

  // 申请退款
  applyRefund() {
    wx.showModal({
      title: '请联系能记家庭农场客服进行退款',
      content: '手机号：15876534944\n     微信号：njjtnc15876534944',
      showCancel: false
    });
  },

  // 联系客服
  contactService() {
    wx.showModal({
      title: '能记家庭农场客服',
      content: '手机号：15876534944\n     微信号：njjtnc15876534944',
      showCancel: false
    });
  },

  // 跳转到物流详情页
  goToLogisticsDetail() {
    const orderId = this.data.order.id;
    if (!orderId) {
      wx.showToast({
        title: '订单ID获取失败',
        icon: 'none'
      });
      return;
    }
    wx.navigateTo({
      url: `/subpkg/logistics-detail/logistics-detail?orderId=${orderId}`
    });
  }
});
