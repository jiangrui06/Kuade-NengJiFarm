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
        console.error('权限验证失败:', err);
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
        console.log('扫码结果:', res.result);

        const code = (res.result || '').trim();
        if (!code) {
          wx.showToast({ title: '无效的二维码', icon: 'none' });
          return;
        }

        // 执行核销
        this.doVerify(code);
      },
      fail: (err) => {
        console.log('取消扫码');
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

    api.api.staff.verifyOrder(code)
      .then(data => {
        console.log('核销响应:', data);

        // 检查是否已核销（重复扫码）
        const isAlreadyVerified = data.alreadyVerified || false;
        const isVerified = data.verified || false;

        // 构建券信息展示
        const voucherInfo = {
          typeName: data.typeName||'活动券',
          userName: data.userName || '未知用户',
          content: data.content || data.message || '-',
          useTime: this.formatTime(new Date()),
          participantCount: data.participantCount || data.count || data.verifiedCount || data.numberOfDiners || 1
        };

        // 根据是否已核销设置不同的提示
        let resultTitle, resultMsg, resultCode;
        if (isAlreadyVerified) {
          resultTitle = '券已核销';
          resultMsg = data.message || '该券已被核销，无需重复操作';
          resultCode = 'info';
        } else {
          resultTitle = '核销完成';
          resultMsg = `已成功核销${voucherInfo.typeName}`;
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
      })
      .catch(err => {
        console.error('核销失败:', err);

        let title = '核销失败';
        let msg = (err && err.message) || '该券无效或已被使用';

        // 如果是后端返回的错误信息，友好展示
        if (err && err.code === 404) {
          msg = '未找到该券信息，请确认二维码是否正确';
        } else if (err && err.code === 409) {
          msg = '该券已被使用，不能重复核销';
        } else if (err && err.code === 403) {
          msg = err.message || '无权操作';
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
      });
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

    console.log('加载今日核销记录:', { startDate, endDate });

    // 历史记录加载不显示 loading，避免影响主功能
    api.api.staff.getVerifyHistory({ startDate, endDate }, { showLoading: false })
      .then(data => {
        console.log('核销历史API响应:', data);

        // 根据API文档，响应结构为 {list: [...], total: ..., page: ..., pageSize: ...}
        const list = Array.isArray(data) ? data : (data.list || data.data || []);

        console.log('解析后的列表:', list);

        // 格式化数据（与 staff-verify-history 保持一致）
        const historyList = list.map(item => ({
          id: item.id || Math.random().toString(36).substr(2, 9),
          voucherType: item.voucherType || 'activity',
          typeName: item.categoryName || item.typeName || '活动券',
          userName: item.userName || '未知用户',
          userPhone: item.userPhone || item.phone || '',
          content: item.content || item.description || '-',
          participantCount: item.participantCount || item.count || item.numberOfDiners || 1,
          verifyTime: item.verifyTime || item.time || item.createTime,
          verifyTimeFormatted: item.verifyTime ? this.formatTime(new Date(item.verifyTime)) : '-',
          status: item.status || '已核销',
          verified: item.verified || true,
          orderId: item.orderId || item.orderNo || item.id,
          raw: item
        }));

        console.log('格式化后的历史列表:', historyList);

        this.setData({ historyList });
      })
      .catch(err => {
        console.error('加载核销历史失败:', err);
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
      const d = date instanceof Date ? date : new Date(date);
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
      const d = new Date(dateStr);
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
    const d = date instanceof Date ? date : new Date(date);
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
