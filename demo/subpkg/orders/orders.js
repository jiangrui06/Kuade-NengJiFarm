const api = require('../../utils/api');

Page({
  data: {
    activeTab: 'all', // 当前选中的标签：all, pending, paid, shipping, review, refund
    tabs: [
      { key: 'all', name: '全部' },
      { key: 'pending', name: '待支付' },
      { key: 'paid', name: '已支付' },
      { key: 'shipping', name: '待收货' },
    ],
    orders: [],
    loading: true
  },

  onLoad(options) {
    // 如果有传入tab参数，则设置为当前选中的标签
    if (options.tab) {
      this.setData({ activeTab: options.tab });
    }
    this.getOrders();
  },

  getOrders() {
    wx.showLoading({ title: '加载中...' });

    // API调用
    api.request({
      url: '/api/OrderDetails',
      method: 'GET',
      data: {
        status: this.data.activeTab === 'all' ? '' : this.data.activeTab,
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
    
    this.setData({ activeTab: tab, loading: true });
    this.getOrders();
  },

  viewOrderDetail(e) {
    const orderId = e.currentTarget.dataset.orderId;
    wx.navigateTo({
      url: `/subpkg/order-detail/order-detail?id=${orderId}`
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
