const api = require('../../utils/api');

Page({
  data: {
    // 订单ID
    orderId: '',
    // 支付金额
    totalPrice: 0,
    // 加载状态
    loading: false,
    // 支付状态
    payStatus: 'pending' // pending, success, failed
  },

  onLoad: function (options) {
    // 获取订单ID和支付金额
    const orderId = options.orderId;
    const totalPrice = options.totalPrice || 0;
    const activityId = options.activityId;
    const source = options.source;
    
    if (!orderId && !source) {
      wx.showToast({
        title: '缺少订单ID',
        icon: 'none'
      });
      return;
    }
    
    this.setData({
      orderId: orderId,
      totalPrice: totalPrice,
      activityId: activityId,
      source: source
    });
    
    // 自动开始支付
    this.startPayment();
  },

  // 开始支付
  startPayment: function() {
    this.setData({ loading: true });
    
    // 构建支付请求参数
    const requestData = {
      paymentMethod: 'wechat',
      payAmount: this.data.totalPrice
    };
    
    // 确定API路径
    let apiUrl = '';
    if (this.data.orderId) {
      apiUrl = `/api/OrderDetails/${this.data.orderId}/pay`;
    } else if (this.data.source === 'activity' && this.data.activityId) {
      apiUrl = `/api/activity/${this.data.activityId}/pay`;
    } else {
      wx.showToast({
        title: '缺少支付参数',
        icon: 'none'
      });
      this.setData({ loading: false });
      return;
    }
    
    // 调用支付API
    api.request({
      url: apiUrl,
      method: 'POST',
      data: requestData
    })
    .then((data) => {
      this.setData({ loading: false });
      
      // 调用微信支付
      wx.requestPayment({
        timeStamp: data.paymentInfo.timeStamp,
        nonceStr: data.paymentInfo.nonceStr,
        package: data.paymentInfo.package,
        signType: data.paymentInfo.signType,
        paySign: data.paymentInfo.paySign,
        success: (res) => {
          console.log('支付成功:', res);
          this.setData({ payStatus: 'success' });
        },
        fail: (err) => {
          console.error('支付失败:', err);
          this.setData({ payStatus: 'failed' });
        }
      });
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
  retryPayment: function() {
    this.setData({ payStatus: 'pending' });
    this.startPayment();
  },

  // 查看订单
  viewOrder: function() {
    // 清空购物车数据
    this.clearCart();
    wx.navigateTo({ 
      url: `/subpkg/orders-detail/orders-detail?id=${this.data.orderId}`
    });
  },
  
  // 完成支付
  completePayment: function() {
    // 清空购物车数据
    this.clearCart();
    
    // 根据来源决定跳转方向
    if (this.data.source === 'activity' && this.data.activityId) {
      // 跳转到活动详情页面
      wx.navigateTo({ 
        url: `/subpkg/activity-detail/activity-detail?id=${this.data.activityId}&paid=true`
      });
    } else {
      // 跳转到首页
      wx.switchTab({ 
        url: '/pages/index/index'
      });
    }
  },
  
  // 清空购物车
  clearCart: function() {
    // 清空本地存储中的购物车数据
    wx.removeStorageSync('orderCart');
  }
});