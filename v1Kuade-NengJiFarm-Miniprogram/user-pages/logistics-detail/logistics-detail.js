const { api } = require('../../utils/api');
// 引入物流查询插件
const plugin = requirePlugin("logisticsPlugin");

Page({
  data: {
    orderId: '',
    loading: true,
    logisticsInfo: {},
    logisticsTrace: [],
    shippingAddress: null,
    orderItems: [],
    statusIcon: '🚚',
    statusHint: '您的包裹正在运输中',
    // 物流插件所需数据
    waybillId: '',
    deliveryId: '',
    deliveryName: '',
    transId: '',
    pluginLoading: false
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
            console.warn('获取物流详情失败，使用订单数据', err);
            // 如果API失败，从订单数据获取
            this.setMockLogisticsData(orderId, orderData);
            return this.getLogisticsTrace(orderId, orderData);
          });
      })
      .catch((orderErr) => {
        console.warn('获取订单详情失败，尝试直接获取物流', orderErr);
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
        console.warn('获取物流轨迹失败，使用模拟数据', err);
        // 如果API失败，使用模拟轨迹数据
        this.setMockLogisticsTrace(orderData);
      });
  },

  // 根据状态获取图标和提示语
  _getStatusMeta(status) {
    const map = {
      'pending':      { icon: '📦', hint: '商家正在准备发货' },
      'paid':         { icon: '📦', hint: '商家正在准备发货' },
      'picked':       { icon: '📦', hint: '快递员已揽收' },
      'shipping':     { icon: '🚚', hint: '您的包裹正在运输中' },
      'transporting': { icon: '🚚', hint: '您的包裹正在运输中' },
      'delivering':   { icon: '🚚', hint: '快递员正在派送中' },
      'delivered':    { icon: '✅', hint: '包裹已签收' },
      'completed':    { icon: '✅', hint: '包裹已签收' },
      'signed':       { icon: '✅', hint: '包裹已签收' }
    };
    return map[status] || { icon: '🚚', hint: '物流信息更新中' };
  },

  // 设置物流数据
  setLogisticsData(data) {
    const meta = this._getStatusMeta(data.status);
    const statusIcon = meta.icon;
    const statusHint = meta.hint;

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
      statusHint: statusHint,
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
    const utils = require('../../utils/utils');
    return utils.media.processUrl(imageUrl);
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
    
    // 判断订单状态
    const orderStatus = orderData ? orderData.status : '';
    const isPaidOnly = orderStatus === 'paid';
    
    // 确定状态
    let status = 'shipping';
    let statusText = '运输中';
    if (isPaidOnly) {
      status = 'pending';
      statusText = '待发货';
    } else if (orderData && (orderData.status === 'completed' || orderData.status === 'delivered')) {
      status = 'delivered';
      statusText = '已签收';
    }

    const meta = this._getStatusMeta(status);

    const logisticsData = {
      companyName: isPaidOnly ? '' : '顺丰速运',
      companyPhone: isPaidOnly ? '' : '95338',
      waybillNo: isPaidOnly ? '' : orderId,
      status: status,
      statusText: statusText,
      shippingAddress: orderData ? orderData.shippingAddress : {
        name: '收货人',
        phone: '13800138000',
        address: '广东省深圳市南山区'
      },
      items: processedItems
    };

    this.setData({
      logisticsInfo: logisticsData,
      shippingAddress: logisticsData.shippingAddress,
      orderItems: logisticsData.items,
      statusIcon: meta.icon,
      statusHint: meta.hint,
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
      location: '深圳'
    });
    
    trace.push({
      time: this.formatDate(new Date(now.getTime() - 4 * 60 * 60 * 1000)),
      desc: '快件已到达【深圳转运中心】',
      location: '深圳'
    });

    this.setData({
      logisticsTrace: trace
    });
  },

  formatDate(date) {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    const hours = String(date.getHours()).padStart(2, '0');
    const minutes = String(date.getMinutes()).padStart(2, '0');
    const seconds = String(date.getSeconds()).padStart(2, '0');
    return `${year}-${month}-${day} ${hours}:${minutes}:${seconds}`;
  },

  copyWaybillNo() {
    wx.setClipboardData({
      data: this.data.logisticsInfo.waybillNo || '',
      success: () => {
        wx.showToast({ title: '单号已复制', icon: 'success' });
      }
    });
  },

  callCompany() {
    const phone = this.data.logisticsInfo.companyPhone || '95338';
    wx.makePhoneCall({
      phoneNumber: phone
    });
  },

  goBack() {
    wx.navigateBack();
  },

  // 查看物流详情（调用微信官方物流插件）
  viewLogisticsPlugin() {
    const { logisticsInfo, orderId } = this.data;

    // 检查是否有物流单号
    if (!logisticsInfo.waybillNo) {
      wx.showToast({ title: '暂无物流信息', icon: 'none' });
      return;
    }

    this.setData({ pluginLoading: true });

    // 获取openId
    const openId = wx.getStorageSync('openid') || wx.getStorageSync('openId') || '';

    // 调用后端接口获取waybill_token（使用新的API接口）
    api.logistics.getWaybillToken({
      openId: openId,
      waybillId: logisticsInfo.waybillNo,
      deliveryId: logisticsInfo.companyCode || 'SF', // 快递公司代码，默认顺丰
      receiverPhone: logisticsInfo.shippingAddress?.phone || '13800138000',
      transId: this.data.transId || '',
      goodsList: logisticsInfo.items?.map(item => ({
        goodsName: item.name || '能记农场商品',
        goodsImgUrl: item.image || ''
      })) || [{ goodsName: '能记农场商品', goodsImgUrl: '' }]
    })
      .then((data) => {
        const waybillToken = data.waybillToken;
        if (waybillToken) {
          // 调用微信官方插件打开物流详情页
          plugin.openWaybillTracking({
            waybillToken: waybillToken,
            success: () => {
              console.log('打开物流详情成功');
            },
            fail: (err) => {
              console.error('打开物流详情失败：', err);
              wx.showToast({ title: '打开失败', icon: 'none' });
            }
          });
        } else {
          wx.showToast({ title: '获取物流信息失败', icon: 'none' });
        }
      })
      .catch((err) => {
        console.error('获取物流token失败:', err);
        wx.showToast({ title: err.message || '获取物流信息失败', icon: 'none' });
      })
      .finally(() => {
        this.setData({ pluginLoading: false });
      });
  }
});
