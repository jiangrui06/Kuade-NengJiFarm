const { api } = require('../../utils/api');
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
    orderCountdowns: {},
    cancelledCountdowns: {}
  },
  
  searchTimer: null,
  countdownTimer: null,
  cancelledTimer: null,
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

  processImageUrl: function (imageUrl) {
    if (!imageUrl) return '';
    imageUrl = imageUrl.replace(/[`\s]/g, '');
    
    if (imageUrl.startsWith('http://') || imageUrl.startsWith('https://')) {
      if (imageUrl.includes('192.168.203.56')) {
        imageUrl = imageUrl.replace('192.168.203.56', '192.168.203.56');
      }
      return imageUrl;
    }
    
    const baseUrl = 'http://192.168.203.56';
    if (!imageUrl.startsWith('/')) {
      imageUrl = '/' + imageUrl;
    }
    return baseUrl + imageUrl;
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

    let orderType = this.data.currentOrderType;
    let status = '';
    
    if (['food', 'acre', 'activity', 'cart'].includes(this.data.activeTab)) {
      orderType = this.data.activeTab;
      this.setData({ currentOrderType: orderType });
    } else if (this.data.activeTab !== 'all') {
      status = this.data.activeTab;
    }

    console.log('获取订单列表参数:', {
      type: orderType,
      status: status,
      activeTab: this.data.activeTab,
      currentOrderType: this.data.currentOrderType
    });

    const self = this;
    api.order.getList({
      type: orderType,
      status: status,
      page: 1,
      pageSize: 10,
      sortBy: 'createTime',
      sortOrder: 'desc'
    })
      .then((data) => {
        console.log('获取订单列表成功，数据:', data);
        let ordersData = [];
        if (data && data.orders && Array.isArray(data.orders)) {
          ordersData = data.orders;
        } else if (data && Array.isArray(data)) {
          ordersData = data;
        }
        
        console.log('所有订单详情:');
        ordersData.forEach((order, index) => {
          console.log(`订单${index}:`, {
            id: order.id,
            idType: typeof order.id,
            type: order.type,
            typeText: order.typeText,
            status: order.status,
            orderNumber: order.orderNumber,
            createTime: order.createTime
          });
        });

        const allOrders = ordersData.map(order => ({
          ...order,
          type: order.type,
          typeText: order.typeText,
          statusText: order.statusText,
          orderNumber: order.orderNumber,
          totalPrice: order.totalPrice ? order.totalPrice.toString().replace(/[¥￥]/g, '') : order.totalPrice,
          items: (order.items || []).map(item => ({
            ...item,
            image: self.processImageUrl(item.image),
            price: item.price ? item.price.toString().replace(/[¥￥]/g, '') : item.price
          }))
        }));
        
        self.initOrderCountdowns(allOrders);

        // 自动清理超期已取消订单（超过24小时未删除的）
        self.autoCleanExpiredCancelledOrders(ordersData);

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
        wx.showToast({
          title: '获取订单失败，请重试',
          icon: 'none'
        });
      });
  },

  initOrderCountdowns(orders) {
    const self = this;
    const orderCountdowns = {};
    orders.forEach(order => {
      if (order.status === 'pending') {
        const remaining = orderTimer.getRemainingTime(order.createTime);
        const orderIdStr = String(order.id);
        orderCountdowns[orderIdStr] = {
          remaining: remaining,
          text: orderTimer.formatTime(remaining)
        };
        
        console.log('初始化订单倒计时:', {
          orderId: order.id,
          orderIdStr: orderIdStr,
          type: order.type,
          typeText: order.typeText,
          remaining: remaining,
          createTime: order.createTime
        });
        
        orderTimer.startTimer(order.id, order.createTime, (orderId) => {
          console.log('定时器触发超时:', orderId);
          self.handleOrderTimeout(orderId);
        });
      }
    });
    this.setData({ orderCountdowns });
    // 初始化已取消订单的自动删除倒计时
    this.initCancelledCountdowns(orders);
  },

  // 初始化已取消订单的剩余删除倒计时
  initCancelledCountdowns(orders) {
    const cancelledCountdowns = {};
    orders.forEach(order => {
      if (order.status === 'cancelled') {
        const orderIdStr = String(order.id);
        const localTime = orderTimer.getLocalCancelledTime(order.id);
        const remaining = orderTimer.getCancelledRemainingTime(localTime);
        cancelledCountdowns[orderIdStr] = {
          remaining: remaining,
          text: this.formatCancelledRemaining(remaining)
        };
      }
    });
    this.setData({ cancelledCountdowns });
  },

  startCountdownUpdate() {
    this.stopCountdownUpdate();
    // 待支付订单需要秒级倒计时
    this.countdownTimer = setInterval(() => {
      this.updateCountdowns();
    }, 1000);
    // 已取消订单只需分钟级更新，30秒刷新一次
    this.cancelledTimer = setInterval(() => {
      this.updateCancelledCountdowns();
    }, 30000);
  },

  stopCountdownUpdate() {
    if (this.countdownTimer) {
      clearInterval(this.countdownTimer);
      this.countdownTimer = null;
    }
    if (this.cancelledTimer) {
      clearInterval(this.cancelledTimer);
      this.cancelledTimer = null;
    }
  },

  startOrderRefresh() {
    const self = this;
    this.stopOrderRefresh();
    this.refreshTimer = setInterval(() => {
      if (self.data.isPageVisible && !self.data.isRequesting) {
        console.log('自动刷新订单列表');
        self.getOrders();
      }
    }, 30000);
  },

  stopOrderRefresh() {
    if (this.refreshTimer) {
      clearInterval(this.refreshTimer);
      this.refreshTimer = null;
    }
  },

  updateCountdowns() {
    const self = this;
    const { allOrders, orderCountdowns } = this.data;
    const newCountdowns = { ...orderCountdowns };
    let needUpdate = false;
    const timeoutOrderIds = [];
    
    allOrders.forEach(order => {
      const orderIdStr = String(order.id);
      if (order.status === 'pending' && newCountdowns[orderIdStr]) {
        const remaining = orderTimer.getRemainingTime(order.createTime);
        newCountdowns[orderIdStr] = {
          remaining: remaining,
          text: orderTimer.formatTime(remaining)
        };
        needUpdate = true;
        
        if (remaining <= 0) {
          console.log('检测到超时订单:', {
            orderId: order.id,
            orderIdStr: orderIdStr,
            type: order.type,
            typeText: order.typeText,
            orderNumber: order.orderNumber
          });
          timeoutOrderIds.push(order.id);
        }
      }
    });
    
    if (needUpdate) {
      this.setData({ orderCountdowns: newCountdowns });
    }
    
    if (timeoutOrderIds.length > 0) {
      console.log('检测到超时订单列表:', timeoutOrderIds);
      timeoutOrderIds.forEach(orderId => {
        setTimeout(() => {
          self.handleOrderTimeout(orderId);
        }, 0);
      });
    }
  },

  // 每秒更新已取消订单的剩余删除时间
  updateCancelledCountdowns() {
    const { allOrders, cancelledCountdowns } = this.data;
    const newCountdowns = { ...cancelledCountdowns };
    let needUpdate = false;

    allOrders.forEach(order => {
      if (order.status === 'cancelled') {
        const orderIdStr = String(order.id);
        const localTime = orderTimer.getLocalCancelledTime(order.id);
        // 有本地记录才显示倒计时，否则不显示
        if (localTime) {
          const remaining = orderTimer.getCancelledRemainingTime(localTime);
          newCountdowns[orderIdStr] = {
            remaining: remaining,
            text: this.formatCancelledRemaining(remaining)
          };
          needUpdate = true;
          // 倒计时归零时自动触发清理
          if (remaining <= 0) {
            this.autoCleanExpiredCancelledOrders(allOrders.filter(o => o.id === order.id));
          }
        }
      }
    });

    if (needUpdate) {
      this.setData({ cancelledCountdowns: newCountdowns });
    }
  },

  // 格式化已取消订单剩余时间（X分钟后自动删除）
  formatCancelledRemaining(ms) {
    if (ms <= 0) return '即将自动删除';
    const totalMinutes = Math.ceil(ms / (60 * 1000));
    return `${totalMinutes}分钟后自动删除`;
  },

  handleOrderTimeout(orderId) {
    const orderIdStr = String(orderId);
    console.log('开始处理超时订单:', { orderId, orderIdStr });
    
    if (!this.processingTimeoutOrders || !(this.processingTimeoutOrders instanceof Set)) {
      this.processingTimeoutOrders = new Set();
    }
    
    if (this.processingTimeoutOrders.has(orderIdStr)) {
      console.log('订单已在处理中，跳过:', { orderId, orderIdStr });
      return;
    }
    
    this.processingTimeoutOrders.add(orderIdStr);
    
    // 不立即从界面移除，等待取消成功后刷新列表
    orderTimer.clearTimer(orderId);
    
    console.log('调用 API 取消订单');
    api.order.updateStatus(orderId, 'cancelled')
      .then(() => {
        console.log(`订单 ${orderId} 自动取消成功`);
        // 取消成功后刷新订单列表，显示已取消状态
        this.getOrders();
      })
      .catch((err) => {
        // 如果订单不存在（404），说明已经被处理了，不算错误
        if (err && err.code === 404) {
          console.log(`订单 ${orderId} 不存在，可能已被删除`);
        } else {
          console.error(`订单 ${orderId} 取消失败:`, err);
        }
        // 无论成功或失败，都刷新一下列表
        this.getOrders();
      })
      .finally(() => {
        if (this.processingTimeoutOrders) {
          this.processingTimeoutOrders.delete(orderIdStr);
        }
        console.log('订单处理完成');
      });
  },

  switchTab(e) {
    const tab = e.currentTarget.dataset.tab;
    if (tab === this.data.activeTab) return;
    
    let newCurrentOrderType = this.data.currentOrderType;
    if (['pending', 'paid', 'shipping', 'cancelled', 'review', 'refund'].includes(tab)) {
      newCurrentOrderType = '';
    } else if (['food', 'acre', 'activity', 'cart'].includes(tab)) {
      newCurrentOrderType = tab;
    }
    
    this.setData({ 
      activeTab: tab, 
      currentOrderType: newCurrentOrderType,
      loading: true,
      scrollToView: 'tab-' + tab
    });
    this.getOrders();
  },

  viewOrderDetail(e) {
    const orderId = e.currentTarget.dataset.orderId;
    wx.navigateTo({
      url: `/subpkg/orders-detail/orders-detail?id=${orderId}`
    });
  },

  payOrder(e) {
    const orderId = e.currentTarget.dataset.orderId;
    wx.navigateTo({
      url: `/subpkg/pay/pay?orderId=${orderId}`
    });
  },

  // 删除已取消的订单
  deleteCancelledOrder(e) {
    const orderId = e.currentTarget.dataset.orderId;
    wx.showModal({
      title: '确认删除',
      content: '确定要删除这个已取消的订单吗？删除后将无法恢复。',
      confirmText: '删除',
      cancelText: '取消',
      confirmColor: '#e64340',
      success: (res) => {
        if (res.confirm) {
          wx.showLoading({ title: '删除中...' });
          api.order.delete(orderId)
            .then(() => {
              // 清理本地的取消时间记录
              orderTimer.removeCancelledTime(orderId);
              wx.showToast({ title: '订单已删除', icon: 'success' });
              this.getOrders();
            })
            .catch((err) => {
              console.error('删除已取消订单失败:', err);
              wx.showToast({ title: '删除失败，请重试', icon: 'none' });
            })
            .finally(() => {
              wx.hideLoading();
            });
        }
      }
    });
  },

  deleteOrder(e) {
    const orderId = e.currentTarget.dataset.orderId;
    
    const targetOrder = this.data.allOrders.find(order => order.id === orderId);
    const isOrderTimeout = targetOrder ? orderTimer.getRemainingTime(targetOrder.createTime) <= 0 : false;
    
    wx.showModal({
      title: '确认删除',
      content: '确定要删除这个待付款订单吗？',
      confirmText: '删除',
      cancelText: '取消',
      confirmColor: '#ff4d4f',
      success: (res) => {
        if (res.confirm) {
          // 先清除计时器，防止删除后还提示超时
          orderTimer.clearTimer(orderId);
          
          wx.showLoading({ title: '删除中...' });
          
          api.order.delete(orderId)
            .then(() => {
              const toastTitle = isOrderTimeout ? '订单已超时' : '删除成功';
              wx.showToast({
                title: toastTitle,
                icon: isOrderTimeout ? 'none' : 'success'
              });
              this.getOrders();
            })
            .catch((err) => {
              console.error('删除订单失败:', err);
              wx.showToast({
                title: '删除失败，请稍后重试',
                icon: 'none'
              });
            })
            .finally(() => {
              wx.hideLoading();
            });
        }
      }
    });
  },

  // 自动清理超期已取消订单（超过10分钟未删除的）
  autoCleanExpiredCancelledOrders(orders) {
    const self = this;
    // 先清理过期的本地缓存记录
    orderTimer.cleanExpiredRecords();

    const expiredOrders = orders.filter(order => {
      if (order.status !== 'cancelled') return false;
      // 优先使用本地 Storage 记录的取消时间判断
      const cancelledTime = order.cancelTime || order.updateTime || order.createTime;
      return orderTimer.isCancelledOrderExpired(order.id, cancelledTime);
    });

    if (expiredOrders.length === 0) return;

    console.log('发现超期已取消订单，自动清理:', expiredOrders.length, '条');
    expiredOrders.forEach(order => {
      console.log(`自动删除超期订单: ${order.id} - ${order.orderNumber}`);
      api.order.delete(order.id)
        .then(() => {
          console.log(`超期已取消订单 ${order.id} 自动删除成功`);
          // 清理本地的取消时间记录
          orderTimer.removeCancelledTime(order.id);
        })
        .catch((err) => {
          console.warn(`超期已取消订单 ${order.id} 删除失败:`, err);
        });
    });
  },

  goToShop() {
    wx.switchTab({
      url: '/pages/index/index'
    });
  },

  goBack() {
    wx.switchTab({
      url: '/pages/index/index'
    });
  },

  // 下拉刷新
  onPullDownRefresh() {
    console.log('下拉刷新订单列表');
    this.getOrders();
    // 刷新完成后停止下拉刷新
    setTimeout(() => {
      wx.stopPullDownRefresh();
    }, 1000);
  }
});
