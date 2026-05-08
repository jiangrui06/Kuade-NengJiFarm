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
      diningTableNo: '',
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

  processImageUrl: function (imageUrl) {
    const utils = require('../../utils/utils');
    return utils.media.processUrl(imageUrl);
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

        // 确保订单编号存在（用于显示）
        if (!orderData.orderNumber) {
          orderData.orderNumber = orderData.id;
        }
        
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
        
        // 处理点餐订单桌号：兼容后端字段名
        orderData.diningTableNo = orderData.diningTableNo || orderData.tableNumber || orderData.tableNo || orderData.dining_table_no || '';
        
        // 标记订单类型
        orderData.isActivityOrder = orderData.type === 'activity';
        orderData.isAcreOrder = orderData.type === 'acre';
        orderData.isFoodOrder = orderData.type === 'food';
        orderData.isGoodsOrder = orderData.type === 'goods';
        orderData.isCancelledOrder = orderData.status === 'cancelled';

        // 处理 verified 字段（活动订单核销状态）
        orderData.verified = orderData.verified || false;

        // 处理有效期字段（仅活动订单）
        if (orderData.validity) {
          orderData.validity.startTime = orderData.validity.startTime || '';
          orderData.validity.endTime = orderData.validity.endTime || '';
          orderData.validity.isValid = orderData.validity.isValid || false;
          orderData.validity.expired = orderData.validity.expired || false;
        }

        // 处理物流信息 - 兼容多种字段名
        console.log('原始物流数据:', {
          logistics: orderData.logistics,
          logisticsInfo: orderData.logisticsInfo,
          logisticsTrace: orderData.logisticsTrace,
          expressInfo: orderData.expressInfo,
          logisticsCompany: orderData.logisticsCompany,
          expressCompany: orderData.expressCompany,
          trackingNumber: orderData.trackingNumber,
          expressNo: orderData.expressNo
        });
        
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
        
        console.log('标准化后的物流数据:', orderData.logistics);
        
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

        // 加载退款信息（商品: paid/shipping, 点餐: ordered, 活动: verify_pending）
        const refundableStatuses = ['paid', 'shipping', 'ordered', 'verify_pending'];
        if (refundableStatuses.includes(orderData.status)) {
          this._loadRefundInfo(orderId);
        }

        // 加载物流信息（商品订单且非取消状态）- 异步加载补充数据
        if (orderData.isGoodsOrder && !orderData.isCancelledOrder &&
            (orderData.status === 'paid' || orderData.status === 'shipping' || orderData.status === 'completed')) {
          // 始终加载物流轨迹，确保显示最新数据
          this._loadLogisticsInfo(orderId);
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
    const order = this.data.order;
    wx.navigateTo({
      url: `/user-pages/pay/pay?orderId=${order.id}&type=${order.type || 'goods'}`
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

  // 模拟发货
  markShipping() {
    const { order } = this.data;
    wx.showModal({
      title: '确认发货',
      content: `确定要标记此订单为已发货吗？\n收货地址：${order.shippingAddress ? order.shippingAddress.name + ' ' + order.shippingAddress.phone + ' ' + order.shippingAddress.address : '未设置'}`,
      success: (res) => {
        if (res.confirm) {
          wx.showLoading({ title: '发货中...' });
          api.order.updateStatus(order.id, 'shipped')
            .then(() => {
              wx.hideLoading();
              wx.showToast({ title: '发货成功', icon: 'success' });
              this.getOrderDetail(order.id);
            })
            .catch((err) => {
              wx.hideLoading();
              wx.showToast({ title: err.message || '发货失败', icon: 'none' });
            });
        }
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
    wx.navigateTo({
      url: `/user-pages/logistics-detail/logistics-detail?orderId=${order.id}`
    });
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
          api.order.delete(order.id)
            .then(() => {
              wx.showToast({ title: '订单已删除', icon: 'success' });
              setTimeout(() => { wx.navigateBack(); }, 1500);
            })
            .catch(() => {
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

  // 加载物流信息
  _loadLogisticsInfo(orderId) {
    console.log('开始加载物流信息，订单ID:', orderId);
    
    // 获取物流详情（公司、运单号等）
    api.logistics.getDetail(orderId)
      .then((logisticsData) => {
        console.log('物流详情数据:', logisticsData);
        if (logisticsData) {
          const updates = {};
          
          // 更新物流公司和运单号 - 兼容多种字段名
          const company = logisticsData.companyName || logisticsData.courierCompany || logisticsData.expressCompany || logisticsData.logisticsCompany;
          if (company) {
            updates['order.logisticsCompany'] = company;
          }
          
          const waybill = logisticsData.waybillNo || logisticsData.expressNo || logisticsData.trackingNumber || logisticsData.logisticsNo || logisticsData.waybillNumber;
          if (waybill) {
            updates['order.trackingNumber'] = waybill;
          }
          
          // 更新物流状态
          if (logisticsData.status) {
            updates['order.logisticsStatus'] = logisticsData.status;
          }
          
          console.log('物流详情更新:', updates);
          this.setData(updates);
        }
      })
      .catch((err) => {
        console.error('获取物流详情失败:', err);
      });

    // 获取物流轨迹
    api.logistics.getTrace(orderId)
      .then((traceData) => {
        console.log('物流轨迹数据:', traceData);
        let trace = [];
        
        // 解析多种返回格式
        if (Array.isArray(traceData)) {
          trace = traceData;
        } else if (traceData && Array.isArray(traceData.trace)) {
          trace = traceData.trace;
        } else if (traceData && Array.isArray(traceData.logistics)) {
          trace = traceData.logistics;
        } else if (traceData && Array.isArray(traceData.traces)) {
          trace = traceData.traces;
        } else if (traceData && Array.isArray(traceData.list)) {
          trace = traceData.list;
        } else if (traceData && traceData.data && Array.isArray(traceData.data)) {
          trace = traceData.data;
        }
        
        // 标准化物流轨迹数据格式
        trace = trace.map(item => ({
          desc: item.desc || item.description || item.content || item.detail || item.status || '物流状态更新',
          time: item.time || item.createTime || item.datetime || item.timestamp || item.date || ''
        }));
        
        console.log('解析后的物流轨迹:', trace);
        
        if (trace.length > 0) {
          this.setData({
            'order.logistics': trace
          });
        }
      })
      .catch((err) => {
        console.error('获取物流轨迹失败:', err);
      });
  },

  // 加载退款信息
  _loadRefundInfo(orderId) {
    const reasonMap = {
      wrong_item: '收到的商品与描述不符',
      damaged: '商品损坏/腐烂',
      not_as_expected: '不想要了',
      delayed_delivery: '长时间未发货',
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

    api.refund.getDetail(orderId)
      .then((refundData) => {
        if (refundData) {
          this.setData({
            'order.hasRefund': true,
            'order.refundInfo': {
              refundId: refundData.refundId,
              status: refundData.status,
              reason: refundData.reason,
              reasonText: reasonMap[refundData.reason] || refundData.reason,
              description: refundData.description,
              images: refundData.images || [],
              refundAmount: refundData.refundAmount,
              createTime: refundData.createTime,
              processTime: refundData.processTime,
              adminReply: refundData.adminReply,
              statusText: statusMap[refundData.status] || refundData.status
            }
          });
        }
      })
      .catch((err) => {
        console.warn('加载退款信息失败:', err);
        // 无退款记录或加载失败，不显示提示避免干扰用户
        // 如果需要提示，可以取消注释下面的代码
        // wx.showToast({ title: '退款信息加载失败', icon: 'none' });
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
    const orderId = order.id;

    // 如果已有退款申请，提示并先查询详情
    if (order.hasRefund) {
      this._showRefundDetail();
      return;
    }

    // 退款原因选项
    const reasons = [
      { value: 'wrong_item', label: '收到的商品与描述不符' },
      { value: 'damaged', label: '商品损坏/腐烂' },
      { value: 'not_as_expected', label: '不想要了' },
      { value: 'delayed_delivery', label: '长时间未发货' },
      { value: 'duplicate_order', label: '重复下单' },
      { value: 'other', label: '其他原因' }
    ];

    // 使用 actionSheet 让用户选择退款原因
    wx.showActionSheet({
      itemList: reasons.map(r => r.label),
      success: (res) => {
        const selectedReason = reasons[res.tapIndex];
        this._submitRefund(orderId, selectedReason.value, selectedReason.label);
      }
    });
  },

  // 内部方法：提交退款申请
  _submitRefund(orderId, reason, reasonLabel) {
    wx.showModal({
      title: `退款原因：${reasonLabel}`,
      content: '如有补充说明请在下方填写（选填）',
      editable: true,
      placeholderText: '补充说明（选填，最多200字）',
      success: (res) => {
        if (!res.confirm) return;

        const description = (res.content || '').trim().substring(0, 200);

        wx.showLoading({ title: '提交中...' });
        api.refund.apply(orderId, {
          reason,
          description
        })
          .then((refundData) => {
            wx.hideLoading();
            wx.showToast({ title: '退款申请已提交', icon: 'success' });

            // 更新订单数据中的退款信息
            this.setData({
              'order.hasRefund': true,
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
          })
          .catch((err) => {
            wx.hideLoading();
            const msg = err && err.message ? err.message : '提交失败，请重试';
            wx.showToast({ title: msg, icon: 'none' });
          });
      }
    });
  },

  // 内部方法：查看退款详情
  _showRefundDetail() {
    const { order } = this.data;
    const refundInfo = order.refundInfo;

    if (!refundInfo) return;

    const statusText = {
      pending: '等待商家处理',
      approved: '商家已同意，等待退款',
      rejected: '商家已拒绝',
      processing: '退款处理中',
      completed: '退款已完成',
      failed: '退款失败',
      cancelled: '已取消'
    };

    const msg = `退款金额：¥${refundInfo.refundAmount || 0}\n` +
      `退款原因：${refundInfo.reason || '-'}\n` +
      `当前状态：${statusText[refundInfo.status] || refundInfo.status}`;

    wx.showModal({
      title: '退款详情',
      content: msg,
      showCancel: refundInfo.status === 'pending',
      cancelText: '取消退款',
      confirmText: '知道了',
      success: (res) => {
        if (res.confirm) return;
        // 用户取消退款申请
        this._cancelRefund(order.id);
      }
    });
  },

  // 取消退款申请
  _cancelRefund(orderId) {
    wx.showModal({
      title: '确认取消',
      content: '确定要取消退款申请吗？',
      success: (res) => {
        if (!res.confirm) return;

        wx.showLoading({ title: '取消中...' });
        api.refund.cancel(orderId)
          .then(() => {
            wx.hideLoading();
            wx.showToast({ title: '已取消退款申请', icon: 'success' });
            this.setData({
              'order.hasRefund': false,
              'order.refundInfo': null
            });
            this.getOrderDetail(orderId);
          })
          .catch(() => {
            wx.hideLoading();
            wx.showToast({ title: '取消失败，请重试', icon: 'none' });
          });
      }
    });
  }
});

