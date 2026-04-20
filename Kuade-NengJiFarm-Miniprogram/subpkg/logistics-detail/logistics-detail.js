const { api } = require('../../utils/api');

Page({
  data: {
    orderId: '',
    loading: true,
    logisticsInfo: {},
    logisticsTrace: [],
    shippingAddress: null,
    orderItems: [],
    statusIcon: '🚚'
  },

  onLoad(options) {
    const orderId = options.orderId;
    if (!orderId) {
      wx.showToast({
        title: '缺少订单ID',
        icon: 'none'
      });
      setTimeout(() => {
        wx.navigateBack();
      }, 1500);
      return;
    }

    this.setData({ orderId });
    this.getLogisticsDetail(orderId);
  },

  // 获取物流详情
  getLogisticsDetail(orderId) {
    wx.showLoading({ title: '加载中...' });

    // 先尝试获取订单详情作为备用数据
    api.order.getDetail(orderId)
      .then((orderData) => {
        console.log('获取订单详情成功:', orderData);
        
        // 先尝试获取物流详情
        return api.logistics.getDetail(orderId)
          .then((data) => {
            console.log('获取物流详情成功:', data);
            // 合并订单数据和物流数据
            data.shippingAddress = orderData.shippingAddress;
            data.items = orderData.items;
            this.setLogisticsData(data);
            return this.getLogisticsTrace(orderId, orderData);
          })
          .catch((err) => {
            console.warn('获取物流详情失败，使用订单数据:', err);
            // 如果API失败，从订单数据获取
            this.setMockLogisticsData(orderId, orderData);
            return this.getLogisticsTrace(orderId, orderData);
          });
      })
      .catch((orderErr) => {
        console.warn('获取订单详情失败，尝试直接获取物流:', orderErr);
        // 订单获取也失败，继续尝试物流API
        return api.logistics.getDetail(orderId)
          .then((data) => {
            console.log('获取物流详情成功:', data);
            this.setLogisticsData(data);
            return this.getLogisticsTrace(orderId, null);
          })
          .catch((err) => {
            console.warn('获取物流详情也失败，使用模拟数据:', err);
            this.setMockLogisticsData(orderId, null);
            return this.getLogisticsTrace(orderId, null);
          });
      })
      .finally(() => {
        wx.hideLoading();
      });
  },

  // 获取物流轨迹
  getLogisticsTrace(orderId, orderData) {
    return api.logistics.getTrace(orderId)
      .then((traceData) => {
        console.log('获取物流轨迹成功:', traceData);
        this.setLogisticsTrace(traceData);
      })
      .catch((err) => {
        console.warn('获取物流轨迹失败，使用模拟数据:', err);
        // 如果API失败，使用模拟轨迹数据
        this.setMockLogisticsTrace(orderData);
      });
  },

  // 设置物流数据
  setLogisticsData(data) {
    let statusIcon = '🚚';
    if (data.status === 'delivered' || data.status === 'completed') {
      statusIcon = '✅';
    } else if (data.status === 'shipping' || data.status === 'transporting') {
      statusIcon = '🚚';
    } else if (data.status === 'picked') {
      statusIcon = '📦';
    }

    // 处理商品图片
    let processedItems = [];
    if (data.items) {
      processedItems = data.items.map(item => ({
        ...item,
        image: this.processImageUrl(item.image)
      }));
    }

    this.setData({
      logisticsInfo: data,
      shippingAddress: data.shippingAddress || null,
      orderItems: processedItems,
      statusIcon: statusIcon,
      loading: false
    });
  },

  // 设置物流轨迹
  setLogisticsTrace(traceData) {
    let trace = [];
    if (Array.isArray(traceData)) {
      trace = traceData;
    } else if (traceData && Array.isArray(traceData.trace)) {
      trace = traceData.trace;
    }

    this.setData({
      logisticsTrace: trace
    });
  },

  // 处理图片URL
  processImageUrl: function(imageUrl) {
    if (!imageUrl) return '';
    imageUrl = imageUrl.replace(/[`\s]/g, '');
    if (imageUrl.startsWith('http://') || imageUrl.startsWith('https://')) {
      return imageUrl.replace('http://127.0.0.1:5000', 'http://192.168.203.56');
    }
    return 'http://192.168.203.56' + imageUrl;
  },

  // 设置模拟物流数据
  setMockLogisticsData(orderId, orderData) {
    const now = new Date();
    
    // 处理商品图片
    let processedItems = [];
    if (orderData && orderData.items) {
      processedItems = orderData.items.map(item => ({
        ...item,
        image: this.processImageUrl(item.image)
      }));
    }
    
    const logisticsData = {
      companyName: '顺丰速运',
      waybillNo: orderId,
      status: 'shipping',
      statusText: '运输中',
      shippingAddress: orderData ? orderData.shippingAddress : {
        name: '收货人',
        phone: '13800138000',
        address: '广东省深圳市南山区'
      },
      items: processedItems
    };

    let statusIcon = '🚚';
    if (orderData && (orderData.status === 'completed' || orderData.status === 'delivered')) {
      statusIcon = '✅';
      logisticsData.status = 'delivered';
      logisticsData.statusText = '已签收';
    }

    this.setData({
      logisticsInfo: logisticsData,
      shippingAddress: logisticsData.shippingAddress,
      orderItems: logisticsData.items,
      statusIcon: statusIcon,
      loading: false
    });
  },

  // 设置模拟物流轨迹
  setMockLogisticsTrace(orderData) {
    const now = new Date();
    const trace = [];
    
    // 添加发货信息
    trace.push({
      time: this.formatDate(new Date(now.getTime() - 2 * 60 * 60 * 1000)),
      desc: '快件已从【深圳转运中心】发出，准备发往下一站',
      location: '深圳市'
    });
    
    trace.push({
      time: this.formatDate(new Date(now.getTime() - 4 * 60 * 60 * 1000)),
      desc: '快件已到达【深圳转运中心】',
      location: '深圳市'
    });
    
    trace.push({
      time: this.formatDate(new Date(now.getTime() - 6 * 60 * 60 * 1000)),
      desc: '快件已从【深圳龙岗营业部】发出',
      location: '深圳市'
    });
    
    trace.push({
      time: this.formatDate(new Date(now.getTime() - 8 * 60 * 60 * 1000)),
      desc: '【深圳龙岗营业部】已揽收',
      location: '深圳市'
    });
    
    // 如果订单已完成，添加签收信息
    if (orderData && (orderData.status === 'completed' || orderData.status === 'delivered')) {
      trace.unshift({
        time: this.formatDate(now),
        desc: '快件已签收，签收人：本人',
        location: '深圳市'
      });
    }

    this.setData({
      logisticsTrace: trace
    });
  },

  // 格式化日期
  formatDate(date) {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    const hours = String(date.getHours()).padStart(2, '0');
    const minutes = String(date.getMinutes()).padStart(2, '0');
    return `${year}-${month}-${day} ${hours}:${minutes}`;
  },

  // 复制运单号
  copyWaybillNo() {
    const waybillNo = this.data.logisticsInfo.waybillNo || this.data.orderId;
    if (!waybillNo) {
      wx.showToast({
        title: '暂无运单号',
        icon: 'none'
      });
      return;
    }

    wx.setClipboardData({
      data: waybillNo,
      success: () => {
        wx.showToast({
          title: '复制成功',
          icon: 'success'
        });
      },
      fail: () => {
        wx.showToast({
          title: '复制失败',
          icon: 'none'
        });
      }
    });
  },

  // 返回上一页
  goBack() {
    wx.navigateBack();
  }
});
