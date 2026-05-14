const api = require('../../utils/api');

Page({
  data: {
    scanned: false,
    inputCode: '',
    verifying: false,
    showResult: false,
    resultCode: '',     // 'success' | 'fail'
    resultTitle: '',
    resultMsg: '',
    voucherInfo: null,
    historyList: []
  },

  onLoad() {
    // 检查是否为员工
    const role = wx.getStorageSync('user_role');
    if (role !== 'staff') {
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
    
    this.loadHistory();
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
      onlyFromCamera: true,
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
   * 手动输入核销码
   */
  onCodeInput(e) {
    this.setData({ inputCode: e.detail.value.trim() });
  },

  verifyByInput() {
    const code = this.data.inputCode;
    if (!code) return;
    this.doVerify(code);
  },

  /**
   * 执行核销请求
   */
  doVerify(code) {
    if (this.data.verifying) return;

    this.setData({ verifying: true });

    api.api.staff.verifyVoucher(code)
      .then(data => {

        // 构建券信息展示
        const voucherInfo = {
          typeName: data.typeName||'活动卷',
          userName: data.userName || '未知用户',
          content: data.content || '-',
          useTime: this.formatTime(new Date())
        };

        this.setData({
          verifying: false,
          showResult: true,
          resultCode: 'success',
          resultTitle: '✅ 核销成功',
          resultMsg: `已成功核销${voucherInfo.typeName}`,
          voucherInfo: voucherInfo,
          inputCode: ''
        });

        // 刷新历史记录
        setTimeout(() => { this.loadHistory(); }, 500);

        // 震动反馈
        wx.vibrateShort({ type: 'medium' });
      })
      .catch(err => {
        
        let title = '❌ 核销失败';
        let msg = (err && err.message) || '该券无效或已被使用';
        
        // 如果是后端返回的错误信息，友好展示
        if (err && err.code === 404) {
          msg = '未找到该券信息，请确认二维码是否正确';
        } else if (err && err.code === 409) {
          msg = '该券已被使用，不能重复核销';
        } else if (err && err.code === 403) {
          msg = '该券已过期，无法核销';
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
      voucherInfo: null,
      inputCode: ''
    });
  },

  /**
   * 加载核销历史记录
   */
  loadHistory() {
    api.api.staff.getVerifyHistory()
      .then(list => {
        // 格式化数据
        const historyList = (list || []).map(item => ({
          id: item.id || Math.random().toString(36).substr(2, 9),
          voucherType: 'activity',
          typeName: item.typeName || '活动券',
          userName: item.userName || '未知',
          time: item.verifyTime ? this.formatTime(item.verifyTime) : '-',
          status: '已核销'
        }));

        this.setData({ historyList });
      })
      .catch(err => {
        // 不影响主功能
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
  }
});
