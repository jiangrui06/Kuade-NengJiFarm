const api = require('../../utils/api');

Page({
  data: {
    activeTab: 'all', // 当前选中的标签：all, pending, paid, shipping, review, refund, food, acre, activity, cart
    scrollToView: '', // 用于滚动到指定标签
    tabs: [
      { key: 'all', name: '全部' },
      { key: 'food', name: '点餐' },
      { key: 'acre', name: '认购' },
      { key: 'activity', name: '活动' },
      { key: 'cart', name: '购物车' },
      { key: 'pending', name: '待支付' },
      { key: 'paid', name: '已支付' },
      { key: 'shipping', name: '待收货' },
    ],
    orders: [],
    loading: true
  },

  onLoad(options) {
    console.log('Orders page onLoad, options:', options);
    // 如果有传入tab参数，则设置为当前选中的标签
    let tab = 'all';
    if (options.tab) {
      console.log('Setting activeTab to:', options.tab);
      tab = options.tab;
    }
    console.log('Current activeTab:', tab);
    this.setData({
      activeTab: tab,
      scrollToView: 'tab-' + tab
    });
    this.getOrders();
  },

  getOrders() {
    wx.showLoading({ title: '加载中...' });

    // 确定订单类型和状态
    let orderType = '';
    let status = '';
    
    // 处理订单类型标签
    if (['food', 'acre', 'activity', 'cart'].includes(this.data.activeTab)) {
      orderType = this.data.activeTab;
    } else if (this.data.activeTab !== 'all') {
      // 处理订单状态标签
      status = this.data.activeTab;
    }

    // API调用
    api.request({
      url: '/api/OrderDetails',
      method: 'GET',
      data: {
        type: orderType,
        status: status,
        page: 1,
        pageSize: 10,
        sortBy: 'createTime',
        sortOrder: 'desc'
      }
    })
      .then((data) => {
        this.setData({
          orders: data.orders || [],
          loading: false
        });
      })
      .catch((err) => {
        console.error('获取订单列表失败:', err);
        this.setData({ loading: false });
        // 出错时显示空订单状态
        this.setData({ orders: [] });
      })
      .finally(() => {
        wx.hideLoading();
      });
  },

  switchTab(e) {
    const tab = e.currentTarget.dataset.tab;
    if (tab === this.data.activeTab) return;
    
    this.setData({ 
      activeTab: tab, 
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

  goBack() {
    wx.navigateBack();
  }
});
