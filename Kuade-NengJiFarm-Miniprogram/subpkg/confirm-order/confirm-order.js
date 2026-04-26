const { request, api } = require('../../utils/api');

Page({
  data: {
    orderInfo: {
      items: [],
      totalPrice: 0,
      totalCount: 0
    },
    paymentMethods: [
      { id: 'wechat', name: '微信支付', icon: '💳' }
    ],
    selectedPayment: 'wechat',
    loading: false,
    isCreatingOrder: false,
    tableNumber: null,
    showTableModal: false,
    tableList: []
  },

  onLoad: function () {
    // 初始化页面状态
    this.initPageState();
    const cart = wx.getStorageSync('orderCart') || {};
    const cartItems = Object.values(cart);

    let totalPrice = 0;
    let totalCount = 0;
    cartItems.forEach(item => {
      const price = Number((item.price || 0).toString().replace(/[¥￥]/g, ''));
      totalPrice += price * Number(item.quantity || 0);
      totalCount += Number(item.quantity || 0);
    });
    totalPrice = Number(totalPrice.toFixed(2));

    // 从本地存储读取桌台号码（与点餐页面联动）
    const tableNumber = wx.getStorageSync('tableNumber');

    // 获取桌台列表
    this.getTableList();

    this.setData({
      orderInfo: {
        items: cartItems,
        totalPrice,
        totalCount
      },
      tableNumber: tableNumber || null
    });
  },

  onShow: function () {
    console.log('Confirm order page onShow');
    const cart = wx.getStorageSync('orderCart') || {};
    const cartItems = Object.values(cart);

    let totalPrice = 0;
    let totalCount = 0;
    cartItems.forEach(item => {
      const price = Number((item.price || 0).toString().replace(/[¥￥]/g, ''));
      totalPrice += price * Number(item.quantity || 0);
      totalCount += Number(item.quantity || 0);
    });
    totalPrice = Number(totalPrice.toFixed(2));

    // 每次显示页面时，都从本地存储读取最新的桌台号码（与点餐页面联动）
    const tableNumber = wx.getStorageSync('tableNumber');

    // 获取桌台列表
    this.getTableList();

    this.setData({
      'orderInfo.items': cartItems,
      'orderInfo.totalPrice': totalPrice,
      'orderInfo.totalCount': totalCount,
      tableNumber: tableNumber || null
    });
  },

  onHide: function () {
    console.log('Confirm order page onHide');
  },

  onUnload: function () {
    console.log('Confirm order page onUnload');
  },

  // 初始化页面状态
  initPageState: function () {
    this.setData({
      loading: false,
      isCreatingOrder: false,
      selectedPayment: 'wechat',
      showTableModal: false
    });
    console.log('Confirm order page state initialized');
  },

  selectPayment: function (e) {
    const paymentId = e.currentTarget.dataset.id;
    if (!paymentId) return;
    this.setData({ selectedPayment: paymentId });
  },

  // 获取桌台列表
  getTableList: function () {
    console.log('获取桌台列表');
    // 与点餐页面保持一致，共8个桌台
    this.setData({ tableList: [1, 2, 3, 4, 5, 6, 7, 8].map(i => ({ id: String(i), name: `桌台${i}` })) });
    console.log('桌台列表已设置:', this.data.tableList);
  },

  // 显示桌台选择弹窗
  showTableModal: function () {
    console.log('显示桌台选择弹窗, tableList:', this.data.tableList);
    this.setData({ showTableModal: true });
  },

  // 隐藏桌台选择弹窗
  hideTableModal: function () {
    console.log('隐藏桌台选择弹窗');
    this.setData({ showTableModal: false });
  },

  // 选择桌台号码
  selectTableNumber: function (e) {
    const tableId = e.currentTarget.dataset.tableId;
    console.log('选择桌台:', tableId, '当前tableNumber:', this.data.tableNumber);
    if (!tableId) return;

    // 更新本地状态
    this.setData({
      tableNumber: tableId,
      showTableModal: false
    });

    // 保存到本地存储（与点餐页面联动）
    wx.setStorageSync('tableNumber', tableId);

    wx.showToast({
      title: `已选择桌台 ${tableId}`,
      icon: 'success',
      duration: 1500
    });
  },

  // 确认订单
  confirmOrder: function () {
    if (this.data.isCreatingOrder) return;

    // 检查是否选择了桌台
    if (!this.data.tableNumber) {
      wx.showToast({ title: '请先选择桌台', icon: 'none' });
      return;
    }

    const items = this.data.orderInfo.items || [];
    if (!items.length) {
      wx.showToast({ title: '购物车为空', icon: 'none' });
      return;
    }

    const totalPrice = Number(this.data.orderInfo.totalPrice || 0);
    if (totalPrice <= 0) {
      wx.showToast({ title: '金额异常', icon: 'none' });
      return;
    }

    this.setData({ loading: true, isCreatingOrder: true });

    // 直接创建订单，允许用户有多个待支付订单
    this.createOrder();
  },



  // 创建订单
  createOrder: function () {
    const items = this.data.orderInfo.items || [];
    const totalPrice = Number(this.data.orderInfo.totalPrice || 0);
    const tableNumber = Number(wx.getStorageSync('tableNumber') || 0);

    const payload = {
      sourceType: 'food',
      sourceName: '点餐',
      quantity: this.data.orderInfo.totalCount || 1,
      tableNumber: tableNumber > 0 ? tableNumber : 0,
      totalPrice,
      items: items.map(item => ({
        id: String(item.id || ''),
        name: item.name || '餐品',
        price: Number((item.price || 0).toString().replace(/[¥￥]/g, '')),
        quantity: Number(item.quantity || 1),
        image: item.image || ''
      }))
    };

    request({
      url: '/api/OrderDetails/create',
      method: 'POST',
      data: payload,
      showLoading: false
    })
      .then((data) => {
        const orderId = data.orderId || data.id;
        if (!orderId) {
          wx.showToast({ title: '创建订单失败', icon: 'none' });
          this.setData({ loading: false, isCreatingOrder: false });
          return;
        }
        // 只清空已下单的商品，而不是整个购物车
        this.clearOrderedItems(items);
        // 用 redirectTo 替换当前页，避免页面栈过深，同时订单页 onLoad 会自动刷新
        wx.redirectTo({
          url: '/subpkg/orders/orders?tab=pending'
        });
      })
      .catch(() => {
        wx.showToast({ title: '下单失败', icon: 'none' });
      })
      .finally(() => {
        this.setData({ loading: false, isCreatingOrder: false });
      });
  },

  // 清空已下单的商品
  clearOrderedItems: function (orderedItems) {
    try {
      // 1. 更新 orderCart 缓存
      const orderCart = wx.getStorageSync('orderCart') || {};
      const newOrderCart = { ...orderCart };
      
      orderedItems.forEach(item => {
        const key = String(item.id);
        delete newOrderCart[key];
      });
      
      wx.setStorageSync('orderCart', newOrderCart);
      
      // 2. 更新 cartList 缓存
      const cartList = wx.getStorageSync('cartList') || [];
      const newCartList = cartList.filter(cartItem => {
        if (cartItem.type !== 'food') return true;
        const cartItemId = String(cartItem.id);
        return !orderedItems.some(item => String(item.id) === cartItemId);
      });
      
      wx.setStorageSync('cartList', newCartList);
      
      console.log('已清空已下单的商品缓存');
    } catch (e) {
      console.error('清空已下单商品缓存失败:', e);
    }
  },

  goBack: function () {
    wx.navigateBack();
  }
});
