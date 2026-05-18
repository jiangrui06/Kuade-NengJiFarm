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
    // 状态标签
    statusTabs: [
      { key: 'all', name: '全部' },
      { key: 'pending', name: '待付款' },
      { key: 'paid', name: '待处理' },
      { key: 'shipping', name: '待收货' },
      { key: 'cancelled', name: '已取消' },
      { key: 'refund', name: '退款/售后' },
      { key: 'completed', name: '已完成' }
    ],
    // 订单类型标签
    typeTabs: [
      { key: 'all', name: '全部类型' },
      { key: 'goods', name: '商品' },
      { key: 'food', name: '点餐' },
      { key: 'activity', name: '活动' }
    ],
    activeTypeTab: 'all',
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
  debounceTimer: null, // 搜索防抖定时器

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

    // 使用 hasLoaded 标志位避免重复请求
    if (this.data.hasLoaded && this.data.orders.length > 0) {
      // 已加载过数据且有订单，只刷新不重新请求
      if (!this.data.isRequesting) {
        // 根据搜索关键词决定调用搜索或列表
        if (this.data.searchKeyword?.trim()) {
          this.searchOrders();
        } else {
          this.refreshOrders();
        }
      }
    } else {
      // 首次加载或没有订单数据
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
    // 清除防抖定时器
    if (this.debounceTimer) {
      clearTimeout(this.debounceTimer);
      this.debounceTimer = null;
    }
  },

  onUnload() {
    this.stopCountdownUpdate();
    // 清除防抖定时器
    if (this.debounceTimer) {
      clearTimeout(this.debounceTimer);
      this.debounceTimer = null;
    }
  },

  goBack() {
    wx.navigateBack({
      fail: () => { wx.reLaunch({ url: '/pages/index/index' }); }
    });
  },

  onSearchInput(e) {
    const keyword = e.detail.value;
    this.setData({ searchKeyword: keyword });

    // 清除之前的防抖定时器
    if (this.debounceTimer) {
      clearTimeout(this.debounceTimer);
      this.debounceTimer = null;
    }

    // 防抖处理：300ms后执行搜索
    // 只有当关键词不为空时才执行搜索
    if (keyword && keyword.trim()) {
      this.debounceTimer = setTimeout(() => {
        this.searchOrders();
      }, 300);
    } else {
      // 如果关键词为空，立即清除搜索状态
      this.setData({
        searching: false,
        noSearchResult: false,
        hasMore: true
      });
    }
  },

  searchOrders({ _isPullRefresh } = {}) {
    const keyword = this.data.searchKeyword?.trim();
    if (!keyword) {
      // 清除搜索状态，重新加载订单列表
      this.setData({
        searchKeyword: '',
        searching: false,
        noSearchResult: false,
        allOrders: [],
        orders: [],
        currentPage: 1,
        hasMore: true,
        isRequesting: false
      });
      return this.getOrders({ _isPullRefresh });
    }

    // 防抖处理：如果正在请求中，先清除之前的请求状态
    if (this.data.isRequesting) {
      return Promise.resolve();
    }

    // 下拉刷新时不切换 loading，避免 scroll-view 布局抖动
    if (_isPullRefresh) {
      this.setData({ isRequesting: true, searching: true });
    } else {
      this.setData({ isRequesting: true, loading: true, searching: true });
    }

    // 确定订单类型：优先使用类型标签，其次使用状态标签中的类型
    let orderType = this.data.activeTypeTab !== 'all' ? this.data.activeTypeTab : this.data.currentOrderType;
    let status = '';

    // 如果在特定类型标签页，使用标签页类型
    if (['food', 'activity', 'cart'].includes(this.data.activeTab)) {
      orderType = this.data.activeTab;
      this.setData({ currentOrderType: orderType });
    }
    // 如果在状态标签页，设置状态过滤
    else if (this.data.activeTab !== 'all') {
      status = this.data.activeTab === 'paid' ? 'paid,ordered'
        : this.data.activeTab === 'refund' ? 'refunding,refunded'
        : this.data.activeTab;
    }
    // 如果在"全部"标签页，根据关键词自动识别订单类型（仅当没有选择类型标签时）
    else if (this.data.activeTypeTab === 'all') {
      const lowerKeyword = keyword.toLowerCase();
      if (lowerKeyword.includes('点餐') || lowerKeyword.includes('菜品') || lowerKeyword.includes('dish')) {
        orderType = 'food';
      } else if (lowerKeyword.includes('商品') || lowerKeyword.includes('农场') || lowerKeyword.includes('优选') || lowerKeyword.includes('commodity')) {
        orderType = 'goods';
      } else if (lowerKeyword.includes('活动') || lowerKeyword.includes('报名') || lowerKeyword.includes('activity')) {
        orderType = 'activity';
      }
    }

    const self = this;
    const searchParams = {
      keyword,
      status: status || 'all',
      type: orderType || 'all',
      page: 1,
      pageSize: 20,
      sortBy: 'createTime',
      sortOrder: 'desc'
    };
    // 下拉刷新时不显示 loading 遮罩
    const reqOptions = _isPullRefresh ? { showLoading: false } : {};

    // 请求超时兜底：15秒无响应则主动超时
    const apiPromise = api.order.searchOrders(searchParams, reqOptions);
    const timeoutPromise = new Promise((_, reject) => {
      setTimeout(() => reject({ code: -1, message: '请求超时' }), 15000);
    });

    return Promise.race([apiPromise, timeoutPromise])
      .then((data) => {
        let list = [];
        // 支持多种数据结构
        if (data && Array.isArray(data)) {
          list = data;
        } else if (data && data.list && Array.isArray(data.list)) {
          list = data.list;
        } else if (data && data.orders && Array.isArray(data.orders)) {
          list = data.orders;
        } else if (data && data.data && Array.isArray(data.data)) {
          list = data.data;
        }

        const orders = list.map(order => ({
          ...order,
          type: order.type,
          typeText: order.typeText,
          statusText: order.statusText,
          orderNumber: order.orderNumber || order.orderNo,
          isPickupOrder: order.isPickupOrder || (order.type === 'goods' && order.deliveryMethod === 'pickup'),
          totalPrice: order.totalPrice ? order.totalPrice.toString().replace(/[¥￥]/g, '') : order.totalPrice,
          items: (order.items || []).map(item => ({
            ...item,
            image: self.processImageUrl(item.image),
            price: item.price ? item.price.toString().replace(/[¥￥]/g, '') : item.price
          }))
        }));

        self.initOrderCountdowns(orders);

        // 检查本地存储中的退款记录，覆写已退款订单的状态
        orders.forEach(order => {
          const refundKey = 'refundNo_' + (order.orderNumber || order.orderNo || order.id);
          if (wx.getStorageSync(refundKey) && !['refund', 'refunding', 'refunded'].includes(order.status)) {
            order.hasRefund = true;
            order.status = 'refunding';
            order.statusText = '退款中';
          }
        });

        self.setData({
          allOrders: [],
          orders,
          noSearchResult: orders.length === 0,
          loading: false,
          searching: false,
          isRequesting: false,
          currentPage: 1,
          hasMore: false,  // 搜索结果不支持分页
          totalOrders: orders.length
        });
      })
      .catch((err) => {
        self.setData({
          loading: false,
          searching: false,
          isRequesting: false,
          hasMore: true  // 重置 hasMore 状态，避免影响后续加载
        });
        wx.showToast({
          title: '搜索失败，请重试',
          icon: 'none'
        });
      });
  },

  processImageUrl(imageUrl) {
    if (!imageUrl) return '';
    const baseUrl = 'https://api.nengjifarm.com';
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
    // 如果没有 type 字段，才使用原有的 typeText
    else if (!type && order.typeText) typeText = order.typeText;
    const tableNo = order.diningTableNo || order.tableNumber || order.tableNo || order.dining_table_no || '';

    // 使用 self 引用，避免 this 指向问题
    const self = this;
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
        image: self.processImageUrl(item.image),
        price: (item.price || item.unitPrice || 0).toString().replace(/[¥￥]/g, '')
      }))
    };
  },

  // ======== 分页加载核心 ========

  // 首次加载 / 切换tab
  getOrders({ _isPullRefresh } = {}) {
    if (this.data.isRequesting) {
      return Promise.resolve();
    }

    // 下拉刷新时不切换 loading，避免 scroll-view 布局抖动
    if (_isPullRefresh) {
      this.setData({ isRequesting: true, searching: true });
    } else {
      this.setData({ isRequesting: true, loading: true, searching: true });
    }

    // 确定订单类型：优先使用类型标签，其次使用状态标签中的类型
    let orderType = this.data.activeTypeTab !== 'all' ? this.data.activeTypeTab : this.data.currentOrderType;
    let status = '';

    // 如果状态标签是类型标签（food/activity），则使用它
    if (['food', 'activity', 'cart'].includes(this.data.activeTab)) {
      orderType = this.data.activeTab;
      this.setData({ currentOrderType: orderType });
    } else if (this.data.activeTab !== 'all') {
      status = this.data.activeTab;
      // "待发货-待出餐"同时查 paid + ordered
      if (status === 'paid') {
        status = 'paid,ordered,verify_pending';
      } else if (status === 'refund') {
        status = 'refunding,refunded';
      }
    }

    const self = this;
    const reqParams = {
      type: orderType || 'all',
      status: status || 'all',
      page: 1,
      pageSize: PAGE_SIZE,
      sortBy: 'createTime',
      sortOrder: 'desc'
    };
    // 下拉刷新时不显示 loading 遮罩，避免干扰 scroll-view 的 refresher 状态机
    const reqOptions = _isPullRefresh ? { showLoading: false } : {};

    // 请求超时兜底：15秒无响应则主动超时，避免三个点一直转
    const apiPromise = api.order.getList(reqParams, reqOptions);
    const timeoutPromise = new Promise((_, reject) => {
      setTimeout(() => reject({ code: -1, message: '请求超时' }), 15000);
    });

    return Promise.race([apiPromise, timeoutPromise])
      .then((data) => {

        let ordersData = [];
        let total = 0;

        // 支持多种数据结构
        if (data && Array.isArray(data)) {
          ordersData = data;
          total = data.length;
        } else if (data && data.orders && Array.isArray(data.orders)) {
          ordersData = data.orders;
          total = data.total || data.orders.length;
        } else if (data && data.data && Array.isArray(data.data)) {
          ordersData = data.data;
          total = data.data.length;
        } else if (data && data.list && Array.isArray(data.list)) {
          ordersData = data.list;
          total = data.total || data.list.length;
        }


        const allOrders = ordersData.map(order => ({
          ...order,
          totalPrice: order.totalPrice ? order.totalPrice.toString().replace(/[¥￥]/g, '') : order.totalPrice,
          isPickupOrder: order.isPickupOrder || (order.type === 'goods' && order.deliveryMethod === 'pickup'),
          items: (order.items || []).map(item => ({
            ...item,
            image: self.processImageUrl(item.image),
            price: item.price ? item.price.toString().replace(/[¥￥]/g, '') : item.price
          }))
        }));

        self.initOrderCountdowns(allOrders);

        // 检查本地存储中的退款记录，覆写已退款订单的状态（订单在数据库仍是原状态但已提交退款）
        allOrders.forEach(order => {
          const refundKey = 'refundNo_' + (order.orderNumber || order.orderNo || order.id);
          if (wx.getStorageSync(refundKey) && !['refund', 'refunding', 'refunded'].includes(order.status)) {
            order.hasRefund = true;
            order.status = 'refunding';
            order.statusText = '退款中';
          }
        });


        // 判断是否有更多数据
        let hasMore = false;
        if (total > 0) {
          hasMore = allOrders.length < total;
        } else {
          hasMore = allOrders.length >= PAGE_SIZE;
        }

        self.setData({
          allOrders: allOrders,
          orders: allOrders,
          noSearchResult: false,
          loading: false,
          searching: false,
          isRequesting: false,
          hasLoaded: true,
          currentPage: 1,
          hasMore: hasMore,
          totalOrders: total
        });


        // 退款tab：额外查询退款列表，补全点餐等主查询未返回的退款订单
        if (status.includes('refunding') || status.includes('refunded')) {
          return self._supplementRefundOrders();
        }
      })
      .catch((err) => {
        self.setData({
          loading: false,
          searching: false,
          isRequesting: false,
          hasMore: true  // 重置 hasMore，允许重试
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

    // 检查是否可以加载下一页
    if (this.data.isRequesting) {
      return;
    }

    if (!this.data.hasMore) {
      return;
    }

    if (this.data.searchKeyword && this.data.searchKeyword.trim()) {
      return;
    }

    this.setData({ loadingMore: true, isRequesting: true });

    // 使用原有分页逻辑
    let params = { page: nextPage, pageSize: PAGE_SIZE, sortBy: 'createTime', sortOrder: 'desc' };
    const activeTab = this.data.activeTab;
    const activeTypeTab = this.data.activeTypeTab;

    // 设置类型和状态参数
    // 优先使用类型标签
    if (activeTypeTab !== 'all') {
      params.type = activeTypeTab;
    } else if (['food', 'activity', 'cart'].includes(activeTab)) {
      params.type = activeTab;
    } else if (activeTab !== 'all') {
      params.status = activeTab === 'paid' ? 'paid,ordered'
        : activeTab === 'refund' ? 'refunding,refunded'
        : activeTab;
    }

    const self = this;
    api.order.getList(params)
      .then((responseData) => {
        const { orders: rawOrders, total } = self._normalizeOrderList(responseData);

        if (rawOrders.length === 0) {
          self.setData({ hasMore: false, loadingMore: false, isRequesting: false });
          return;
        }

        const mappedOrders = rawOrders.map(order => self._mapOrder(order));

        // 检查本地存储中的退款记录，覆写已退款订单的状态
        mappedOrders.forEach(order => {
          const refundKey = 'refundNo_' + (order.orderNumber || order.orderNo || order.id);
          if (wx.getStorageSync(refundKey) && !['refund', 'refunding', 'refunded'].includes(order.status)) {
            order.hasRefund = true;
            order.status = 'refunding';
            order.statusText = '退款中';
          }
        });

        const existingIds = new Set(self.data.allOrders.map(o => o.id));
        const newOrders = mappedOrders.filter(o => !existingIds.has(o.id));
        const newAllOrders = [...self.data.allOrders, ...newOrders];
        newAllOrders.sort((a, b) => safeDate(b.createTime) - safeDate(a.createTime));

        // 判断是否有更多数据：
        // 1. 如果后端返回了 total，使用 total 判断
        // 2. 否则，如果返回的订单数等于 PAGE_SIZE，假设可能还有更多数据
        let hasMore = false;
        if (total > 0) {
          hasMore = newAllOrders.length < total;
        } else {
          hasMore = rawOrders.length > 0 && rawOrders.length >= PAGE_SIZE;
        }

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
        self.setData({
          loadingMore: false,
          isRequesting: false,
          hasMore: true  // 重置 hasMore，允许重试
        });
        wx.showToast({
          title: '加载失败，请重试',
          icon: 'none'
        });
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
  // 同时支持页面触底和 scroll-view 触底
  onReachBottom() {
    this.loadMoreOrders();
  },

  // scroll-view 触底事件
  loadMoreOrders() {

    // 搜索时不支持分页加载（搜索结果由后端返回，不支持翻页）
    if (this.data.searchKeyword && this.data.searchKeyword.trim()) {
      return;
    }

    if (!this.data.hasMore) {
      return;
    }

    if (this.data.isRequesting) {
      return;
    }

    if (this.data.loadingMore) {
      return;
    }

    // 直接触发加载下一页，不需要复杂的条件判断
    this.loadNextPage();
  },

  switchTab(e) {
    const tab = e.currentTarget.dataset.tab;
    if (tab === this.data.activeTab) return;

    let newCurrentOrderType = '';
    if (['food', 'activity', 'cart'].includes(tab)) {
      newCurrentOrderType = tab;
    }

    // 重置分页状态
    this.setData({
      activeTab: tab,
      currentOrderType: newCurrentOrderType,
      // 切换状态标签时，重置类型标签为"全部"
      activeTypeTab: 'all',
      loading: true,
      scrollToView: 'tab-' + tab,
      currentPage: 1,
      hasMore: true,
      isRequesting: false,
      loadingMore: false
    });

    // 有搜索关键词时，带状态过滤重新搜索；否则正常加载
    if (this.data.searchKeyword?.trim()) {
      this.searchOrders();
    } else {
      this.getOrders();
    }
  },

  // 切换订单类型标签
  switchTypeTab(e) {
    const typeTab = e.currentTarget.dataset.typeTab;
    if (typeTab === this.data.activeTypeTab) return;

    // 重置分页状态
    this.setData({
      activeTypeTab: typeTab,
      currentOrderType: typeTab === 'all' ? '' : typeTab,
      loading: true,
      currentPage: 1,
      hasMore: true,
      isRequesting: false,
      loadingMore: false
    });

    // 清除搜索关键词
    if (this.data.searchKeyword?.trim()) {
      this.setData({
        searchKeyword: '',
        searching: false,
        noSearchResult: false
      });
    }

    // 重新加载数据
    this.getOrders();
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

  cancelOrder(e) {
    const id = e.currentTarget.dataset.orderId || e.currentTarget.dataset.id;
    const order = this.data.orders.find(o => o.id === id);
    if (!order) {
      wx.showToast({ title: '订单不存在', icon: 'none' });
      return;
    }

    wx.showModal({
      title: '确认取消',
      content: '确定要取消这个订单吗？',
      success: (res) => {
        if (res.confirm) {
          api.order.updateStatus(order.orderNo, 'cancelled')
            .then(() => {
              wx.showToast({ title: '订单已取消', icon: 'success' });
              this.getOrders();
            })
            .catch(err => {
              wx.showToast({ title: err.message || '取消订单失败', icon: 'none' });
            });
        }
      }
    });
  },

  payOrder(e) {
    const id = e.currentTarget.dataset.orderId || e.currentTarget.dataset.id;
    const order = this.data.orders.find(o => o.id === id);
    if (!order) {
      wx.showToast({ title: '订单不存在', icon: 'none' });
      return;
    }

    // 检查订单状态是否为待支付（支持 pending 和 pending_payment）
    const pendingStatuses = ['pending', 'pending_payment'];
    if (!pendingStatuses.includes(order.status)) {
      wx.showToast({ title: '订单状态异常，无法支付', icon: 'none' });
      return;
    }

    // 跳转到支付页面
    wx.navigateTo({
      url: `/user-pages/pay/pay?orderNo=${order.orderNo}&totalPrice=${order.totalPrice}&type=${order.type || 'goods'}`
    });
  },

  viewOrderDetail(e) {
    const id = e.currentTarget.dataset.orderId || e.currentTarget.dataset.id;
    const order = this.data.orders.find(o => o.id === id || o.orderNumber === id || o.orderNo === id);
    const navId = (order && order.orderNo) || id;
    wx.navigateTo({ url: `/user-pages/orders-detail/orders-detail?id=${navId}` });
  },

  applyRefund(e) {
    const id = e.currentTarget.dataset.orderId || e.currentTarget.dataset.id;
    const order = this.data.orders.find(o => o.id === id);
    if (!order) {
      wx.showToast({ title: '订单不存在', icon: 'none' });
      return;
    }

    // 根据订单类型展示不同的退款原因
    const reasons = this._getRefundReasonsByType(order.type);

    wx.showActionSheet({
      itemList: reasons.map(r => r.label),
      success: (res) => {
        const selectedReason = reasons[res.tapIndex];
        wx.showModal({
          title: `退款原因：${selectedReason.label}`,
          content: '如有补充说明请在下方填写（选填）',
          editable: true,
          placeholderText: '补充说明（选填，最多200字）',
          success: (modalRes) => {
            if (!modalRes.confirm) return;
            const description = (modalRes.content || '').trim().substring(0, 200);
            wx.showLoading({ title: '提交中...' });
            api.refund.apply({
              orderNo: id,
              orderType: order.type,
              reason: selectedReason.value,
              reasonText: selectedReason.label,
              description
            })
              .then((refundData) => {
                wx.hideLoading();
                wx.showToast({ title: '退款申请已提交', icon: 'success' });
                // 保存 refundId 到本地存储（同时用数字ID和订单号，确保详情页能查到）
                if (refundData && refundData.refundId) {
                  wx.setStorageSync('refundNo_' + id, refundData.refundId);
                  if (order.orderNumber) {
                    wx.setStorageSync('refundNo_' + order.orderNumber, refundData.refundId);
                  }
                  if (order.orderNo && order.orderNo !== order.orderNumber) {
                    wx.setStorageSync('refundNo_' + order.orderNo, refundData.refundId);
                  }
                }
                this.getOrders();
              })
              .catch((err) => {
                wx.hideLoading();
                const msg = err && err.message ? err.message : '提交失败，请重试';
                wx.showToast({ title: msg, icon: 'none' });
              });
          }
        });
      }
    });
  },

  goToShop() { wx.reLaunch({ url: '/pages/index/index' }); },

  // 退款tab：补充查询退款列表，补全点餐等主API未返回的退款订单
  _supplementRefundOrders() {
    const self = this;
    return api.refund.getList({ page: 1, pageSize: 999 })
      .then((data) => {
        const refundItems = data && data.items ? data.items : (Array.isArray(data) ? data : []);
        if (refundItems.length === 0) return;

        // 获取当前类型过滤条件
        const activeTypeTab = self.data.activeTypeTab;

        // 收集已有订单的 orderNo/orderNumber/orderNo/id
        const existingOrderNos = new Set();
        self.data.orders.forEach(o => {
          if (o.orderNumber) existingOrderNos.add(String(o.orderNumber));
          if (o.orderNo) existingOrderNos.add(String(o.orderNo));
          if (o.id) existingOrderNos.add(String(o.id));
        });

        // 找出退款列表中尚未加载到订单列表中的（并按类型过滤）
        const missingRefunds = refundItems.filter(r => {
          const refNo = r.orderNo || r.orderId || '';
          if (!refNo || existingOrderNos.has(String(refNo))) return false;
          // 如果选了特定类型标签，按类型过滤
          if (activeTypeTab !== 'all') {
            const itemType = (r.orderType || '').toLowerCase();
            if (itemType !== activeTypeTab) return false;
          }
          return true;
        });

        if (missingRefunds.length === 0) return;

        // 逐个加载缺失的订单详情
        const fetchPromises = missingRefunds.map(r => {
          const orderNo = r.orderNo || r.orderId;
          if (!orderNo) return Promise.resolve(null);
          return api.order.getDetail(orderNo)
            .then(orderData => {
              if (!orderData) return null;
              return {
                ...orderData,
                id: orderData.id || orderNo,
                orderNumber: orderData.orderNumber || orderNo,
                orderNo: orderData.orderNo || orderNo,
                hasRefund: true,
                status: 'refunding',
                statusText: '退款中',
                type: orderData.type || r.orderType || '',
                typeText: orderData.typeText || (orderData.type === 'food' ? '点餐订单' : orderData.type === 'activity' ? '活动订单' : '商品订单'),
                items: (orderData.items || []).map(item => ({
                  ...item,
                  image: self.processImageUrl(item.image),
                  price: (item.price || '').toString().replace(/[¥￥]/g, '')
                })),
                totalPrice: (orderData.totalPrice || 0).toString().replace(/[¥￥]/g, ''),
                createTime: orderData.createTime || r.createTime || ''
              };
            })
            .catch(() => null);
        });

        return Promise.all(fetchPromises).then((extraOrders) => {
          let validOrders = extraOrders.filter(Boolean);
          // 二次确认类型过滤（从后端拿到的 orderData.type 更准确）
          if (activeTypeTab !== 'all') {
            validOrders = validOrders.filter(o => (o.type || '').toLowerCase() === activeTypeTab);
          }
          if (validOrders.length === 0) return;
          const merged = [...self.data.orders, ...validOrders];
          merged.sort((a, b) => new Date(String(b.createTime || 0).replace(/-/g, '/')) - new Date(String(a.createTime || 0).replace(/-/g, '/')));
          self.setData({
            orders: merged,
            allOrders: merged,
            noSearchResult: false,
            totalOrders: self.data.totalOrders + validOrders.length
          });
        });
      })
      .catch(() => {});
  },

  // 下拉刷新（模仿活动页）
  onPullDownRefresh() {
    this.setData({
      currentPage: 1,
      hasMore: true,
      isRequesting: false,
      loadingMore: false
    });

    const promise = this.data.searchKeyword?.trim()
      ? this.searchOrders({ _isPullRefresh: true })
      : this.getOrders({ _isPullRefresh: true });

    Promise.resolve(promise).then(() => {
      wx.stopPullDownRefresh();
    }).catch(() => {
      wx.stopPullDownRefresh();
      this.setData({ isRequesting: false, searching: false, loading: false });
    });
  },

  // 根据订单类型获取退款原因列表
  _getRefundReasonsByType(type) {
    const goodsReasons = [
      { value: 'wrong_item', label: '收到的商品与描述不符' },
      { value: 'damaged', label: '商品损坏/腐烂' },
      { value: 'not_as_expected', label: '不想要了' },
      { value: 'delayed_delivery', label: '长时间未发货' },
      { value: 'duplicate_order', label: '重复下单' },
      { value: 'other', label: '其他原因' }
    ];
    const foodReasons = [
      { value: 'wrong_dish', label: '菜品与点单不符' },
      { value: 'poor_quality', label: '菜品质量不佳' },
      { value: 'delayed_service', label: '出餐速度慢' },
      { value: 'duplicate_order', label: '重复下单' },
      { value: 'other', label: '其他原因' }
    ];
    const activityReasons = [
      { value: 'activity_changed', label: '活动内容变更' },
      { value: 'schedule_conflict', label: '时间安排冲突' },
      { value: 'duplicate_order', label: '重复下单' },
      { value: 'other', label: '其他原因' }
    ];

    if (type === 'food') return foodReasons;
    if (type === 'activity') return activityReasons;
    return goodsReasons;
  },

});
