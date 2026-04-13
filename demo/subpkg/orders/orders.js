const { api } = require('../../utils/api');

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
      { key: 'pending', name: '待付款' },
      { key: 'paid', name: '待发货' },
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

  // 处理图片路径，确保使用正确的基础 URL
  processImageUrl: function (imageUrl) {
    if (!imageUrl) return '';
    
    // 去除反引号和空格
    imageUrl = imageUrl.replace(/[`\s]/g, '');
    
    // 如果是完整的 URL，替换基础 URL
    if (imageUrl.startsWith('http://') || imageUrl.startsWith('https://')) {
      // 替换 127.0.0.1:5000 为 192.168.203.56
      return imageUrl.replace('http://127.0.0.1:5000', 'http://192.168.203.56');
    }
    
    // 如果是相对路径，添加基础 URL
    return 'http://192.168.203.56' + imageUrl;
  },

  getOrders() {
    wx.showLoading({ title: '加载中...' });

    let orderType = '';
    let status = '';
    
    if (['food', 'acre', 'activity', 'cart'].includes(this.data.activeTab)) {
      orderType = this.data.activeTab;
    } else if (this.data.activeTab !== 'all') {
      status = this.data.activeTab;
    }

    api.order.getList({
      type: orderType,
      status: status,
      page: 1,
      pageSize: 10,
      sortBy: 'createTime',
      sortOrder: 'desc'
    })
      .then((data) => {
        // 处理订单商品图片路径
        const orders = (data.orders || []).map(order => ({
          ...order,
          items: (order.items || []).map(item => ({
            ...item,
            image: this.processImageUrl(item.image)
          }))
        }));
        
        this.setData({
          orders: orders,
          loading: false
        });
      })
      .catch((err) => {
        console.error('获取订单列表失败:', err);
        this.setData({ loading: false });
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
