const api = require('../../utils/api');

Page({
  data: {
    historyList: [],
    loading: true,
    hasMore: true,
    currentPage: 1,
    pageSize: 10,
    total: 0,
    searchKeyword: '',
    filterType: 'all', // all/points_exchange/goods_pickup/parent_child_study/pick_experience
    scrollToView: '',
    categories: [
      { id: 'points_exchange', name: '积分兑换' },
      { id: 'goods_pickup', name: '商品自取' },
      { id: 'parent_child_study', name: '亲子研学' },
      { id: 'pick_experience', name: '采摘体验' }
    ],
    dateRange: {
      startDate: '',
      endDate: ''
    },
    showDetail: false,
    detailItem: null
  },

  onLoad() {
    // 验证员工权限
    this.verifyPermission();
  },

  /**
   * 验证员工权限
   */
  verifyPermission() {
    this.setData({ loading: true });
    api.api.staff.verifyPermission(null, { showLoading: false })
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
        this.setData({ loading: false });
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
   * 加载活动分类（与活动页面保持一致）
   */
  loadCategories() {
    api.request({
      url: '/api/activity/list',
      method: 'GET',
      showLoading: false
    }, { showLoading: false })
      .then(data => {
        let allActivities = [];
        if (data.activities && data.activities.all) {
          allActivities = data.activities.all;
        } else if (Array.isArray(data.activities)) {
          allActivities = data.activities;
        } else if (Array.isArray(data)) {
          allActivities = data;
        }

        const categorySet = new Set();
        allActivities.forEach(activity => {
          if (activity.categoryName) {
            categorySet.add(activity.categoryName);
          }
        });

        const categories = Array.from(categorySet).map(name => ({
          id: name,
          name: name
        }));

        this.setData({ categories });
      })
      .catch(err => {
      });
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

    const loadPromise = new Promise((resolve, reject) => {

    // 添加筛选条件
    if (this.data.filterType !== 'all') {
      params.voucherType = this.data.filterType;
    }

    if (this.data.searchKeyword) {
      params.keyword = this.data.searchKeyword;
      // 同时搜索活动名称（后端支持activityName参数）
      params.activityName = this.data.searchKeyword;
    }

    if (this.data.dateRange.startDate) {
      params.startDate = this.data.dateRange.startDate;
    }

    if (this.data.dateRange.endDate) {
      params.endDate = this.data.dateRange.endDate;
    }

    api.api.staff.getVerifyHistory(params, { showLoading: false })
      .then(data => {
        const list = Array.isArray(data) ? data : (data.list || data.data || []);
        const total = data.total || list.length;

        const historyList = list.map(item => {
          const isPointsExchange = item.type === 'points_exchange' || item.typeName === '积分兑换' || item.voucherType === 'points_exchange';
          const isGoodsPickup = item.voucherType === 'goods_pickup' || item.voucherType === 'pickup' || item.isPickupOrder || item.deliveryMethod === 'pickup' || item.typeName === '商品自取';
          const isParentChildStudy = item.typeName === '亲子研学' || item.categoryName === '亲子研学' || item.voucherType === 'parent_child_study';
          const isPickExperience = item.typeName === '采摘体验' || item.categoryName === '采摘体验' || item.voucherType === 'pick_experience';
          
          let tagClass = 'tag-activity';
          let voucherType = 'activity';
          let typeName = '活动券';
          
          if (isPointsExchange) {
            tagClass = 'tag-points';
            voucherType = 'points_exchange';
            typeName = '积分兑换';
          } else if (isGoodsPickup) {
            tagClass = 'tag-pickup';
            voucherType = 'goods_pickup';
            typeName = '商品自取';
          } else if (isParentChildStudy) {
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
          showParticipants: !isPointsExchange && !isGoodsPickup,
          verifyTime: item.verifyTime || item.time || item.createTime,
          verifyTimeFormatted: this.formatDateTime(item.verifyTime || item.time || item.createTime),
          status: displayStatus,
          verified: item.verified || true,
          orderId: item.orderId || item.orderNo || item.id,
          raw: item
          };
        });

        // 分页处理：如果是第一页则覆盖，否则追加
        const newHistoryList = this.data.currentPage === 1
          ? historyList
          : [...this.data.historyList, ...historyList];

        resolve({ newHistoryList, total });
      })
      .catch(err => {
        reject(err);
      });
    });

    Promise.all([loadPromise, new Promise(resolve => setTimeout(resolve, 1000))])
      .then(([{ newHistoryList, total }]) => {
        this.setData({
          historyList: newHistoryList,
          total: total,
          hasMore: newHistoryList.length < total,
          loading: false
        });
      })
      .catch(err => {
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
   * 搜索 - 支持活动名称/核销码/用户名模糊搜索
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
    const scrollTo = type === 'all' ? 'filter-all' : (type === 'date' ? 'filter-date' : `filter-${type}`);
    this.setData({ 
      filterType: type, 
      currentPage: 1, 
      hasMore: true,
      scrollToView: scrollTo
    });
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
   * 查看核销记录详情
   */
  viewDetail(e) {
    const item = e.currentTarget.dataset.item;
    if (!item) return;

    const raw = item.raw || {};
    const isGoodsPickup = item.voucherType === 'goods_pickup';
    const detailItem = {
      id: item.id,
      typeName: item.typeName,
      userName: item.userName,
      userPhone: item.userPhone || '-',
      content: item.content,
      participantCount: item.participantCount,
      showParticipants: item.showParticipants,
      verifyTime: item.verifyTimeFormatted,
      status: item.status,
      orderId: item.orderId || '-',
      isGoodsPickup,
      items: []
    };

    this.setData({
      showDetail: true,
      detailItem
    });

    // 自取商品：异步加载商品列表
    if (isGoodsPickup && raw.orderNo) {
      api.order.getDetail(raw.orderNo, { showLoading: false })
        .then(orderData => {
          if (orderData && orderData.items && orderData.items.length > 0) {
            const items = orderData.items.map(i => ({
              name: i.name,
              image: this._processImageUrl(i.image),
              quantity: i.quantity,
              price: i.price
            }));
            this.setData({ 'detailItem.items': items });
          }
        })
        .catch(() => {});
    }
  },

  /**
   * 关闭核销记录详情
   */
  closeDetail() {
    this.setData({
      showDetail: false,
      detailItem: null
    });
  },

  /**
   * 处理图片URL
   */
  _processImageUrl(url) {
    if (!url) return '';
    if (url.startsWith('http')) return url;
    const base = getApp().globalData.baseUrl || 'https://api.nengjifarm.com';
    if (url.startsWith('/api/')) return base + url;
    return base + '/api/file/image/' + url;
  },

  /**
   * 格式化日期时间
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
