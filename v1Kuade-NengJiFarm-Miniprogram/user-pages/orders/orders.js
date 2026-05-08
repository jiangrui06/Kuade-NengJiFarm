const api = require('../../utils/api').api || require('../../utils/api');
const { orderTimer } = require('../../utils/order-timer');

const PAGE_SIZE = 10;

function safeDate(dateStr) {
  if (!dateStr) return new Date(0);
  const normalized = String(dateStr).replace(/-/g, '/');
  return new Date(normalized);
}

Page({
  data: {
    activeTab: 'all',
    currentOrderType: '',
    scrollToView: '',
    currentPage: 1,
    pageSize: PAGE_SIZE,
    totalOrders: 0,
    hasMore: true,
    loading: true,
    loadingMore: false,
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
    isRequesting: false,
    isPageVisible: false,
    orderCountdowns: {},
    hasLoaded: false  // 标记是否已加载过数据
  },

  searchTimer: null,
  countdownTimer: null,
  refreshTimer: null,

  onLoad(options) {
    this.initPageState();
    let tab = 'all';
    if (options.tab) tab = options.tab;
    this.setData({
      activeTab: tab,
      currentOrderType: '',
      scrollToView: 'tab-' + tab,
      isPageVisible: true
    });
    this.getOrders();
  },

  onShow() {
    this.initPageState();
    this.setData({ isPageVisible: true });
    this.startCountdownUpdate();
    this.startOrderRefresh();

    // 使用 hasLoaded 标志位避免重复请求
    if (this.data.hasLoaded) {
      // 已加载过数据，只刷新不重新请求
      if (this.data.orders.length > 0 && !this.data.isRequesting) {
        // 根据搜索关键词决定调用搜索或列表
        if (this.data.searchKeyword?.trim()) {
          this.searchOrders();
        } else {
          this.refreshOrders();
        }
      }
    } else {
      // 首次加载
      if (this.data.searchKeyword?.trim()) {
        this.searchOrders();
      } else {
        this.getOrders();
      }
    }
  },

  initPageState() {
    if (!this.processingTimeoutOrders) {
      this.processingTimeoutOrders = new Set();
    } else {
      this.processingTimeoutOrders.clear();
    }
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
      fail: () => { wx.reLaunch({ url: '/pages/index/index' }); }
    });
  },

  onSearchInput(e) {
    const keyword = e.detail.value;
    this.setData({ searchKeyword: keyword });
    this.searchOrders();
  },

  searchOrders() {
    const keyword = this.data.searchKeyword?.trim();
    if (!keyword) {
      this.getOrders();
      return;
    }

    if (this.data.isRequesting) return;
    this.setData({ isRequesting: true, loading: true, searching: true });

    let orderType = this.data.currentOrderType;
    let status = '';

    // 如果在特定类型标签页，使用标签页类型
    if (['food', 'acre', 'activity', 'cart'].includes(this.data.activeTab)) {
      orderType = this.data.activeTab;
      this.setData({ currentOrderType: orderType });
    }
    // 如果在状态标签页，设置状态过滤
    else if (this.data.activeTab !== 'all') {
      status = this.data.activeTab;
    }
    // 如果在"全部"标签页，根据关键词自动识别订单类型
    else {
      const lowerKeyword = keyword.toLowerCase();
      if (lowerKeyword.includes('点餐') || lowerKeyword.includes('菜品') || lowerKeyword.includes('dish')) {
        orderType = 'food';
      } else if (lowerKeyword.includes('商品') || lowerKeyword.includes('农场') || lowerKeyword.includes('优选') || lowerKeyword.includes('commodity')) {
        orderType = 'goods';
      } else if (lowerKeyword.includes('活动') || lowerKeyword.includes('报名') || lowerKeyword.includes('activity')) {
        orderType = 'activity';
      } else if (lowerKeyword.includes('认购') || lowerKeyword.includes('一亩田') || lowerKeyword.includes('acre')) {
        orderType = 'acre';
      }
    }

    const self = this;
    api.order.searchOrders({
      keyword,
      status: status || 'all',
      type: orderType || 'all',
      page: 1,
      pageSize: 20
    })
      .then((data) => {
        let list = [];
        if (data && data.list && Array.isArray(data.list)) {
          list = data.list;
        }

        const orders = list.map(order => ({
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

        self.initOrderCountdowns(orders);
        orderTimer.cleanExpiredRecords();

        self.setData({
          allOrders: [],
          orders,
          noSearchResult: orders.length === 0,
          loading: false,
          searching: false,
          isRequesting: false
        });
      })
      .catch((err) => {
        console.error('搜索订单失败:', err);
        self.setData({
          loading: false,
          searching: false,
          isRequesting: false
        });
      });
  },

  processImageUrl(imageUrl) {
    if (!imageUrl) return '';
    const baseUrl = 'http://192.168.203.56';
    let normalized = String(imageUrl).replace(/[`\s]/g, '');

    // 如果已经是完整的 API 地址，直接返回
    if (normalized.startsWith(baseUrl + '/api/file/')) {
      return normalized;
    }

    // 如果是相对路径的 API 地址，补全 baseUrl
    if (normalized.startsWith('/api/file/')) {
      return baseUrl + normalized;
    }

    // 提取文件名
    let fileName = '';
    if (normalized.includes('/images/farm/')) {
      fileName = normalized.split('/images/farm/').pop() || '';
    } else if (normalized.includes('/farm/')) {
      fileName = normalized.split('/farm/').pop() || '';
    } else {
      fileName = normalized.split('/').filter(Boolean).pop() || '';
    }

    if (!fileName) return '';

    // 去除可能存在的 URL 参数或锚点
    fileName = fileName.split(/[?#]/)[0];

    // 根据后缀判断类型并映射到正确的 API 接口
    const lowerName = fileName.toLowerCase();
    const isVideo = ['.mp4', '.mov', '.avi', '.mkv', '.wmv'].some(ext => lowerName.endsWith(ext));

    return isVideo
      ? `${baseUrl}/api/file/video/${fileName}`
      : `${baseUrl}/api/file/image/${fileName}`;
  },

  _normalizeOrderList(data) {
    if (!data) return { orders: [], total: 0, totalPages: 0 };
    if (Array.isArray(data)) return { orders: data, total: data.length, totalPages: 1 };

    // 支持多种字段名：orders、list、records、items
    let orders = null;
    let total = 0;
    let page = 1;
    let pageSize = PAGE_SIZE;
    let totalPages = 0;

    // 优先级1: data.orders
    if (data.orders && Array.isArray(data.orders)) {
      orders = data.orders;
      total = data.total !== undefined && data.total !== null ? data.total : data.orders.length;
      page = data.page || 1;
      pageSize = data.pageSize || PAGE_SIZE;
      totalPages = data.totalPages !== undefined && data.totalPages !== null
        ? data.totalPages
        : Math.max(Math.ceil(total / pageSize) || 1, 1);
    }
    // 优先级2: data.list
    else if (data.list && Array.isArray(data.list)) {
      orders = data.list;
      total = data.total !== undefined && data.total !== null ? data.total : data.list.length;
      page = data.page || 1;
      pageSize = data.pageSize || PAGE_SIZE;
      totalPages = data.totalPages !== undefined && data.totalPages !== null
        ? data.totalPages
        : Math.max(Math.ceil(total / pageSize) || 1, 1);
    }
    // 优先级3: data.records
    else if (data.records && Array.isArray(data.records)) {
      orders = data.records;
      total = data.total !== undefined && data.total !== null ? data.total : data.records.length;
      page = data.page || 1;
      pageSize = data.pageSize || PAGE_SIZE;
      totalPages = data.totalPages !== undefined && data.totalPages !== null
        ? data.totalPages
        : Math.max(Math.ceil(total / pageSize) || 1, 1);
    }
    // 优先级4: data.items
    else if (data.items && Array.isArray(data.items)) {
      orders = data.items;
      total = data.total !== undefined && data.total !== null ? data.total : data.items.length;
      page = data.page || 1;
      pageSize = data.pageSize || PAGE_SIZE;
      totalPages = data.totalPages !== undefined && data.totalPages !== null
        ? data.totalPages
        : Math.max(Math.ceil(total / pageSize) || 1, 1);
    }
    // 优先级5: data.data.orders
    else if (data.data && data.data.orders && Array.isArray(data.data.orders)) {
      orders = data.data.orders;
      total = data.data.total !== undefined && data.data.total !== null ? data.data.total : data.data.orders.length;
      page = data.data.page || 1;
      pageSize = data.data.pageSize || PAGE_SIZE;
      totalPages = data.data.totalPages || Math.max(Math.ceil(total / pageSize) || 1, 1);
    }
    // 优先级6: data.data.list
    else if (data.data && data.data.list && Array.isArray(data.data.list)) {
      orders = data.data.list;
      total = data.data.total !== undefined && data.data.total !== null ? data.data.total : data.data.list.length;
      page = data.data.page || 1;
      pageSize = data.data.pageSize || PAGE_SIZE;
      totalPages = data.data.totalPages || Math.max(Math.ceil(total / pageSize) || 1, 1);
    }
    // 优先级7: data.data.records
    else if (data.data && data.data.records && Array.isArray(data.data.records)) {
      orders = data.data.records;
      total = data.data.total !== undefined && data.data.total !== null ? data.data.total : data.data.records.length;
      page = data.data.page || 1;
      pageSize = data.data.pageSize || PAGE_SIZE;
      totalPages = data.data.totalPages || Math.max(Math.ceil(total / pageSize) || 1, 1);
    }
    // 优先级8: data.data (直接是数组)
    else if (data.data && Array.isArray(data.data)) {
      orders = data.data;
      total = data.data.length;
      page = 1;
      pageSize = PAGE_SIZE;
      totalPages = 1;
    }

    if (orders) {
      return { orders, total, page, pageSize, totalPages };
    }

    return { orders: [], total: 0, totalPages: 0 };
  },

  _mapOrder(order) {
    let typeText = order.typeText || '订单';
    // 标准化 type 字段为小写，处理大小写不一致的情况
    const type = (order.type || '').toString().toLowerCase();

    // 根据 type 字段重新设置 typeText，确保搜索功能正常
    if (type === 'goods') typeText = '商品订单';
    else if (type === 'food') typeText = '点餐订单';
    else if (type === 'activity') typeText = '活动订单';
    else if (type === 'acre') typeText = '认购订单';
    // 如果没有 type 字段，才使用原有的 typeText
    else if (!type && order.typeText) typeText = order.typeText;
    const tableNo = order.diningTableNo || order.tableNumber || order.tableNo || order.dining_table_no || '';
    return {
      ...order,
      id: order.id || order.orderId,
      orderNumber: order.orderNumber || order.orderNo,
      typeText: typeText,
      tableNo: tableNo,
      totalPrice: (order.totalPrice || order.totalAmount || 0).toString().replace(/[¥￥]/g, ''),
      statusText: order.statusText,
      verified: order.verified || false,  // 核销状态
      items: (order.items || []).map(item => ({
        ...item,
        image: this.processImageUrl(item.image),
        price: (item.price || item.unitPrice || 0).toString().replace(/[¥￥]/g, '')
      }))
    };
  },

  // ======== 分页加载核心 ========

  // 首次加载 / 切换tab
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

    const self = this;
    api.order.getList({
      type: orderType || 'all',
      status: status || 'all',
      page: 1,
      pageSize: PAGE_SIZE,
      sortBy: 'createTime',
      sortOrder: 'desc'
    })
      .then((data) => {
        let ordersData = [];
        let total = 0;
        if (data && data.orders && Array.isArray(data.orders)) {
          ordersData = data.orders;
          total = data.total || data.orders.length;
        } else if (data && Array.isArray(data)) {
          ordersData = data;
          total = data.length;
        }

        const allOrders = ordersData.map(order => ({
          ...order,
          totalPrice: order.totalPrice ? order.totalPrice.toString().replace(/[¥￥]/g, '') : order.totalPrice,
          items: (order.items || []).map(item => ({
            ...item,
            image: self.processImageUrl(item.image),
            price: item.price ? item.price.toString().replace(/[¥￥]/g, '') : item.price
          }))
        }));

        self.initOrderCountdowns(allOrders);
        orderTimer.cleanExpiredRecords();

        self.setData({
          allOrders: allOrders,
          orders: allOrders,
          noSearchResult: false,
          loading: false,
          searching: false,
          isRequesting: false,
          hasLoaded: true
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

  // 静默刷新
  refreshOrders() {
    if (this.data.isRequesting) return;
    this.setData({ isRequesting: true });
    // 如果有搜索关键词，使用搜索API
    if (this.data.searchKeyword?.trim()) {
      this.searchOrders();
    } else {
      this.getOrders();
    }
  },

  // 加载下一页
  loadNextPage() {
    const nextPage = this.data.currentPage + 1;
    if (this.data.isRequesting || !this.data.hasMore) return;

    this.setData({ loadingMore: true, isRequesting: true });

    // 使用原有分页逻辑
    let params = { page: nextPage, pageSize: PAGE_SIZE };
    const activeTab = this.data.activeTab;
    if (activeTab !== 'all') params.status = activeTab;

    const self = this;
    api.order.getList(params)
      .then((responseData) => {
        const { orders: rawOrders, total } = self._normalizeOrderList(responseData);

        if (rawOrders.length === 0) {
          self.setData({ hasMore: false, loadingMore: false, isRequesting: false });
          return;
        }

        const mappedOrders = rawOrders.map(order => self._mapOrder(order));

        const existingIds = new Set(self.data.allOrders.map(o => o.id));
        const newOrders = mappedOrders.filter(o => !existingIds.has(o.id));
        const newAllOrders = [...self.data.allOrders, ...newOrders];
        newAllOrders.sort((a, b) => safeDate(b.createTime) - safeDate(a.createTime));

        const hasMore = total > nextPage * PAGE_SIZE;

        self.initOrderCountdowns(newAllOrders);

        self.setData({
          allOrders: newAllOrders,
          orders: newAllOrders.slice(0, nextPage * PAGE_SIZE),
          totalOrders: total,
          currentPage: nextPage,
          hasMore: hasMore,
          loadingMore: false,
          isRequesting: false,
          noSearchResult: false
        });
      })
      .catch((err) => {
        console.error('加载下一页失败:', err);
        self.setData({ loadingMore: false, isRequesting: false });
      });
  },

  // 加载上一页（直接从 allOrders 缓存中取）
  loadPrevPage() {
    const prevPage = this.data.currentPage - 1;
    if (prevPage < 1) return;

    const displayOrders = this.data.allOrders.slice(0, prevPage * PAGE_SIZE);

    this.setData({
      currentPage: prevPage,
      orders: displayOrders,
      loadingMore: false,
      isRequesting: false
    });
    wx.pageScrollTo({ scrollTop: 0, duration: 200 });
  },

  // 触底自动加载（只触发下一页，不触发上一页）
  onReachBottom() {
    if (!this.data.hasMore || this.data.isRequesting || this.data.loadingMore) return;

    // 搜索时不支持分页加载（搜索结果由后端返回，不支持翻页）
    if (this.data.searchKeyword && this.data.searchKeyword.trim()) {
      return;
    }

    // 只有当当前已显示数据达到当前页的末端时才触发
    const loadedCount = this.data.orders.length;
    const currentMax = this.data.currentPage * PAGE_SIZE;
    if (loadedCount >= currentMax && loadedCount > 0) {
      this.loadNextPage();
    }
  },

  switchTab(e) {
    const tab = e.currentTarget.dataset.tab;
    if (tab === this.data.activeTab) return;

    let newCurrentOrderType = '';
    if (['food', 'acre', 'activity', 'cart'].includes(tab)) {
      newCurrentOrderType = tab;
    }

    this.setData({
      activeTab: tab,
      currentOrderType: newCurrentOrderType,
      loading: true,
      scrollToView: 'tab-' + tab
    });
    if (this.data.searchKeyword?.trim()) {
      this.searchOrders();
    } else {
      this.getOrders();
    }
  },

  initOrderCountdowns(orders) {
    const orderCountdowns = {};
    orders.forEach(order => {
      if (order.status === 'pending' || order.status === 'pending_payment') {
        const remaining = orderTimer.getRemainingTime(order.createTime);
        orderCountdowns[order.id] = remaining > 0 ? orderTimer.formatTime(remaining) : '00:00';
        if (remaining <= 0) this.handleOrderTimeout(order.id);
      }
    });
    this.setData({ orderCountdowns });
  },

  startCountdownUpdate() {
    this.stopCountdownUpdate();
    this.countdownTimer = setInterval(() => this.updateCountdowns(), 1000);
  },

  updateCountdowns() {
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
        if (remaining <= 0) this.handleOrderTimeout(order.id);
      }
    });
    if (hasChange) this.setData({ orderCountdowns: nextCountdowns });
  },

  stopCountdownUpdate() {
    if (this.countdownTimer) { clearInterval(this.countdownTimer); this.countdownTimer = null; }
  },

  handleOrderTimeout(orderId) {
    if (this.processingTimeoutOrders && this.processingTimeoutOrders.has(orderId)) return;
    this.processingTimeoutOrders.add(orderId);
    orderTimer.handleTimeout(orderId, (id) => { this.getOrders(); });
  },

  startOrderRefresh() {
    this.stopOrderRefresh();
    this.refreshTimer = setInterval(() => {
      if (!this.data.isRequesting && this.data.isPageVisible) this.refreshOrders();
    }, 15000);
  },

  stopOrderRefresh() {
    if (this.refreshTimer) { clearInterval(this.refreshTimer); this.refreshTimer = null; }
  },

  payOrder(e) {
    const id = e.currentTarget.dataset.orderId || e.currentTarget.dataset.id;
    wx.navigateTo({ url: `/user-pages/pay/pay?orderId=${id}&type=${e.currentTarget.dataset.type || 'goods'}` });
  },

  deleteCancelledOrder(e) {
    const id = e.currentTarget.dataset.orderId || e.currentTarget.dataset.id;
    wx.showModal({
      title: '删除订单',
      content: '确定要删除这个订单吗？',
      success: (res) => {
        if (res.confirm) {
          api.order.delete(id).then(() => {
            wx.showToast({ title: '订单已删除', icon: 'success' });
            this.getOrders();
          }).catch(err => wx.showToast({ title: err.message || '删除失败', icon: 'none' }));
        }
      }
    });
  },

  deleteOrder(e) {
    const id = e.currentTarget.dataset.orderId || e.currentTarget.dataset.id;
    const order = this.data.orders.find(o => o.id === id);
    if (!order) {
      wx.showToast({ title: '订单不存在', icon: 'none' });
      return;
    }

    // 检查是否超时
    const remaining = orderTimer.getRemainingTime(order.createTime);
    if (remaining <= 0) {
      wx.showModal({
        title: '订单已超时',
        content: '该订单已超时未支付，确定要删除吗？',
        success: (res) => {
          if (res.confirm) {
            api.order.delete(id).then(() => {
              wx.showToast({ title: '订单已删除', icon: 'success' });
              this.getOrders();
            }).catch(err => wx.showToast({ title: err.message || '删除失败', icon: 'none' }));
          }
        }
      });
    } else {
      wx.showModal({
        title: '删除订单',
        content: '确定要删除这个待付款订单吗？',
        success: (res) => {
          if (res.confirm) {
            api.order.delete(id).then(() => {
              wx.showToast({ title: '订单已删除', icon: 'success' });
              this.getOrders();
            }).catch(err => wx.showToast({ title: err.message || '删除失败', icon: 'none' }));
          }
        }
      });
    }
  },

  viewOrderDetail(e) {
    const id = e.currentTarget.dataset.orderId || e.currentTarget.dataset.id;
    wx.navigateTo({ url: `/user-pages/orders-detail/orders-detail?id=${id}` });
  },

  goToShop() { wx.reLaunch({ url: '/pages/index/index' }); },

  onPullDownRefresh() {
    if (this.data.searchKeyword?.trim()) {
      this.searchOrders();
    } else {
      this.getOrders();
    }
    setTimeout(() => { wx.stopPullDownRefresh(); }, 1000);
  }
});
