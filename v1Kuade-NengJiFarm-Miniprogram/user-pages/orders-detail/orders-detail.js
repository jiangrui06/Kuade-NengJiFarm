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
      verifyTime: '',
      totalPrice: 0,
      diningTableNo: '',
      duration: 30,
      shippingAddress: {
        name: '',
        phone: '',
        address: ''
      },
      items: [],
      paymentMethod: null,
      transactionId: null,
      logistics: [],
      remark: ''  // 订单备注
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

  // 下拉刷新
  onPullDownRefresh() {
    if (this.data.order && this.data.order.id) {
      this.getOrderDetail(this.data.order.id, { showLoading: false });
    }
    // 请求完成后关闭刷新指示器
    setTimeout(() => {
      wx.stopPullDownRefresh();
    }, 1000);
  },

  processImageUrl: function (imageUrl) {
    const utils = require('../../utils/utils');
    return utils.media.processUrl(imageUrl);
  },

  getOrderDetail(orderId, options = {}) {
    const showLoading = options.showLoading !== false;
    if (showLoading) {
      wx.showLoading({ title: '加载中...' });
    }

    api.order.getDetail(orderId)
      .then((orderData) => {
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

        // 确保订单编号存在（用于显示）
        if (!orderData.orderNumber) {
          orderData.orderNumber = orderData.id;
        }
        
        // 统一类型文本
        if (!orderData.typeText) {
          const typeMap = { goods: '商品订单', food: '点餐订单', activity: '活动订单' };
          orderData.typeText = typeMap[orderData.type] || '订单';
        }

        // 统一状态文本：后端可能不返回 statusText
        if (!orderData.statusText) {
          const statusTextMap = {
            'pending': '待支付',
            'cancelled': '已取消',
            'paid': '待发货',
            'ordered': '待出餐',
            'shipping': '待收货',
            'verify_pending': '待核销',
            'verified': '已核销',
            'completed': '已完成',
            'refund': '待退款',
            'refunding': '待退款',
            'refunded': '已退款'
          };
          orderData.statusText = statusTextMap[orderData.status] || orderData.status;
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
        
        // 处理点餐订单桌号：兼容后端字段名
        orderData.diningTableNo = orderData.diningTableNo || orderData.tableNumber || orderData.tableNo || orderData.dining_table_no || '';
        
        // 标记订单类型
        orderData.isActivityOrder = orderData.type === 'activity';
        orderData.isFoodOrder = orderData.type === 'food';
        orderData.isGoodsOrder = orderData.type === 'goods';
        orderData.isCancelledOrder = orderData.status === 'cancelled';

        // 处理 verified 字段（活动订单核销状态）
        orderData.verified = orderData.verified || false;

        // 统一核销时间字段
        orderData.verifyTime = orderData.verifyTime || orderData.verifiedTime || orderData.verificationTime || orderData.verify_time || '';

        // 处理有效期字段（仅活动订单）
        if (orderData.validity) {
          orderData.validity.startTime = orderData.validity.startTime || '';
          orderData.validity.endTime = orderData.validity.endTime || '';
          orderData.validity.isValid = orderData.validity.isValid || false;
          orderData.validity.expired = orderData.validity.expired || false;
        }
        // duration 兜底
        orderData.duration = orderData.duration || 30;

        // 处理物流信息 - 兼容多种字段名
        
        if (!orderData.logistics || orderData.logistics.length === 0) {
          // 尝试从其他字段获取物流信息
          if (orderData.logisticsInfo) {
            orderData.logistics = Array.isArray(orderData.logisticsInfo) ? orderData.logisticsInfo : [orderData.logisticsInfo];
          } else if (orderData.logisticsTrace) {
            orderData.logistics = Array.isArray(orderData.logisticsTrace) ? orderData.logisticsTrace : [orderData.logisticsTrace];
          } else if (orderData.expressInfo) {
            orderData.logistics = Array.isArray(orderData.expressInfo) ? orderData.expressInfo : [orderData.expressInfo];
          }
        }
        
        // 确保 logistics 是数组，并标准化字段
        orderData.logistics = (orderData.logistics || []).map(item => ({
          desc: item.desc || item.description || item.content || item.detail || item.status || '物流状态更新',
          time: item.time || item.createTime || item.datetime || item.timestamp || item.date || ''
        }));
        
        
        // 提取物流公司和运单号
        orderData.logisticsCompany = orderData.logisticsCompany || orderData.expressCompany || orderData.courierCompany || '';
        orderData.trackingNumber = orderData.trackingNumber || orderData.expressNo || orderData.waybillNo || orderData.logisticsNo || '';

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

        // 如果订单处于退款状态但还没有 refundInfo，先初始化一个占位对象
        // 避免退款进度卡片因 refundInfo 为空而不显示
        if ((orderData.status === 'refund' || orderData.status === 'refunding' || orderData.status === 'refunded' || orderData.hasRefund) && !orderData.refundInfo) {
          orderData.refundInfo = {
            status: orderData.status === 'refunded' ? 'completed' : 'pending',
            statusText: orderData.status === 'refunded' ? '退款已完成' : '等待商家处理',
            reason: '',
            reasonText: '',
            refundAmount: orderData.totalPrice || 0,
            description: '',
            images: [],
            createTime: '',
            adminReply: ''
          };
        }

        // 先设置基本数据（包含物流信息）
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

        // 活动订单：待核销/已核销状态加载二维码
        if (orderData.isActivityOrder && (orderData.status === 'verify_pending' || orderData.status === 'verified')) {
          this.getActivityOrderQrcode(orderId, orderData);
        }

        // 加载退款信息：只在真正处于退款流程时查询退款进度
        // 但即使状态不是退款相关，也检查本地存储（防止刚退款后被后端数据覆盖）
        const refundStatuses = ['refund', 'refunding', 'refunded'];
        const hasLocalRefund = wx.getStorageSync('refundNo_' + (orderData.orderNumber || orderData.orderNo || orderId));
        if (refundStatuses.includes(orderData.status) || orderData.hasRefund || hasLocalRefund) {
          this._loadRefundInfo(orderId);
        }

        // 加载物流信息（商品订单且非取消状态）- 异步加载补充数据
        if (orderData.isGoodsOrder && !orderData.isCancelledOrder &&
            (orderData.status === 'paid' || orderData.status === 'shipping' || orderData.status === 'completed')) {
          // 物流接口 /api/logistics/{id} 需要内部数字 ID，传 orderData.id
          const logisticsId = orderData.id || orderId;
          this._loadLogisticsInfo(logisticsId);
        }
      })
      .catch((err) => {
        this.setData({ loading: false });
        wx.showToast({ title: '获取订单详情失败', icon: 'none' });
      })
      .finally(() => {
        if (showLoading) {
          wx.hideLoading();
        }
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
          orderData.qrcode = 'https://api.nengjifarm.com/api/file/image/farm_000000000007.jpg';
        }
        // 从核销接口提取核销时间
        const vt = qrcodeData.verifyTime || qrcodeData.verifiedTime || qrcodeData.verificationTime || qrcodeData.verify_time || '';
        if (vt) orderData.verifyTime = vt;
        this.setData({ order: orderData, loading: false });
      })
      .catch(() => {
        orderData.qrcode = 'https://api.nengjifarm.com/api/file/image/farm_000000000007.jpg';
        this.setData({ order: orderData, loading: false });
      });
  },

  payOrder() {
    const order = this.data.order;
    // 检查订单状态是否为待支付（支持 pending 和 pending_payment）
    const pendingStatuses = ['pending', 'pending_payment'];
    if (!pendingStatuses.includes(order.status)) {
      wx.showToast({ title: '订单状态异常，无法支付', icon: 'none' });
      return;
    }
    wx.navigateTo({
      url: `/user-pages/pay/pay?orderNo=${order.orderNumber || order.orderNo}&totalPrice=${order.totalPrice}&type=${order.type || 'goods'}`
    });
  },

  // 保存活动二维码到本地
  saveQrcode() {
    const { order } = this.data;
    if (!order.qrcode) {
      wx.showToast({ title: '暂无二维码', icon: 'none' });
      return;
    }
    wx.showLoading({ title: '保存中...' });
    wx.downloadFile({
      url: order.qrcode,
      success: (res) => {
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

  goToOrders() {
    wx.navigateTo({ url: '/user-pages/orders/orders' });
  },

  // 跳转到物流详情
  goToLogisticsDetail() {
    const { order } = this.data;
    if (!order || !order.id) return;

    // 没有物流信息时直接提示，不报错
    const trackingNumber = order.trackingNumber || order.expressNo || order.waybillNo || '';
    const logisticsCompany = order.logisticsCompany || order.expressCompany || '';
    if (!trackingNumber || !logisticsCompany) {
      wx.showToast({ title: '暂无物流信息', icon: 'none' });
      return;
    }

    // 从订单数据中获取物流信息
    const openId = wx.getStorageSync('openid') || wx.getStorageSync('openId') || '';
    const waybillId = trackingNumber;
    const deliveryId = order.logisticsCompanyCode || '';
    const transId = order.transactionId || '';
    const receiverPhone = (order.shippingAddress && order.shippingAddress.phone) || '';

    // 校验手机号是否有效，避免无效数据触发后端物流 API 报错
    const cleanedPhone = receiverPhone.replace(/[^0-9]/g, '');
    if (!cleanedPhone || cleanedPhone.length !== 11 || !cleanedPhone.startsWith('1')) {
      wx.showToast({ title: '暂无物流信息', icon: 'none' });
      return;
    }

    // 构造商品列表
    const goodsList = (order.items || []).map(item => {
      const entry = { goodsName: item.name || '稻田时光农场商品' };
      if (item.image && (item.image.startsWith('http://') || item.image.startsWith('https://'))) {
        entry.goodsImgUrl = item.image;
      }
      return entry;
    });

    if (!waybillId || !deliveryId) {
      wx.showToast({ title: '暂无物流信息', icon: 'none' });
      return;
    }

    wx.showLoading({ title: '加载物流中...' });

    // 调用后端接口获取 waybill_token
    api.logistics.getWaybillToken({
      orderId: order.orderNumber || order.orderNo || order.id,
      openId,
      waybillId,
      receiverPhone,
      deliveryId,
      transId,
      goodsList: goodsList.length > 0 ? goodsList : [{ goodsName: '稻田时光农场商品', goodsImgUrl: '' }]
    })
      .then(data => {
        wx.hideLoading();
        const waybillToken = data.waybillToken || data;
        if (waybillToken) {
          const logisticsPlugin = requirePlugin('logisticsPlugin');
          logisticsPlugin.openWaybillTracking({
            waybillToken,
            success() {
            },
            fail(err) {
              wx.showToast({ title: '暂无物流信息', icon: 'none' });
            }
          });
        } else {
          wx.showToast({ title: '暂无物流信息', icon: 'none' });
        }
      })
      .catch(err => {
        wx.hideLoading();
        wx.showToast({ title: '暂无物流信息', icon: 'none' });
      });
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
          api.order.updateStatus(this.data.order.id, 'cancelled')
            .then(() => {
              this.stopCountdown();
              this.stopGlobalTimer();
              this.setData({ countdownText: '', remainingTime: 0 });
              wx.showToast({ title: '订单已取消', icon: 'success' });
              this.getOrderDetail(this.data.order.id);
            })
            .catch(() => {
              wx.showToast({ title: '取消订单失败', icon: 'none' });
            });
        }
      }
    });
  },

  // 加载物流信息（仅获取公司和运单号用于页面展示）
  _loadLogisticsInfo(orderId) {
    api.logistics.getDetail(orderId)
      .then((logisticsData) => {
        if (logisticsData) {
          const updates = {};
          const company = logisticsData.companyName || logisticsData.expressCompany || logisticsData.logisticsCompany;
          if (company) updates['order.logisticsCompany'] = company;
          if (logisticsData.companyCode) updates['order.logisticsCompanyCode'] = logisticsData.companyCode;
          if (logisticsData.companyPhone) updates['order.logisticsCompanyPhone'] = logisticsData.companyPhone;
          const waybill = logisticsData.waybillNo || logisticsData.expressNo || logisticsData.trackingNumber;
          if (waybill) updates['order.trackingNumber'] = waybill;
          if (logisticsData.estimatedArrival) updates['order.estimatedArrival'] = logisticsData.estimatedArrival;
          if (logisticsData.shipTime) updates['order.shipTime'] = logisticsData.shipTime;
          if (Object.keys(updates).length > 0) this.setData(updates);
        }
      })
      .catch(() => {});
  },

  // 加载退款信息
  _loadRefundInfo(orderId) {
    const reasonMap = {
      // 商品订单
      wrong_item: '收到的商品与描述不符',
      damaged: '商品损坏/腐烂',
      not_as_expected: '不想要了',
      delayed_delivery: '长时间未发货',
      // 点餐订单
      wrong_dish: '菜品与点单不符',
      poor_quality: '菜品质量不佳',
      delayed_service: '出餐速度慢',
      // 活动订单
      activity_changed: '活动内容变更',
      schedule_conflict: '时间安排冲突',
      // 通用
      duplicate_order: '重复下单',
      other: '其他原因'
    };
    const statusMap = {
      pending: '等待商家处理',
      approved: '商家已同意，等待退款',
      rejected: '商家已拒绝',
      processing: '退款处理中',
      completed: '退款已完成',
      failed: '退款失败',
      cancelled: '已取消'
    };

    // 先从本地存储尝试获取 refundId（先用 orderId 本身试，再试 order.orderNumber）
    let refundId = wx.getStorageSync('refundNo_' + orderId);
    if (!refundId && this.data.order && this.data.order.orderNumber && this.data.order.orderNumber != orderId) {
      refundId = wx.getStorageSync('refundNo_' + this.data.order.orderNumber);
    }
    if (!refundId && this.data.order && this.data.order.orderNo && this.data.order.orderNo != orderId) {
      refundId = wx.getStorageSync('refundNo_' + this.data.order.orderNo);
    }
    if (refundId) {
      this._fetchRefundDetail(refundId);
      return;
    }
    // 如果本地无记录，从用户退款列表查询匹配本订单的记录
    api.refund.getList({ page: 1, pageSize: 50 })
      .then((data) => {
        const items = data && data.items ? data.items : (Array.isArray(data) ? data : []);
        // 同时匹配 orderNo 和 orderId 字段
        const match = items.find(r => r.orderNo == orderId || r.orderId == orderId);
        if (match) {
          wx.setStorageSync('refundNo_' + orderId, match.refundId);
          this._fetchRefundDetail(match.refundId);
        }
      })
      .catch((err) => {
      });
  },

  // 退款原因code → 中文映射
  _getRefundReasonText(reasonCode) {
    const reasonMap = {
      wrong_item: '收到的商品与描述不符',
      damaged: '商品损坏/腐烂',
      not_as_expected: '不想要了',
      delayed_delivery: '长时间未发货',
      wrong_dish: '菜品与点单不符',
      poor_quality: '菜品质量不佳',
      delayed_service: '出餐速度慢',
      activity_changed: '活动内容变更',
      schedule_conflict: '时间安排冲突',
      duplicate_order: '重复下单',
      other: '其他原因'
    };
    return reasonMap[reasonCode] || reasonCode || '';
  },

  // 根据 refundId 获取退款详情
  _fetchRefundDetail(refundId) {
    const statusMap = {
      pending: '等待商家处理',
      cancelled: '已取消',
      '已退款': '退款已完成'
    };
    api.refund.getDetail({ refundId })
      .then((refundData) => {
        if (refundData) {
          // 根据退款状态覆写订单状态，使界面展示退款中/已退款
          const isRefundCompleted = refundData.status === 'completed' || refundData.status === '已退款';
          this.setData({
            'order.hasRefund': true,
            'order.status': isRefundCompleted ? 'refunded' : 'refunding',
            'order.statusText': isRefundCompleted ? '已退款' : '退款中',
            'order.refundInfo': {
              refundId: refundData.refundId,
              status: refundData.status,
              statusClass: refundData.status === '已退款' ? 'refunded' : refundData.status,
              reason: refundData.reason,
              reasonText: this._getRefundReasonText(refundData.reason),
              description: refundData.description,
              images: refundData.images || [],
              refundAmount: refundData.refundAmount,
              createTime: refundData.createTime,
              processTime: refundData.processTime,
              adminReply: refundData.adminReply,
              statusText: statusMap[refundData.status] || refundData.statusText || refundData.status
            }
          });
        }
      })
      .catch((err) => {
      });
  },

  confirmReceipt() {
    wx.showModal({
      title: '确认收货',
      content: '确定已收到商品吗？',
      success: (res) => {
        if (res.confirm) {
          api.order.updateStatus(this.data.order.id, 'completed')
            .then(() => {
              wx.showToast({ title: '收货成功', icon: 'success' });
              this.getOrderDetail(this.data.order.id);
            })
            .catch(() => {
              wx.showToast({ title: '确认收货失败', icon: 'none' });
            });
        }
      }
    });
  },

  // 申请退款
  applyRefund() {
    const { order } = this.data;
    const orderNo = order.orderNumber || order.orderNo || order.id || '';

    if (order.hasRefund) return;

    // 根据订单类型展示不同的退款原因
    const reasons = this._getRefundReasonsByType(order.type);

    // 使用 actionSheet 让用户选择退款原因
    wx.showActionSheet({
      itemList: reasons.map(r => r.label),
      success: (res) => {
        const selectedReason = reasons[res.tapIndex];
        this._submitRefund(orderNo, order.type, selectedReason.value, selectedReason.label);
      }
    });
  },

  // 根据订单类型获取退款原因列表
  _getRefundReasonsByType(type) {
    const goodsReasons = [
      { value: 'wrong_item', label: '收到的商品与描述不符' },
      { value: 'damaged', label: '商品损坏/腐烂' },
      { value: 'not_as_expected', label: '不想要了' },
      { value: 'delayed_delivery', label: '长时间未发货' },
      { value: 'duplicate_order', label: '重复下单' },
      { value: 'other', label: '其他原因' }
    ];
    const foodReasons = [
      { value: 'wrong_dish', label: '菜品与点单不符' },
      { value: 'poor_quality', label: '菜品质量不佳' },
      { value: 'delayed_service', label: '出餐速度慢' },
      { value: 'duplicate_order', label: '重复下单' },
      { value: 'other', label: '其他原因' }
    ];
    const activityReasons = [
      { value: 'activity_changed', label: '活动内容变更' },
      { value: 'schedule_conflict', label: '时间安排冲突' },
      { value: 'duplicate_order', label: '重复下单' },
      { value: 'other', label: '其他原因' }
    ];

    if (type === 'food') return foodReasons;
    if (type === 'activity') return activityReasons;
    return goodsReasons; // 默认为商品订单
  },

  // 内部方法：提交退款申请
  _submitRefund(orderNo, orderType, reason, reasonLabel) {
    wx.showModal({
      title: `退款原因：${reasonLabel}`,
      content: '如有补充说明请在下方填写（选填）',
      editable: true,
      placeholderText: '补充说明（选填，最多200字）',
      success: (res) => {
        if (!res.confirm) return;

        const description = (res.content || '').trim().substring(0, 200);

        wx.showLoading({ title: '提交中...' });
        api.refund.apply({
          orderNo,
          orderType,
          reason,
          reasonText: reasonLabel,
          description
        })
          .then((refundData) => {
            wx.hideLoading();
            wx.showToast({ title: '退款申请已提交', icon: 'success' });

            // 保存 refundId 到本地存储（同时用订单号和订单编号作为key，确保详情页能查到）
            wx.setStorageSync('refundNo_' + orderNo, refundData.refundId);
            if (this.data.order.orderNumber || this.data.order.orderNo) {
              wx.setStorageSync('refundNo_' + (this.data.order.orderNumber || this.data.order.orderNo), refundData.refundId);
            }

            // 更新订单数据中的退款信息
            this.setData({
              'order.hasRefund': true,
              'order.status': 'refunding',
              'order.statusText': '退款中',
              'order.refundInfo': {
                refundId: refundData.refundId,
                status: 'pending',
                reason: refundData.reason,
                reasonText: reasonLabel,
                description: refundData.description,
                refundAmount: refundData.refundAmount,
                createTime: refundData.createTime,
                statusText: '等待商家处理'
              }
            });

            // 刷新订单详情以同步后端最新状态
            this.getOrderDetail(orderNo);
          })
          .catch((err) => {
            wx.hideLoading();
            const msg = err && err.message ? err.message : '提交失败，请重试';
            wx.showToast({ title: msg, icon: 'none' });
          });
      }
    });
  },

});

