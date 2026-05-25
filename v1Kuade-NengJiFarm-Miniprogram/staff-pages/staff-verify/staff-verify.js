const api = require('../../utils/api');

Page({
  data: {
    scanned: false,
    verifying: false,
    showResult: false,
    resultCode: '',     // 'success' | 'fail'
    resultTitle: '',
    resultMsg: '',
    voucherInfo: null,
    historyList: [],
    showHistoryDetail: false,
    historyDetail: null
  },

  onLoad() {
    // 验证员工权限
    this.verifyPermission();
  },

  /**
   * 验证员工权限
   */
  verifyPermission() {
    api.api.staff.verifyPermission()
      .then(data => {
        if (!data.hasPermission) {
          wx.showModal({
            title: '无权限访问',
            content: '仅员工账号可使用核销功能',
            showCancel: false,
            success: () => {
              wx.navigateBack();
            }
          });
          return;
        }

        // 权限验证通过，加载历史记录
        this.loadHistory();
      })
      .catch(err => {
        wx.showModal({
          title: '权限验证失败',
          content: '无法验证员工权限，请稍后重试',
          showCancel: false,
          success: () => {
            wx.navigateBack();
          }
        });
      });
  },

  onShow() {
    // 每次显示时刷新历史
    this.loadHistory();
  },

  /**
   * 扫码（微信扫一扫）
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

        // 执行核销
        this.doVerify(code);
      },
      fail: (err) => {
        // 用户取消了扫码，不弹提示
        if (!err.errMsg.includes('cancel')) {
          wx.showToast({ title: '扫码失败', icon: 'none' });
        }
      }
    });
  },

  /**
   * 执行核销请求
   */
  doVerify(code) {
    if (this.data.verifying) return;

    this.setData({ verifying: true });

    // 先尝试积分兑换核销，404 则自动尝试活动券核销
    this._verifyPointsExchange(code);
  },

  _verifyPointsExchange(code) {
    api.api.staff.verifyPointsExchange(code)
      .then(data => this._handleVerifySuccess(data))
      .catch(err => {
        // 积分兑换未找到 → 尝试活动券核销
        if (err && err.code === 404) {
          this._verifyVoucher(code);
        } else {
          this._handleVerifyError(err);
        }
      });
  },

  _verifyVoucher(code) {
    api.api.staff.verifyVoucher(code)
      .then(data => this._handleVerifySuccess(data))
      .catch(err => this._handleVerifyError(err));
  },

  _handleVerifySuccess(data) {
    // 判断响应类型：积分兑换核销（有 goodsName 无 voucherType） vs 券/商品自取核销
    const isPointsExchange = data.goodsName && !data.voucherType && !data.type;

    let isAlreadyVerified, verifyTime, voucherType, isGoodsPickup;
    let typeName, userName, content, participantCount;

    if (isPointsExchange) {
      // 积分兑换核销响应（后端文档 §4）
      isAlreadyVerified = false;
      verifyTime = data.verifyTime || null;
      voucherType = 'points_exchange';
      isGoodsPickup = false;
      typeName = '积分兑换';
      userName = data.userName || '用户';
      content = data.goodsName || '-';
      participantCount = 1;
    } else {
      // 券/商品自取核销响应（已有逻辑）
      isAlreadyVerified = data.alreadyVerified || false;
      verifyTime = data.verifyTime || null;
      voucherType = data.voucherType || data.type || '';
      isGoodsPickup = voucherType === 'goods_pickup' || voucherType === 'pickup' || data.isPickupOrder || data.deliveryMethod === 'pickup';
      typeName = isGoodsPickup ? '商品自取' : (data.typeName || (voucherType === 'pick' ? '采摘券' : '活动券'));
      userName = data.userName || '未知用户';
      content = data.content || data.title || data.message ;
      participantCount = data.participantCount || data.count || data.verifiedCount || data.numberOfDiners || 1;
    }

    // 活动类型才显示人数
    const isActivity = !isPointsExchange && !isGoodsPickup;

    const voucherInfo = {
      typeName,
      userName,
      content,
      useTime: verifyTime ? this.formatTime(verifyTime) : this.formatTime(new Date()),
      participantCount,
      showParticipants: isActivity
    };

    let resultTitle, resultMsg, resultCode;
    if (isAlreadyVerified) {
      resultTitle = isGoodsPickup ? '已取货' : '券已核销';
      resultMsg = data.message || '该订单已完成核销，无需重复操作';
      resultCode = 'info';
    } else {
      resultTitle = isGoodsPickup ? '取货完成' : (isPointsExchange ? '核销完成' : '核销完成');
      resultMsg = isPointsExchange ? `已成功核销「${data.goodsName || ''}」` : `已成功核销${typeName}`;
      resultCode = 'complete';
    }

    this.setData({
      verifying: false,
      showResult: true,
      resultCode: resultCode,
      resultTitle: resultTitle,
      resultMsg: resultMsg,
      voucherInfo: voucherInfo,
    });

    // 刷新历史记录
    setTimeout(() => { this.loadHistory(); }, 500);

    // 震动反馈
    wx.vibrateShort({ type: isAlreadyVerified ? 'light' : 'medium' });
  },

  _handleVerifyError(err) {
    let title = '核销失败';
    let msg = '该券无效或已被使用';

    if (err) {
      const code = err.code || err.statusCode;
      // 后端返回了业务消息（非 HTTP 层的"请求失败"）时优先使用
      if (err.message && !err.message.startsWith('请求失败')) {
        msg = err.message;
      } else if (code) {
        // 后端无业务消息时按错误码显示默认文案
        if (code === 400) {
          msg = '券码不能为空';
        } else if (code === 404) {
          msg = '未找到该券码，请确认二维码是否正确';
        } else if (code === 403) {
          msg = '该券已过期或已取消，无法核销';
        } else if (code === 409) {
          msg = '该兑换已核销，不能重复核销';
        }
      }
    }

    this.setData({
      verifying: false,
      showResult: true,
      resultCode: 'fail',
      resultTitle: title,
      resultMsg: msg,
      voucherInfo: null
    });

    // 错误震动
    wx.vibrateShort({ type: 'heavy' });
  },

  /**
   * 重置扫描状态，继续下一次核销
   */
  resetScan() {
    this.setData({
      showResult: false,
      resultCode: '',
      resultTitle: '',
      resultMsg: '',
      voucherInfo: null
    });
  },

  /**
   * 加载今日核销历史记录
   */
  loadHistory() {
    // 获取今日日期范围
    const today = new Date();
    const startDate = this.formatDate(today);
    const endDate = this.formatDate(today);


    // 历史记录加载不显示 loading，避免影响主功能
    api.api.staff.getVerifyHistory({ startDate, endDate }, { showLoading: false })
      .then(data => {

        // 根据API文档，响应结构为 {list: [...], total: ..., page: ..., pageSize: ...}
        const list = Array.isArray(data) ? data : (data.list || data.data || []);


        // 格式化数据（与 staff-verify-history 保持一致）
        const historyList = list.map(item => {
          const isPointsExchange = item.type === 'points_exchange' || item.typeName === '积分兑换';
          const isPickupHistory = item.voucherType === 'goods_pickup' || item.voucherType === 'pickup' || item.isPickupOrder || item.deliveryMethod === 'pickup';
          const isStudy = item.typeName === '亲子研学' || item.categoryName === '亲子研学';
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
          voucherType: voucherType,
          typeName: typeName,
          tagClass: tagClass,
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
      .catch(err => {
        wx.showToast({ title: '加载失败', icon: 'none' });
      });
  },

  /**
   * 查看核销记录详情（与 staff-verify-history 保持一致）
   */
  showHistoryDetail(e) {
    const item = e.currentTarget.dataset.item;
    if (!item) return;

    const raw = item.raw || {};
    this.setData({
      showHistoryDetail: true,
      historyDetail: {
        id: item.id,
        typeName: item.typeName,
        userName: item.userName,
        userPhone: item.userPhone || raw.userPhone || raw.phone || '-',
        content: item.content || raw.content || raw.voucherContent || '-',
        participantCount: item.participantCount || 1,
        showParticipants: item.showParticipants,
        verifyTime: item.verifyTimeFormatted || this.formatDateTime(raw.verifyTime || item.verifyTime),
        status: item.status,
        orderId: item.orderId || '-'
      }
    });
  },

  /**
   * 关闭核销记录详情
   */
  closeHistoryDetail() {
    this.setData({
      showHistoryDetail: false,
      historyDetail: null
    });
  },

  /**
   * 格式化时间
   */
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

  /**
   * 格式化日期时间为 YYYY-MM-DD HH:mm
   */
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

  /**
   * 格式化日期为 YYYY-MM-DD
   */
  formatDate(date) {
    const d = (date instanceof Date && !isNaN(date)) ? date : new Date(String(date).replace(/-/g, '/'));
    const year = d.getFullYear();
    const month = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  },

  /**
   * 跳转到核销记录历史页面
   */
  goToHistory() {
    wx.navigateTo({
      url: '/staff-pages/staff-verify-history/staff-verify-history'
    });
  }
});
