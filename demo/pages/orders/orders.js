const api = require('../../utils/api');

Page({
  data: {
    activeTab: 'all', // 当前选中的标签：all, pending, paid, shipping, review, refund
    tabs: [
      { key: 'all', name: '全部' },
      { key: 'pending', name: '待支付' },
      { key: 'paid', name: '已支付' },
      { key: 'shipping', name: '待收货' },
      { key: 'refund', name: '退款/售后' }
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

    // 使用虚拟数据先展示页面
    const mockOrders = [];
    
    // 根据当前标签生成对应的虚拟订单
    if (this.data.activeTab === 'all' || this.data.activeTab === 'pending') {
      mockOrders.push({
        id: '1001',
        status: 'pending',
        statusText: '待支付',
        createTime: '2026-03-25 18:30:00',
        totalPrice: 198.00,
        items: [
          {
            name: '红烧肉',
            price: 68.00,
            quantity: 1,
            image: 'https://images.unsplash.com/photo-1555939594-58d7cb561ad1?auto=format&fit=crop&w=100&q=80'
          },
          {
            name: '清炒时蔬',
            price: 28.00,
            quantity: 1,
            image: 'https://images.unsplash.com/photo-1576181259264-6902b56d9f36?auto=format&fit=crop&w=100&q=80'
          }
        ]
      });
    }
    
    if (this.data.activeTab === 'all' || this.data.activeTab === 'paid') {
      mockOrders.push({
        id: '1002',
        status: 'paid',
        statusText: '已支付',
        createTime: '2026-03-24 12:15:00',
        totalPrice: 88.00,
        items: [
          {
            name: '宫保鸡丁',
            price: 48.00,
            quantity: 1,
            image: 'https://images.unsplash.com/photo-1606744872121-a423a4010b30?auto=format&fit=crop&w=100&q=80'
          }
        ]
      });
    }
    
    if (this.data.activeTab === 'all' || this.data.activeTab === 'shipping') {
      mockOrders.push({
        id: '1003',
        status: 'shipping',
        statusText: '待收货',
        createTime: '2026-03-23 20:00:00',
        totalPrice: 156.00,
        items: [
          {
            name: '鱼香肉丝',
            price: 38.00,
            quantity: 2,
            image: 'https://images.unsplash.com/photo-1563245372-f865e890286d?auto=format&fit=crop&w=100&q=80'
          },
          {
            name: '米饭',
            price: 2.00,
            quantity: 2,
            image: 'https://images.unsplash.com/photo-1576717039968-6b2757ab4a20?auto=format&fit=crop&w=100&q=80'
          }
        ]
      });
    }
    
    this.setData({
      orders: mockOrders,
      loading: false
    });

    wx.hideLoading();

    // API调用（暂时注释，等API准备好后启用）
    /*
    api.request({
      url: '/api/orders',
      method: 'GET',
      data: {
        status: this.data.activeTab === 'all' ? '' : this.data.activeTab,
        page: 1,
        pageSize: 10
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
      })
      .finally(() => {
        wx.hideLoading();
      });
    */
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
      url: `/pages/order-detail/order-detail?id=${orderId}`
    });
  },

  payOrder(e) {
    const orderId = e.currentTarget.dataset.orderId;
    wx.navigateTo({
      url: `/pages/pay/pay?orderId=${orderId}`
    });
  },

  goBack() {
    wx.navigateBack();
  }
});
