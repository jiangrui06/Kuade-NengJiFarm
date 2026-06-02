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
    remainingTime: 0,
    showOtherReasonInput: false,
    otherReasonText: ''
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
            subtotal: subtotal,
            isFarmGood: item.isFarmGood || item.is_farm_good || false
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
        // 到店自取标记：商品订单且配送方式为 pickup
        orderData.isPickupOrder = orderData.isGoodsOrder && (orderData.deliveryMethod === 'pickup' || orderData.deliveryMethod === 'self_pickup');
        // 兼容字段名：某些接口返回 delivery_type 或 shipping_method
        if (!orderData.deliveryMethod && orderData.delivery_type === 'pickup') {
          orderData.isPickupOrder = true;
          orderData.deliveryMethod = 'pickup';
        }

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

        // 统一积分字段名（兼容后端可能下发的 earnedPoints/pointsEarned）
        if (orderData.pointsEarned && !orderData.earnedPoints) {
          orderData.earnedPoints = orderData.pointsEarned;
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

        // 活动订单或自取订单：待核销/已核销状态加载二维码
        const needsQrcode = (orderData.isActivityOrder || orderData.isPickupOrder) &&
          (orderData.status === 'verify_pending' || orderData.status === 'verified' || orderData.status === 'paid');
        if (needsQrcode) {
          this.getOrderQrcode(orderId, orderData);
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

        // 加载该订单的积分信息（已完成/已退款等已完成流转的订单）
        const orderNo = orderData.orderNumber || orderData.orderNo || orderId;
        this._loadPointsInfo(orderNo);
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

  getOrderQrcode(orderId, orderData) {
    // 自取订单优先使用 mock 二维码（订单号编码）
    if (orderData.isPickupOrder) {
      // 使用订单号生成二维码内容，实际项目由后端生成
      const qrContent = orderData.orderNumber || orderData.id || orderId;
      orderData.qrcode = 'https://api.nengjifarm.com/api/file/image/farm_000000000007.jpg';
      orderData.verifyCode = qrContent;
      // 异步尝试从后端获取真实二维码
      api.order.getQrcode(orderId)
        .then((qrcodeData) => {
          if (qrcodeData && qrcodeData.qrCodeUrl) {
            orderData.qrcode = qrcodeData.qrCodeUrl.startsWith('http')
              ? qrcodeData.qrCodeUrl
              : 'https://api.nengjifarm.com' + qrcodeData.qrCodeUrl;
            orderData.verifyCode = qrcodeData.verifyCode || qrContent;
          }
          const vt = qrcodeData.verifyTime || qrcodeData.verifiedTime || qrcodeData.verificationTime || qrcodeData.verify_time || '';
          if (vt) orderData.verifyTime = vt;
          this.setData({ order: orderData, loading: false });
        })
        .catch(() => {
          this.setData({ order: orderData, loading: false });
        });
    } else {
      // 活动订单：从后端获取核销码
      api.order.getQrcode(orderId)
        .then((qrcodeData) => {
          if (qrcodeData && qrcodeData.qrCodeUrl) {
            orderData.qrcode = qrcodeData.qrCodeUrl.startsWith('http')
              ? qrcodeData.qrCodeUrl
              : 'https://api.nengjifarm.com' + qrcodeData.qrCodeUrl;
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
    }
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
          api.order.cancel(this.data.order.id)
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

  // 加载物流信息（详情 + 轨迹）
  _loadLogisticsInfo(orderId) {
    const self = this;
    // 并行请求物流详情和轨迹
    const detailPromise = api.logistics.getDetail(orderId).catch(() => null);
    const tracePromise = api.logistics.getTrace(orderId).catch(() => null);
    Promise.all([detailPromise, tracePromise])
      .then(([logisticsData, traceData]) => {
        const updates = {};
        if (logisticsData) {
          const company = logisticsData.companyName || logisticsData.expressCompany || logisticsData.logisticsCompany;
          if (company) updates['order.logisticsCompany'] = company;
          if (logisticsData.companyCode) updates['order.logisticsCompanyCode'] = logisticsData.companyCode;
          if (logisticsData.companyPhone) updates['order.logisticsCompanyPhone'] = logisticsData.companyPhone;
          const waybill = logisticsData.waybillNo || logisticsData.expressNo || logisticsData.trackingNumber;
          if (waybill) updates['order.trackingNumber'] = waybill;
          if (logisticsData.estimatedArrival) updates['order.estimatedArrival'] = logisticsData.estimatedArrival;
          if (logisticsData.shipTime) updates['order.shipTime'] = logisticsData.shipTime;
          if (logisticsData.status) updates['order.logisticsStatus'] = logisticsData.status;
          if (logisticsData.statusText) updates['order.logisticsStatusText'] = logisticsData.statusText;
        }
        // 轨迹按时间降序排列（最新在前）
        if (traceData && Array.isArray(traceData) && traceData.length > 0) {
          const sorted = [...traceData].sort((a, b) => (b.time || '').localeCompare(a.time || ''));
          updates['order.logisticsTrace'] = sorted;
        }
        if (Object.keys(updates).length > 0) self.setData(updates);
      })
      .catch(() => {});
  },

  // 加载该订单的积分信息
  _loadPointsInfo(orderNo) {
    if (!orderNo) return;
    const order = this.data.order;

    // 如果订单API已返回积分字段，直接显示，不再额外请求
    if (order.earnedPoints > 0 || order.spentPoints > 0) return;

    // 仅对已完成流转的订单查询积分
    const relevantStatuses = ['completed', 'verified', 'refunded', 'refunding', 'shipping'];
    if (!relevantStatuses.includes(order.status) && !order.hasRefund) return;

    // 从积分流水查询该订单的记录（不传 order_no 参数，后端可能不支持筛选）
    api.points.records({ page: 1, pageSize: 50 })
      .then(data => {
        const list = data && data.list ? data.list : (Array.isArray(data) ? data : []);
        const match = list.filter(r => {
          const rNo = String(r.order_no || r.orderNo || r.orderNumber || '');
          return rNo === orderNo;
        });
        if (match.length === 0) return;

        const earned = match
          .filter(r => r.type === 'earn')
          .reduce((sum, r) => sum + parseInt(r.points || 0), 0);
        const spent = match
          .filter(r => r.type === 'spend')
          .reduce((sum, r) => sum + parseInt(r.points || 0), 0);

        if (earned > 0 || spent > 0) {
          this.setData({
            'order.earnedPoints': earned,
            'order.spentPoints': spent
          });
        }
      })
      .catch(() => {});
  },

  // 加载退款信息
  _loadRefundInfo(orderId) {
    const statusMap = {
      pending: '等待商家处理',
      approved: '商家已同意，等待退款',
      rejected: '退款已驳回',
      processing: '退款处理中',
      completed: '退款已完成',
      failed: '退款失败',
      cancelled: '已取消'
    };

    // 直接从后端查询退款详情（使用订单号）
    api.refund.getDetail(orderId)
      .then((refundData) => {
        if (refundData) {
          this._applyRefundData(refundData, statusMap);
        }
      })
      .catch(() => {
        // 无退款记录时（data=null），尝试从退款列表查询
        api.refund.getList({ page: 1, pageSize: 50 })
          .then((data) => {
            const list = data && data.list ? data.list : (Array.isArray(data) ? data : []);
            const match = list.find(r => r.orderNo == orderId || r.orderId == orderId);
            if (match) {
              wx.setStorageSync('refundNo_' + orderId, match.refundId);
              this._applyRefundData(match, statusMap);
            }
          })
          .catch(() => {});
      });
  },

  // 将退款数据应用到订单状态
  _applyRefundData(refundData, statusMap) {
    if (!refundData) return;

    // 退款被驳回：清除本地退款标记，让申请退款按钮重新显示；保留退款信息卡片
    if (refundData.status === 'rejected') {
      // 清除本地存储的退款标记，避免脏数据覆盖后端状态
      const order = this.data.order;
      const cleanKeys = new Set([order.id, order.orderNumber, order.orderNo].filter(Boolean));
      cleanKeys.forEach(k => wx.removeStorageSync('refundNo_' + k));
      this.setData({
        'order.hasRefund': false,
        'order.refundInfo': {
          refundId: refundData.refundId,
          status: 'rejected',
          statusClass: 'rejected',
          reason: refundData.reason,
          reasonText: this._getRefundReasonText(refundData.reason),
          description: refundData.description,
          images: refundData.images || [],
          refundAmount: refundData.refundAmount,
          createTime: refundData.createTime,
          processTime: refundData.processTime,
          processNote: refundData.processNote,
          adminReply: refundData.adminReply,
          statusText: '退款已驳回'
        }
      });
      return;
    }

    const isRefundCompleted = refundData.status === 'completed' || refundData.status === '已退款';
    this.setData({
      'order.hasRefund': true,
      'order.status': isRefundCompleted ? 'refunded' : 'refunding',
      'order.statusText': isRefundCompleted ? '已退款' : '退款中',
      'order.refundInfo': {
        refundId: refundData.refundId,
        status: refundData.status,
        statusClass: refundData.status,
        reason: refundData.reason,
        reasonText: this._getRefundReasonText(refundData.reason),
        description: refundData.description,
        images: refundData.images || [],
        refundAmount: refundData.refundAmount,
        createTime: refundData.createTime,
        processTime: refundData.processTime,
        processNote: refundData.processNote,
        adminReply: refundData.adminReply,
        statusText: statusMap[refundData.status] || refundData.statusText || refundData.status
      }
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

  confirmReceipt() {
    wx.showModal({
      title: '确认收货',
      content: '确定已收到商品吗？',
      success: (res) => {
        if (res.confirm) {
          const orderNo = this.data.order.orderNumber || this.data.order.orderNo || this.data.order.id;
          api.order.confirmReceipt(orderNo)
            .then((data) => {
              wx.showToast({ title: '收货成功', icon: 'success' });
              // 用后端返回的数据更新订单状态
              if (data) {
                const updates = {};
                if (data.status) updates['order.status'] = data.status;
                if (data.statusText) updates['order.statusText'] = data.statusText;
                if (data.completeTime) updates['order.completeTime'] = data.completeTime;
                if (Object.keys(updates).length > 0) this.setData(updates);
              }
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

    // 积分提醒：获得积分的订单退款后会扣回，先弹提示再让用户选原因
    const showRefundReasons = () => {
      // 根据订单类型展示不同的退款原因
      const reasons = this._getRefundReasonsByType(order.type);

      // 使用 actionSheet 让用户选择退款原因
      wx.showActionSheet({
        itemList: reasons.map(r => r.label),
        success: (res) => {
          const selectedReason = reasons[res.tapIndex];

          if (selectedReason.label === '其他原因') {
            // 其他原因：弹出自定义输入框（多行文本）
            this.setData({
              showOtherReasonInput: true,
              otherReasonText: ''
            });
            this._pendingRefundData = { orderNo, orderType: order.type, reason: '其他原因', reasonLabel: '其他原因' };
          } else {
            this._doSubmitRefund(orderNo, selectedReason.label, '', [], selectedReason.label);
          }
        }
      });
    };

    if (order.earnedPoints > 0) {
      wx.showModal({
        title: '退款提示',
        content: `该订单获得 ${order.earnedPoints} 积分，退款后所得积分将自动被扣回。`,
        success: (res) => {
          if (res.confirm) showRefundReasons();
        }
      });
    } else {
      showRefundReasons();
    }
  },

  // 外部输入框：监听输入
  onOtherReasonInput(e) {
    this.setData({ otherReasonText: e.detail.value });
  },

  // 外部输入框：确认提交
  confirmOtherReason() {
    const description = (this.data.otherReasonText || '').trim();
    if (!description) {
      wx.showToast({ title: '请填写退款原因', icon: 'none' });
      return;
    }

    const data = this._pendingRefundData;
    if (!data) return;

    this.setData({ showOtherReasonInput: false });
    this._doSubmitRefund(data.orderNo, data.reason, description, [], data.reasonLabel);
  },

  // 遮罩层点击：仅点击遮罩本身时关闭
  onOverlayTap(e) {
    if (e.target === e.currentTarget) {
      this.cancelOtherReason();
    }
  },

  // 外部输入框：取消
  cancelOtherReason() {
    this.setData({ showOtherReasonInput: false, otherReasonText: '' });
    this._pendingRefundData = null;
  },

  // 根据订单类型获取退款原因列表（枚举值必须与API文档一致）
  _getRefundReasonsByType(type) {
    const common = [
      { label: '商品/菜品与描述不符' },
      { label: '商品破损/质量问题' },
      { label: '与预期不符' },
      { label: '配送延迟' },
      { label: '重复下单' },
      { label: '其他原因' }
    ];
    return common;
  },

  _doSubmitRefund(orderNo, reason, description, images, reasonLabel) {
    wx.showLoading({ title: '提交中...' });
    api.refund.apply(orderNo, {
      reason,
      description,
      images
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
            images: refundData.images || images,
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
  },

  // 取消退款申请
  // 预览退款凭证图片
  previewRefundImage(e) {
    const url = e.currentTarget.dataset.url;
    const images = this.data.order.refundInfo.images || [];
    wx.previewImage({
      current: url,
      urls: images
    });
  },

});

