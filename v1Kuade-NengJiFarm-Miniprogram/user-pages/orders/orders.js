const api = require('../../utils/api').api || require('../../utils/api');
const { orderTimer } = require('../../utils/order-timer');

Page({
  data: {
    activeTab: 'all',
    currentOrderType: '',
    scrollToView: '',
    tabs: [
        { key: 'all', name: '全部' },
        { key: 'pending', name: '待付款' },
        { key: 'paid', name: '待发货' },
        { key: 'shipping', name: '待收货' },
        { key: 'cancelled', name: '已取消' }
      ],
    searchKeyword: '',
    searching: false,
    allOrders: [],
    orders: [],
    noSearchResult: false,
    loading: true,
    isRequesting: false,
    isPageVisible: false,
    orderCountdowns: {}
  },
  
  searchTimer: null,
  countdownTimer: null,
  refreshTimer: null,

  onLoad(options) {
    // 完整初始化所有状态
    this.initPageState();
    console.log('Orders page onLoad, options:', options);
    let tab = 'all';
    if (options.tab) {
      console.log('Setting activeTab to:', options.tab);
      tab = options.tab;
    }
    console.log('Current activeTab:', tab);
    
    this.setData({
      activeTab: tab,
      scrollToView: 'tab-' + tab,
      isPageVisible: true
    });
    this.getOrders();
  },

  onShow() {
    // 每次显示页面时都重新初始化关键状态
    this.initPageState();
    if (this.data.isPageVisible) {
      this.getOrders();
    }
    this.setData({ isPageVisible: true });
    this.startCountdownUpdate();
    this.startOrderRefresh();
  },

  // 页面状态初始化函数
  initPageState() {
    // 初始化处理超时订单的集合
    if (!this.processingTimeoutOrders) {
      this.processingTimeoutOrders = new Set();
    } else {
      this.processingTimeoutOrders.clear();
    }
    console.log('Page state initialized');
  },

  onHide() {
    this.setData({ isPageVisible: false });
    this.stopCountdownUpdate();
    this.stopOrderRefresh();
  },

  onUnload() {
    this.stopCountdownUpdate();
    this.stopOrderRefresh();
  },

  goBack() {
    wx.navigateBack({
      fail: () => {
        wx.reLaunch({
          url: '/pages/index/index'
        });
      }
    });
  },

  onSearchInput(e) {
    const keyword = e.detail.value;
    this.setData({ searchKeyword: keyword });
    this.applyLocalSearchFilter(keyword);
  },

  applyLocalSearchFilter: function (keyword) {
    const filteredOrders = this.filterOrders(this.data.allOrders, keyword);
    const hasSearchKeyword = keyword && keyword.trim();
    const noSearchResult = hasSearchKeyword && filteredOrders.length === 0 && this.data.allOrders.length > 0;
    
    this.setData({
      orders: filteredOrders,
      noSearchResult: noSearchResult
    });
  },

  searchOrders() {
    if (this.data.allOrders && this.data.allOrders.length > 0) {
      this.applyLocalSearchFilter(this.data.searchKeyword);
    } else {
      this.getOrders();
    }
  },

  processImageUrl(imageUrl) {
    const utils = require('../../utils/utils');
    return utils.media.processUrl(imageUrl);
  },

  filterOrders: function (orders, keyword) {
    if (!keyword || !keyword.trim()) {
      return orders;
    }
    
    const searchKey = keyword.trim().toLowerCase();
    return orders.filter(order => {
      if (order.orderNumber && String(order.orderNumber).toLowerCase().includes(searchKey)) {
        return true;
      }
      if (order.typeText && order.typeText.toLowerCase().includes(searchKey)) {
        return true;
      }
      if (order.items && order.items.length > 0) {
        for (let item of order.items) {
          if (item.name && item.name.toLowerCase().includes(searchKey)) {
            return true;
          }
        }
      }
      return false;
    });
  },

  getOrders() {
    if (this.data.isRequesting) {
      console.log('请求中，忽略重复调用');
      return;
    }
    
    this.setData({ isRequesting: true, loading: true, searching: true });

    let params = { page: 1, pageSize: 50 };
    if (this.data.activeTab !== 'all') {
      params.status = this.data.activeTab;
    }

    const self = this;
    
    // 使用新的订单聚合API
    api.order.getList(params)
      .then((responseData) => {
        // 新 API 返回格式：{ total: 21, orders: [ ... ], page: 1, pageSize: 10, totalPages: 1 }
        const orders = responseData && responseData.orders ? responseData.orders : (Array.isArray(responseData) ? responseData : []);
        
        const allOrders = orders.map(order => {
          // 添加类型文本描述
          let typeText = order.typeText || '订单';
          if (!order.typeText) {
            if (order.type === 'goods') typeText = '商品订单';
            else if (order.type === 'food') typeText = '点餐订单';
            else if (order.type === 'activity') typeText = '活动订单';
            else if (order.type === 'acre') typeText = '认购订单';
          }
          
          return {
            ...order,
            id: order.id || order.orderId,
            orderNumber: order.orderNumber || order.orderNo,
            typeText: typeText,
            totalPrice: (order.totalPrice || order.totalAmount || 0).toString().replace(/[¥￥]/g, ''),
            statusText: self.mapStatusToText(order),
            items: (order.items || []).map(item => ({
              ...item,
              image: self.processImageUrl(item.image),
              price: (item.price || item.unitPrice || 0).toString().replace(/[¥￥]/g, '')
            }))
          };
        });
        
        // 按创建时间倒序排列
        allOrders.sort((a, b) => new Date(b.createTime) - new Date(a.createTime));

        self.initOrderCountdowns(allOrders);

        const filteredOrders = self.filterOrders(allOrders, self.data.searchKeyword);
        const hasSearchKeyword = self.data.searchKeyword && self.data.searchKeyword.trim();
        const noSearchResult = hasSearchKeyword && filteredOrders.length === 0 && allOrders.length > 0;
        
        self.setData({
          allOrders: allOrders,
          orders: filteredOrders,
          noSearchResult: noSearchResult,
          loading: false,
          searching: false,
          isRequesting: false
        });
      })
      .catch((err) => {
        console.error('获取订单列表失败:', err);
        // 降级处理：尝试旧接口
        self.getOrdersLegacy(params);
      });
  },

  // 旧接口兼容方法
  getOrdersLegacy(params) {
    const self = this;
    Promise.all([
      api.order.getCommodityList(params).catch(() => []),
      api.order.getDishList(params).catch(() => []),
      api.order.getActivityList(params).catch(() => [])
    ])
      .then(([commodityOrders, dishOrders, activityOrders]) => {
        const combinedOrders = [
          ...(commodityOrders || []).map(o => ({ ...o, type: 'goods', typeText: '商品订单' })),
          ...(dishOrders || []).map(o => ({ ...o, type: 'food', typeText: '点餐订单' })),
          ...(activityOrders || []).map(o => ({ ...o, type: 'activity', typeText: '活动订单' }))
        ];
        
        const allOrders = combinedOrders.map(order => ({
          ...order,
          id: order.orderId || order.id,
          orderNumber: order.orderNo || order.orderNumber,
          totalPrice: (order.totalAmount || order.totalPrice || 0).toString().replace(/[¥￥]/g, ''),
          statusText: self.mapStatusToText(order),
          items: (order.items || []).map(item => ({
            ...item,
            image: self.processImageUrl(item.image),
            price: (item.price || item.unitPrice || 0).toString().replace(/[¥￥]/g, '')
          }))
        }));
        
        allOrders.sort((a, b) => new Date(b.createTime) - new Date(a.createTime));
        self.initOrderCountdowns(allOrders);

        const filteredOrders = self.filterOrders(allOrders, self.data.searchKeyword);
        const hasSearchKeyword = self.data.searchKeyword && self.data.searchKeyword.trim();
        const noSearchResult = hasSearchKeyword && filteredOrders.length === 0 && allOrders.length > 0;
        
        self.setData({
          allOrders: allOrders,
          orders: filteredOrders,
          noSearchResult: noSearchResult,
          loading: false,
          searching: false,
          isRequesting: false
        });
      })
      .catch((err) => {
        console.error('获取订单列表失败:', err);
        self.setData({
          loading: false,
          searching: false,
          isRequesting: false
        });
      });
  },

  mapStatusToText(order) {
    // 新订单聚合API直接返回 status 字段
    if (order.status) {
      const statusMap = {
        'pending': '待付款',
        'paid': '待发货',
        'shipping': '运输中',
        'completed': '已完成',
        'cancelled': '已取消'
      };
      return statusMap[order.status] || order.statusText || '进行中';
    }
    
    // 旧接口兼容
    const statusId = order.orderStatusId || order.orderStatus;
    const type = order.type;
    
    if (type === 'goods') {
      const statusMap = { 1: '待付款', 2: '待发货', 3: '运输中', 4: '已完成', 5: '已取消', 6: '退款中', 7: '已退款' };
      return statusMap[statusId] || '未知状态';
    } else if (type === 'food') {
      const statusMap = { 1: '待付款', 2: '已付款', 3: '已完成', 4: '已取消' };
      return statusMap[statusId] || '未知状态';
    } else if (type === 'activity') {
      const statusMap = { 1: '待付款', 2: '待核销', 3: '已核销', 4: '已取消', 5: '退款中', 6: '已退款' };
      return statusMap[statusId] || '未知状态';
    }
    return order.statusText || '进行中';
  },

  switchTab(e) {
    const tab = e.currentTarget.dataset.tab;
    if (tab === this.data.activeTab) return;
    
    this.setData({
      activeTab: tab,
      scrollToView: 'tab-' + tab,
      searchKeyword: '',
      noSearchResult: false
    });
    this.getOrders();
  },

  initOrderCountdowns(orders) {
    const orderCountdowns = {};
    orders.forEach(order => {
      if (order.status === 'pending' || order.status === 'pending_payment') {
        const remaining = orderTimer.getRemainingTime(order.createTime);
        if (remaining > 0) {
          orderCountdowns[order.id] = orderTimer.formatTime(remaining);
        } else {
          orderCountdowns[order.id] = '00:00';
          this.handleOrderTimeout(order.id);
        }
      }
    });
    this.setData({ orderCountdowns });
  },

  startCountdownUpdate() {
    this.stopCountdownUpdate();
    this.countdownTimer = setInterval(() => {
      const { orders, orderCountdowns } = this.data;
      const nextCountdowns = { ...orderCountdowns };
      let hasChange = false;

      orders.forEach(order => {
        if (order.status === 'pending' || order.status === 'pending_payment') {
          const remaining = orderTimer.getRemainingTime(order.createTime);
          const nextTimeStr = orderTimer.formatTime(remaining);
          
          if (nextCountdowns[order.id] !== nextTimeStr) {
            nextCountdowns[order.id] = nextTimeStr;
            hasChange = true;
          }
          
          if (remaining <= 0) {
            this.handleOrderTimeout(order.id);
          }
        }
      });

      if (hasChange) {
        this.setData({ orderCountdowns: nextCountdowns });
      }
    }, 1000);
  },

  stopCountdownUpdate() {
    if (this.countdownTimer) {
      clearInterval(this.countdownTimer);
      this.countdownTimer = null;
    }
  },

  handleOrderTimeout(orderId) {
    if (this.processingTimeoutOrders && this.processingTimeoutOrders.has(orderId)) {
      return;
    }
    
    this.processingTimeoutOrders.add(orderId);
    console.log('处理超时订单:', orderId);
    
    orderTimer.handleTimeout(orderId, (id) => {
      console.log('订单超时处理完成，刷新列表:', id);
      this.getOrders();
    });
  },

  startOrderRefresh() {
    this.stopOrderRefresh();
    this.refreshTimer = setInterval(() => {
      if (!this.data.isRequesting && this.data.isPageVisible) {
        this.getOrders();
      }
    }, 15000);
  },

  stopOrderRefresh() {
    if (this.refreshTimer) {
      clearInterval(this.refreshTimer);
      this.refreshTimer = null;
    }
  },

  payOrder(e) {
    const { orderId, type, activityid } = e.currentTarget.dataset;
    const id = orderId || e.currentTarget.dataset.id;
    wx.navigateTo({
      url: `/user-pages/pay/pay?orderId=${id}&type=${type}&activityId=${activityid || ''}`
    });
  },

  cancelOrder(e) {
    const id = e.currentTarget.dataset.orderId || e.currentTarget.dataset.id;
    wx.showModal({
      title: '取消订单',
      content: '确定要取消这个订单吗？',
      success: (res) => {
        if (res.confirm) {
          wx.showLoading({ title: '处理中...' });
          api.order.cancel(id)
            .then(() => {
              wx.showToast({ title: '订单已取消', icon: 'success' });
              this.getOrders();
            })
            .catch(err => {
              wx.showToast({ title: err.message || '取消失败', icon: 'none' });
            });
        }
      }
    });
  },

  deleteOrder(e) {
    const id = e.currentTarget.dataset.orderId || e.currentTarget.dataset.id;
    wx.showModal({
      title: '删除订单',
      content: '确定要删除这个订单记录吗？',
      success: (res) => {
        if (res.confirm) {
          wx.showLoading({ title: '删除中...' });
          api.order.delete(id)
            .then(() => {
              wx.showToast({ title: '已删除', icon: 'success' });
              this.getOrders();
            })
            .catch(err => {
              wx.showToast({ title: err.message || '删除失败', icon: 'none' });
            });
        }
      }
    });
  },

  deleteCancelledOrder(e) {
    this.deleteOrder(e);
  },

  viewLogistics(e) {
    const id = e.currentTarget.dataset.orderId || e.currentTarget.dataset.id;
    wx.navigateTo({
      url: `/user-pages/logistics-detail/logistics-detail?orderId=${id}`
    });
  },

  viewOrderDetail(e) {
    const id = e.currentTarget.dataset.orderId || e.currentTarget.dataset.id;
    wx.navigateTo({
      url: `/user-pages/orders-detail/orders-detail?id=${id}`
    });
  },

  viewDetail(e) {
    this.viewOrderDetail(e);
  },

  goToShop() {
    wx.reLaunch({
      url: '/pages/index/index'
    });
  },

  onPullDownRefresh() {
    this.getOrders();
    setTimeout(() => {
      wx.stopPullDownRefresh();
    }, 1000);
  }
});

