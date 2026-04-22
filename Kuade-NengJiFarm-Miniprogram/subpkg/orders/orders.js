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
    // 防御性初始化，防止 getOrders 在 onLoad 完成前被调用
    if (!this.processingTimeoutOrders) this.processingTimeoutOrders = new Set();
    if (!this.deletedTimeoutOrderIds) this.deletedTimeoutOrderIds = new Set();
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
    if (this.data.isPageVisible) {
      this.getOrders();
    }
    this.setData({ isPageVisible: true });
    this.startCountdownUpdate();
    this.startOrderRefresh();
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
        
        const deletedIds = (self.deletedTimeoutOrderIds instanceof Set)
          ? self.deletedTimeoutOrderIds
          : new Set();
        ordersData = ordersData.filter(order => {
          const orderIdStr = String(order.id);
          const isDeleted = deletedIds.has(orderIdStr);
          if (isDeleted) {
            console.log('过滤掉已删除订单:', { 
              id: order.id, 
              idStr: orderIdStr,
              type: order.type 
            });
          }
          return !isDeleted;
        });
        console.log('过滤已删除订单后:', ordersData.length, '个订单');
        
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
  },

  startCountdownUpdate() {
    this.stopCountdownUpdate();
    this.countdownTimer = setInterval(() => {
      this.updateCountdowns();
    }, 1000);
  },

  stopCountdownUpdate() {
    if (this.countdownTimer) {
      clearInterval(this.countdownTimer);
      this.countdownTimer = null;
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

  handleOrderTimeout(orderId) {
    const orderIdStr = String(orderId);
    console.log('开始处理超时订单:', { orderId, orderIdStr });
    
    if (!this.processingTimeoutOrders || !(this.processingTimeoutOrders instanceof Set)) {
      this.processingTimeoutOrders = new Set();
    }
    if (!this.deletedTimeoutOrderIds || !(this.deletedTimeoutOrderIds instanceof Set)) {
      this.deletedTimeoutOrderIds = new Set();
    }
    
    if (this.processingTimeoutOrders.has(orderIdStr)) {
      console.log('订单已在处理中，跳过:', { orderId, orderIdStr });
      return;
    }
    
    if (this.deletedTimeoutOrderIds.has(orderIdStr)) {
      console.log('订单已删除过，跳过:', { orderId, orderIdStr });
      return;
    }
    
    this.processingTimeoutOrders.add(orderIdStr);
    this.deletedTimeoutOrderIds.add(orderIdStr);
    
    // 先从界面移除订单
    const newAllOrders = this.data.allOrders.filter(order => String(order.id) !== orderIdStr);
    const filteredOrders = this.filterOrders(newAllOrders, this.data.searchKeyword);
    
    const newCountdowns = { ...this.data.orderCountdowns };
    delete newCountdowns[orderIdStr];
    
    console.log('更新界面，移除订单');
    this.setData({
      allOrders: newAllOrders,
      orders: filteredOrders,
      orderCountdowns: newCountdowns
    });
    
    orderTimer.clearTimer(orderId);
    
    console.log('调用 API 处理订单');
    api.order.updateStatus(orderId, 'cancelled')
      .then(() => {
        console.log(`订单 ${orderId} 自动取消成功`);
        // 尝试删除订单，但即使删除失败也没关系
        return api.order.delete(orderId)
          .then(() => {
            console.log(`订单 ${orderId} 自动删除成功`);
          })
          .catch((deleteErr) => {
            console.warn(`订单 ${orderId} 删除失败，但已成功取消:`, deleteErr);
            // 删除失败不算错误，因为订单已经取消了
          });
      })
      .catch((err) => {
        // 如果订单不存在（404），说明已经被处理了，不算错误
        if (err && err.code === 404) {
          console.log(`订单 ${orderId} 不存在，可能已被删除`);
        } else {
          console.error(`订单 ${orderId} 取消失败:`, err);
        }
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
    if (['pending', 'paid', 'shipping', 'review', 'refund'].includes(tab)) {
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
