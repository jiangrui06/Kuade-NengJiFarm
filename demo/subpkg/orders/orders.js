const { api } = require('../../utils/api');

Page({
  data: {
    activeTab: 'all', // 当前选中的标签：all, pending, paid, shipping, review, refund, food, acre, activity, cart
    currentOrderType: '', // 当前订单类型
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
    loading: true,
    isRequesting: false, // 防止重复请求
    isPageVisible: false // 页面是否可见
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
      scrollToView: 'tab-' + tab,
      isPageVisible: true
    });
    this.getOrders();
  },

  onShow() {
    // 只有页面可见时才刷新数据
    if (this.data.isPageVisible) {
      this.getOrders();
    }
    this.setData({ isPageVisible: true });
  },

  onHide() {
    // 页面隐藏时标记为不可见
    this.setData({ isPageVisible: false });
  },

  // 处理图片路径，确保使用正确的基础 URL
  processImageUrl: function (imageUrl) {
    if (!imageUrl) return '';
    
    // 去除反引号和空格
    imageUrl = imageUrl.replace(/[`\s]/g, '');
    
    // 如果是完整的 URL，替换基础 URL
    if (imageUrl.startsWith('http://') || imageUrl.startsWith('https://')) {
      // 只替换 127.0.0.1:5000 为 192.168.203.56，不影响其他URL
      if (imageUrl.includes('127.0.0.1:5000')) {
        imageUrl = imageUrl.replace('127.0.0.1:5000', '192.168.203.56');
      }
      // 如果已经是正确的URL格式，直接返回
      return imageUrl;
    }
    
    // 如果是相对路径，添加基础 URL
    // 确保基础 URL 后面有斜杠
    const baseUrl = 'http://192.168.203.56';
    // 确保图片路径以斜杠开头
    if (!imageUrl.startsWith('/')) {
      imageUrl = '/' + imageUrl;
    }
    return baseUrl + imageUrl;
  },

  getOrders() {
    // 防止重复请求
    if (this.data.isRequesting) {
      console.log('请求中，忽略重复调用');
      return;
    }
    
    this.setData({ isRequesting: true, loading: true });

    let orderType = this.data.currentOrderType;
    let status = '';
    
    // 如果当前标签是订单类型标签，更新currentOrderType
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
        // 处理订单商品图片路径，添加数据验证
        let ordersData = [];
        if (data && Array.isArray(data.orders)) {
          ordersData = data.orders;
        } else if (data && Array.isArray(data)) {
          ordersData = data;
        }
        
        const orders = ordersData.map(order => ({
          ...order,
          totalPrice: order.totalPrice ? order.totalPrice.toString().replace(/[¥￥]/g, '') : order.totalPrice,
          items: ((order.items || []).map(item => ({
            ...item,
            image: this.processImageUrl(item.image),
            price: item.price ? item.price.toString().replace(/[¥￥]/g, '') : item.price
          })))
        }));
        
        this.setData({
          orders: orders,
          loading: false
        });
      })
      .catch((err) => {
        console.error('获取订单列表失败:', err);
        this.setData({ loading: false, orders: [] });
      })
      .finally(() => {
        this.setData({ isRequesting: false });
        wx.hideLoading();
      });
  },

  switchTab(e) {
    const tab = e.currentTarget.dataset.tab;
    if (tab === this.data.activeTab) return;
    
    // 当切换到状态标签（如待付款）时，清空currentOrderType
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

  goBack() {
    wx.navigateBack();
  },

  // 删除订单
  deleteOrder(e) {
    const orderId = e.currentTarget.dataset.orderId;
    
    wx.showModal({
      title: '确认删除',
      content: '确定要删除这个待付款订单吗？',
      confirmText: '删除',
      cancelText: '取消',
      confirmColor: '#ff4d4f',
      success: (res) => {
        if (res.confirm) {
          wx.showLoading({ title: '删除中...' });
          
          api.order.delete(orderId)
            .then(() => {
              wx.showToast({
                title: '删除成功',
                icon: 'success'
              });
              // 重新加载订单列表
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
  }
});
