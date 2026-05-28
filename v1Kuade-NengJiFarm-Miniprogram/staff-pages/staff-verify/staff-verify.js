const api = require('../../utils/api');

Page({
  data: {
    scanned: false,
    verifying: false,
    showResult: false,
    resultCode: '',     // 'success' | 'fail' | 'info' | 'confirm'
    resultTitle: '',
    resultMsg: '',
    voucherInfo: null,
    pendingConfirm: null,   // 待确认核销的数据
    historyList: [],
    showHistoryDetail: false,
    historyDetail: null
  },

  onLoad() {
    this.verifyPermission();
  },

  verifyPermission() {
    api.api.staff.verifyPermission()
      .then(data => {
        if (!data.hasPermission) {
          wx.showModal({
            title: '无权限访问',
            content: '仅员工账号可使用核销功能',
            showCancel: false,
            success: () => { wx.navigateBack(); }
          });
          return;
        }
        this.loadHistory();
      })
      .catch(() => {
        wx.showModal({
          title: '权限验证失败',
          content: '无法验证员工权限，请稍后重试',
          showCancel: false,
          success: () => { wx.navigateBack(); }
        });
      });
  },

  onShow() {
    this.loadHistory();
  },

  /**
   * 第一步：扫码
   */
  scanCode() {
    wx.scanCode({
      scanType: ['qrCode'],
      success: (res) => {
        const code = (res.result || '').trim();
        if (!code) {
          wx.showToast({ title: '无效的二维码', icon: 'none' });
          return;
        }
        this.queryVoucherInfo(code);
      },
      fail: (err) => {
        if (!err.errMsg.includes('cancel')) {
          wx.showToast({ title: '扫码失败', icon: 'none' });
        }
      }
    });
  },

  /**
   * 第二步：查询券信息（只读，不改状态）
   */
  queryVoucherInfo(code) {
    if (this.data.verifying) return;
    this.setData({ verifying: true });

    api.api.staff.getVoucherInfo(code)
      .then(data => this._handleVoucherInfo(data, code))
      .catch(err => {
        // 404 → voucher-info 不识别 EXC_ 码等场景，直接调统一核销接口
        if (err && (err.code === 404 || err.statusCode === 404)) {
          this._directVerifyVoucher(code);
        } else {
          this._handleVerifyError(err);
        }
      });
  },

  /**
   * 兜底：getVoucherInfo 不支持的码（如 EXC_），直接调统一核销接口
   */
  _directVerifyVoucher(code) {
    api.api.staff.verifyVoucher(code)
      .then(data => this._handleVerifySuccess(data))
      .catch(err => this._handleVerifyError(err));
  },

  /**
   * 处理券信息查询结果
   */
  _handleVoucherInfo(data, code) {
    // 积分兑换：直接调统一核销接口
    if (data.type === 'exchange') {
      this._directVerifyVoucher(code);
      return;
    }

    const isGoodsPickup = data.type === 'goods_pickup';
    const isActivity = data.type === 'activity' || data.type === 'pick';

    // 构建基础 voucherInfo
    const buildVoucherInfo = (overrides = {}) => ({
      typeName: data.typeName || (isGoodsPickup ? '商品自取' : '活动券'),
      userName: data.userName || '未知用户',
      userPhone: data.userPhone || '',
      content: data.content || data.title || '',
      orderNo: data.orderNo || '',
      participantCount: data.participantCount || 1,
      showParticipants: isActivity,
      totalAmount: data.totalAmount || '',
      items: this._mapItems(data.items), // 优先使用接口返回的 items
      ...overrides
    });

    // 可核销 → 展示信息，让员工确认
    if (data.canVerify && !data.verified) {
      this.setData({
        verifying: false,
        showResult: true,
        resultCode: 'confirm',
        resultTitle: '确认核销',
        resultMsg: '',
        voucherInfo: buildVoucherInfo(),
        pendingConfirm: code
      });

      // 接口未返回 items 时，兜底异步加载
      if (isGoodsPickup && data.orderNo && !(data.items && data.items.length > 0)) {
        this._loadOrderItems(data.orderNo);
      }
      wx.vibrateShort({ type: 'light' });
      return;
    }

    // 已核销 / 不可核销
    const isAlreadyVerified = data.verified || data.alreadyVerified;

    this.setData({
      verifying: false,
      showResult: true,
      resultCode: 'info',
      resultTitle: isAlreadyVerified ? (isGoodsPickup ? '已取货' : '已核销') : '不可核销',
      resultMsg: data.message || '该订单已完成核销，无需重复操作',
      voucherInfo: buildVoucherInfo(),
      pendingConfirm: null
    });

    // 接口未返回 items 时，兜底异步加载
    if (isGoodsPickup && data.orderNo && !(data.items && data.items.length > 0)) {
      this._loadOrderItems(data.orderNo);
    }
    wx.vibrateShort({ type: 'light' });
  },

  /**
   * 第三步：确认核销
   */
  confirmVerify() {
    const code = this.data.pendingConfirm;
    if (!code) return;

    this.setData({
      verifying: true,
      showResult: false,
      pendingConfirm: null
    });

    api.api.staff.verifyVoucher(code)
      .then(data => this._handleVerifySuccess(data))
      .catch(err => this._handleVerifyError(err));
  },

  /**
   * 处理核销成功（统一接口，所有券类型走此方法）
   */
  _handleVerifySuccess(data) {
    const isAlreadyVerified = data.alreadyVerified || false;
    const voucherType = data.voucherType || data.type || '';
    // 积分兑换：统一接口返回 goodsName 但无 voucherType
    const isPointsExchange = !!(data.goodsName && !voucherType);
    const isGoodsPickup = voucherType === 'goods_pickup';
    const isActivity = voucherType === 'activity' || voucherType === 'pick';

    let resultTitle, resultMsg, resultCode;

    if (isAlreadyVerified) {
      resultTitle = isGoodsPickup ? '已取货' : '已核销';
      resultMsg = data.message || '该订单已完成核销';
      resultCode = 'info';
    } else if (isPointsExchange) {
      resultTitle = '核销完成';
      resultMsg = `已成功核销「${data.goodsName || ''}」`;
      resultCode = 'complete';
    } else {
      resultTitle = isGoodsPickup ? '取货完成' : '核销完成';
      resultMsg = `已成功核销${data.typeName || ''}`;
      resultCode = 'complete';
    }

    // 优先使用接口返回的 items，并做字段映射
    const items = this._mapItems(data.items);

    this.setData({
      verifying: false,
      showResult: true,
      resultCode,
      resultTitle,
      resultMsg,
      voucherInfo: {
        typeName: isPointsExchange ? '积分兑换' : (data.typeName || (isGoodsPickup ? '商品自取' : '活动券')),
        userName: data.userName || '未知用户',
        userPhone: data.userPhone || '',
        content: data.content || data.title || data.message || data.goodsName || '',
        orderNo: data.orderNo || '',
        participantCount: data.participantCount || 1,
        showParticipants: !isPointsExchange && isActivity,
        totalAmount: data.totalAmount || '',
        items
      },
      pendingConfirm: null
    });

    // 接口未返回 items 时，兜底异步加载
    if (isGoodsPickup && data.orderNo && items.length === 0) {
      this._loadOrderItems(data.orderNo);
    }

    setTimeout(() => { this.loadHistory(); }, 500);
    wx.vibrateShort({ type: 'medium' });
  },

  _handleVerifyError(err) {
    let title = '核销失败';
    let msg = '该券无效或已被使用';

    if (err) {
      const code = err.code || err.statusCode;
      if (err.message && !err.message.startsWith('请求失败')) {
        msg = err.message;
      } else if (code) {
        if (code === 400) msg = '券码不能为空';
        else if (code === 404) msg = '未找到该券码，请确认二维码是否正确';
        else if (code === 403) msg = '该券已过期或已取消，无法核销';
        else if (code === 409) msg = '该兑换已核销，不能重复核销';
      }
    }

    this.setData({
      verifying: false,
      showResult: true,
      resultCode: 'fail',
      resultTitle: title,
      resultMsg: msg,
      voucherInfo: null,
      pendingConfirm: null
    });

    wx.vibrateShort({ type: 'heavy' });
  },

  /**
   * 重置，继续下一次核销
   */
  resetScan() {
    this.setData({
      showResult: false,
      resultCode: '',
      resultTitle: '',
      resultMsg: '',
      voucherInfo: null,
      pendingConfirm: null
    });
  },

  /**
   * 字段映射：后端 items → 前端 items
   * 兼容后端实际返回的 productName / productImage / unitPrice
   */
  _mapItems(items) {
    if (!Array.isArray(items) || items.length === 0) return [];
    return items.map(item => ({
      name: item.name || '',
      image: this._processImageUrl(item.image || ''),
      quantity: item.quantity || 0,
      price: item.price || 0
    }));
  },

  /**
   * 异步加载自取商品列表（兜底逻辑）
   */
  _loadOrderItems(orderNo, dataPath) {
    if (!orderNo) return;
    dataPath = dataPath || 'voucherInfo.items';
    api.order.getDetail(orderNo)
      .then(orderData => {
        if (!orderData) return;
        const rawItems = orderData.items || orderData.orderItems || [];
        if (rawItems.length > 0) {
          const items = rawItems.map(item => ({
            name: item.name || '',
            image: this._processImageUrl(item.image || ''),
            quantity: item.quantity || 0,
            price: item.price || 0
          }));
          if (items.length > 0) {
            this.setData({ [dataPath]: items });
          }
        }
      })
      .catch(() => {});
  },

  /**
   * 处理图片URL
   */
  _processImageUrl(url) {
    if (!url) return '';
    if (url.startsWith('http')) return url;
    if (url.startsWith('/')) return 'https://api.nengjifarm.com' + url;
    return 'https://api.nengjifarm.com/api/file/image/' + url;
  },

  /**
   * 加载今日核销历史
   */
  loadHistory() {
    const today = new Date();
    const startDate = this.formatDate(today);
    const endDate = this.formatDate(today);

    api.api.staff.getVerifyHistory({ startDate, endDate }, { showLoading: false })
      .then(data => {
        const list = Array.isArray(data) ? data : (data.list || data.data || []);
        const historyList = list.map(item => {
          const isPointsExchange = item.type === 'points_exchange' || item.typeName === '积分兑换' || item.voucherType === 'points_exchange';
          const isPickupHistory = item.voucherType === 'goods_pickup' || item.voucherType === 'pickup' || item.isPickupOrder || item.deliveryMethod === 'pickup' || item.typeName === '商品自取';
          const isStudy = item.typeName === '亲子研学' || item.categoryName === '亲子研学' || item.voucherType === 'parent_child_study';
          const isPickExperience = item.typeName === '采摘体验' || item.categoryName === '采摘体验' || item.voucherType === 'pick_experience';

          let tagClass = 'tag-activity';
          let voucherType = 'activity';
          let typeName = '活动券';

          if (isPointsExchange) {
            tagClass = 'tag-points';
            voucherType = 'points_exchange';
            typeName = '积分兑换';
          } else if (isPickupHistory) {
            tagClass = 'tag-pickup';
            voucherType = 'goods_pickup';
            typeName = '商品自取';
          } else if (isStudy) {
            tagClass = 'tag-study';
            voucherType = 'parent_child_study';
            typeName = '亲子研学';
          } else if (isPickExperience) {
            tagClass = 'tag-pick';
            voucherType = 'pick_experience';
            typeName = '采摘体验';
          }

          const statusMap = {
            'verified': '已核销',
            'pending': '待核销',
            'cancelled': '已取消'
          };
          const displayStatus = statusMap[item.status] || item.status || '已核销';

          return {
            id: item.id || Math.random().toString(36).substr(2, 9),
            voucherType,
            typeName,
            tagClass,
            userName: item.userName || '未知用户',
            userPhone: item.userPhone || item.phone || '',
            content: isPointsExchange ? (item.goodsName || '积分商品') : (item.content || item.description || '-'),
            participantCount: item.participantCount || item.count || item.numberOfDiners || 1,
            showParticipants: !isPointsExchange && !isPickupHistory,
            verifyTime: item.verifyTime || item.time || item.createTime,
            verifyTimeFormatted: item.verifyTime ? this.formatTime(item.verifyTime) : '-',
            status: displayStatus,
            verified: item.verified || true,
            orderId: item.orderId || item.orderNo || item.id,
            raw: item
          };
        });

        this.setData({ historyList });
      })
      .catch(() => {});
  },

  showHistoryDetail(e) {
    const item = e.currentTarget.dataset.item;
    if (!item) return;

    const raw = item.raw || {};
    const isGoodsPickup = item.voucherType === 'goods_pickup';

    // 优先使用 raw.items（历史记录接口可能已返回），兜底空数组
    const items = this._mapItems(raw.items);

    const historyDetail = {
      id: item.id,
      typeName: item.typeName,
      userName: item.userName,
      userPhone: item.userPhone || raw.userPhone || raw.phone || '-',
      content: item.content || raw.content || raw.voucherContent || '-',
      participantCount: item.participantCount || 1,
      showParticipants: item.showParticipants,
      verifyTime: item.verifyTimeFormatted || this.formatDateTime(raw.verifyTime || item.verifyTime),
      status: item.status,
      orderId: item.orderId || '-',
      isGoodsPickup,
      items
    };

    this.setData({
      showHistoryDetail: true,
      historyDetail
    });

    // 接口未返回 items 时，兜底异步加载
    if (isGoodsPickup && raw.orderNo && items.length === 0) {
      this._loadOrderItems(raw.orderNo, 'historyDetail.items');
    }
  },

  closeHistoryDetail() {
    this.setData({ showHistoryDetail: false, historyDetail: null });
  },

  formatTime(date) {
    try {
      const d = (date instanceof Date && !isNaN(date)) ? date : new Date(String(date).replace(/-/g, '/'));
      const month = String(d.getMonth() + 1).padStart(2, '0');
      const day = String(d.getDate()).padStart(2, '0');
      const hour = String(d.getHours()).padStart(2, '0');
      const min = String(d.getMinutes()).padStart(2, '0');
      return `${month}-${day} ${hour}:${min}`;
    } catch (e) {
      return '-';
    }
  },

  formatDateTime(dateStr) {
    if (!dateStr) return '-';
    try {
      const d = new Date(String(dateStr).replace(/-/g, '/'));
      const year = d.getFullYear();
      const month = String(d.getMonth() + 1).padStart(2, '0');
      const day = String(d.getDate()).padStart(2, '0');
      const hour = String(d.getHours()).padStart(2, '0');
      const min = String(d.getMinutes()).padStart(2, '0');
      return `${year}-${month}-${day} ${hour}:${min}`;
    } catch (e) {
      return '-';
    }
  },

  formatDate(date) {
    const d = (date instanceof Date && !isNaN(date)) ? date : new Date(String(date).replace(/-/g, '/'));
    const year = d.getFullYear();
    const month = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  },

  goToHistory() {
    wx.navigateTo({
      url: '/staff-pages/staff-verify-history/staff-verify-history'
    });
  }
});
