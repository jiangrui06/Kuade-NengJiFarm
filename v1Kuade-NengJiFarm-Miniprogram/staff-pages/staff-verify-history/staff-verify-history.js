const api = require('../../utils/api');

Page({
  data: {
    historyList: [],
    loading: true,
    hasMore: true,
    currentPage: 1,
    pageSize: 20,
    total: 0,
    searchKeyword: '',
    filterType: 'all', // all/pick/activity
    dateRange: {
      startDate: '',
      endDate: ''
    }
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
            content: '仅员工账号可查看核销记录',
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
    // 每次显示时刷新数据
    if (this.data.historyList.length === 0) {
      this.loadHistory();
    }
  },

  /**
   * 加载核销历史记录
   */
  loadHistory() {
    this.setData({ loading: true });

    const params = {
      page: this.data.currentPage,
      pageSize: this.data.pageSize
    };

    // 添加筛选条件
    if (this.data.filterType !== 'all') {
      params.voucherType = this.data.filterType;
    }

    if (this.data.searchKeyword) {
      params.keyword = this.data.searchKeyword;
    }

    if (this.data.dateRange.startDate) {
      params.startDate = this.data.dateRange.startDate;
    }

    if (this.data.dateRange.endDate) {
      params.endDate = this.data.dateRange.endDate;
    }

    api.api.staff.getVerifyHistory(params)
      .then(data => {
        const list = Array.isArray(data) ? data : (data.list || data.data || []);
        const total = data.total || list.length;

        const historyList = list.map(item => ({
          id: item.id || Math.random().toString(36).substr(2, 9),
          voucherType: item.voucherType || item.type || 'pick',
          typeName: item.typeName || (item.voucherType === 'pick' || item.type === 'pick' ? '采摘券' : '活动券'),
          userName: item.userName || item.userName || '未知用户',
          userPhone: item.userPhone || item.phone || '',
          content: item.content || item.description || '-',
          verifyTime: item.verifyTime || item.time || item.createTime,
          verifyTimeFormatted: this.formatDateTime(item.verifyTime || item.time || item.createTime),
          status: item.status || '已核销',
          verified: item.verified || true,  // 核销记录默认已核销
          orderId: item.orderId || item.orderNo || item.id
        }));

        // 分页处理：如果是第一页则覆盖，否则追加
        const newHistoryList = this.data.currentPage === 1
          ? historyList
          : [...this.data.historyList, ...historyList];

        this.setData({
          historyList: newHistoryList,
          total: total,
          hasMore: newHistoryList.length < total,
          loading: false
        });
      })
      .catch(err => {
        console.error('加载核销历史失败:', err);
        this.setData({ loading: false });
        wx.showToast({ title: '加载失败', icon: 'none' });
      });
  },

  /**
   * 下拉刷新
   */
  onPullDownRefresh() {
    this.setData({ currentPage: 1, hasMore: true });
    this.loadHistory();
    setTimeout(() => { wx.stopPullDownRefresh(); }, 1000);
  },

  /**
   * 触底加载更多
   */
  onReachBottom() {
    if (!this.data.hasMore || this.data.loading) return;

    this.setData({ currentPage: this.data.currentPage + 1 });
    this.loadHistory();
  },

  /**
   * 搜索
   */
  onSearchInput(e) {
    const keyword = e.detail.value.trim();
    this.setData({ searchKeyword: keyword, currentPage: 1, hasMore: true });
    this.loadHistory();
  },

  /**
   * 筛选类型
   */
  onFilterChange(e) {
    const type = e.currentTarget.dataset.type;
    this.setData({ filterType: type, currentPage: 1, hasMore: true });
    this.loadHistory();
  },

  /**
   * 选择日期范围
   */
  selectDateRange() {
    const self = this;
    wx.showActionSheet({
      itemList: ['今天', '最近7天', '最近30天', '全部'],
      success(res) {
        const now = new Date();
        let startDate = '';
        let endDate = '';

        switch (res.tapIndex) {
          case 0: // 今天
            startDate = self.formatDate(now);
            endDate = self.formatDate(now);
            break;
          case 1: // 最近7天
            startDate = self.formatDate(new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000));
            endDate = self.formatDate(now);
            break;
          case 2: // 最近30天
            startDate = self.formatDate(new Date(now.getTime() - 30 * 24 * 60 * 60 * 1000));
            endDate = self.formatDate(now);
            break;
          case 3: // 全部
            startDate = '';
            endDate = '';
            break;
        }

        self.setData({
          dateRange: { startDate, endDate },
          currentPage: 1,
          hasMore: true
        });
        self.loadHistory();
      }
    });
  },

  /**
   * 查看详情
   */
  viewDetail(e) {
    const item = e.currentTarget.dataset.item;
    wx.showModal({
      title: '核销详情',
      content: `券类型：${item.typeName}\n持券人：${item.userName}\n核销时间：${item.verifyTimeFormatted}\n券内容：${item.content}`,
      showCancel: false,
      confirmText: '知道了'
    });
  },

  /**
   * 格式化日期时间
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
   * 格式化日期
   */
  formatDate(date) {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  },

  /**
   * 返回上一页
   */
  goBack() {
    wx.navigateBack();
  },

  /**
   * 跳转到核销工作台
   */
  goToVerify() {
    wx.redirectTo({
      url: '/staff-pages/staff-verify/staff-verify'
    });
  }
});
