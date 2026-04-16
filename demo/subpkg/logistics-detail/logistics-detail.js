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

    // 先尝试获取物流详情
    api.logistics.getDetail(orderId)
      .then((data) => {
        console.log('获取物流详情成功:', data);
        this.setLogisticsData(data);
        return this.getLogisticsTrace(orderId);
      })
      .catch((err) => {
        console.warn('获取物流详情失败，使用模拟数据:', err);
        // 如果API失败，使用模拟数据
        this.setMockLogisticsData(orderId);
        return this.getLogisticsTrace(orderId);
      })
      .finally(() => {
        wx.hideLoading();
      });
  },

  // 获取物流轨迹
  getLogisticsTrace(orderId) {
    return api.logistics.getTrace(orderId)
      .then((traceData) => {
        console.log('获取物流轨迹成功:', traceData);
        this.setLogisticsTrace(traceData);
      })
      .catch((err) => {
        console.warn('获取物流轨迹失败，使用模拟数据:', err);
        // 如果API失败，使用模拟轨迹数据
        this.setMockLogisticsTrace();
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

    this.setData({
      logisticsInfo: data,
      shippingAddress: data.shippingAddress || null,
      orderItems: data.items || [],
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
