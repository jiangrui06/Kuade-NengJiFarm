const { request, api } = require('../../utils/api');

Page({
  data: {
    orderType: 'food', // 'food' 或 'goods'
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
    tableList: [],
    addressList: [],
    selectedAddress: null,
    defaultAddress: null,
    showAllAddresses: false
  },

  onLoad: function (options) {
    const orderType = options.type || 'food';
    const tableNumber = options.tableNumber;
    
    this.setData({ orderType });
    
    if (tableNumber) {
      wx.setStorageSync('tableNumber', tableNumber);
    }
    
    // 初始化页面状态
    this.initPageState();
    this.loadCartData(orderType);
    
    if (orderType === 'food') {
      // 点餐：获取桌台列表
      const savedTableNumber = wx.getStorageSync('tableNumber');
      this.getTableList();
      this.setData({ tableNumber: savedTableNumber || null });
    } else {
      // 商品：获取地址列表
      this.getUserAddressList();
    }
  },

  onShow: function () {
    console.log('Confirm order page onShow');
    this.loadCartData(this.data.orderType);
    
    if (this.data.orderType === 'food') {
      const tableNumber = wx.getStorageSync('tableNumber');
      this.getTableList();
      this.setData({ tableNumber: tableNumber || null });
    } else {
      this.getUserAddressList();
    }
  },

  // 加载购物车数据
  loadCartData: function (orderType) {
    let cartItems = [];
    let totalPrice = 0;
    let totalCount = 0;
    
    if (orderType === 'food') {
      const cart = wx.getStorageSync('orderCart') || {};
      cartItems = Object.values(cart);
      cartItems.forEach(item => {
        const price = Number((item.price || 0).toString().replace(/[¥￥]/g, ''));
        totalPrice += price * Number(item.quantity || 0);
        totalCount += Number(item.quantity || 0);
      });
    } else {
      const cartList = wx.getStorageSync('cartList') || [];
      cartItems = cartList.filter(item => item.checked);
      cartItems.forEach(item => {
        const price = Number((item.price || 0).toString().replace(/[¥￥]/g, ''));
        totalPrice += price * Number(item.count || 0);
        totalCount += Number(item.count || 0);
      });
    }
    
    totalPrice = Number(totalPrice.toFixed(2));
    
    this.setData({
      orderInfo: {
        items: cartItems,
        totalPrice,
        totalCount
      }
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
    // 与点餐页面保持一致，8个桌子
    this.setData({ tableList: [1, 2, 3, 4, 5, 6, 7, 8].map(i => ({ id: String(i), name: `桌台${i}` })) });
    console.log('桌台列表已设置', this.data.tableList);
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

    // 根据类型检查必要条件
    if (this.data.orderType === 'food') {
      if (!this.data.tableNumber) {
        wx.showToast({ title: '请先选择桌台', icon: 'none' });
        return;
      }
    } else {
      if (!this.data.selectedAddress) {
        wx.showToast({ title: '请先选择收货地址', icon: 'none' });
        return;
      }
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
    const orderType = this.data.orderType;

    let promise;
    
    if (orderType === 'food') {
      const tableNumber = Number(wx.getStorageSync('tableNumber') || 0);
      const payload = {
        tableId: tableNumber,
        items: items.map(item => ({
          id: parseInt(item.id || '0'),
          price: Number((item.price || 0).toString().replace(/[¥￥]/g, '')),
          quantity: Number(item.quantity || 1)
        }))
      };
      promise = api.order.createDish(payload);
    } else {
      const payload = {
        addressId: parseInt(this.data.selectedAddress),
        items: items.map(item => ({
          id: parseInt(item.id || '0'),
          price: Number((item.price || 0).toString().replace(/[¥￥]/g, '')),
          quantity: Number(item.count || 1)
        }))
      };
      promise = api.order.createCommodity(payload);
    }

    promise
      .then((data) => {
        const orderId = data.orderId || data.id;
        if (!orderId) {
          wx.showToast({ title: '下单失败', icon: 'none' });
          this.setData({ loading: false, isCreatingOrder: false });
          return;
        }

        // 下单成功，清理购物车
        this.clearCartByType(orderType);

        // 跳转到订单页面
        wx.redirectTo({
          url: `/user-pages/orders/orders?tab=pending`
        });
      })
      .catch((err) => {
        console.error('下单失败:', err);
        this.setData({ loading: false, isCreatingOrder: false });
      });
  },

  // 根据类型清理购物车
  clearCartByType: function (type) {
    if (type === 'food') {
      wx.removeStorageSync('orderCart');
    } else {
      const cartList = wx.getStorageSync('cartList') || [];
      const remainingItems = cartList.filter(item => !item.checked);
      wx.setStorageSync('cartList', remainingItems);
    }
  },

  // 获取地址列表
  getUserAddressList: function () {
    wx.showLoading({ title: '加载中...' });
    request({
      url: '/api/user/address',
      method: 'GET'
    })
    .then(data => {
      const addressList = data || [];
      const defaultAddress = addressList.find(addr => addr.isDefault) || (addressList.length > 0 ? addressList[0] : null);
      this.setData({
        addressList,
        selectedAddress: defaultAddress ? defaultAddress.id : null,
        defaultAddress
      });
    })
    .catch(err => {
      console.error('获取地址列表失败:', err);
      wx.showToast({ title: '加载地址失败', icon: 'none' });
    })
    .finally(() => {
      wx.hideLoading();
    });
  },

  // 选择地址
  selectAddress: function (e) {
    const id = e.currentTarget.dataset.id;
    this.setData({ selectedAddress: id });
  },

  // 切换地址显示
  toggleAddressList: function () {
    this.setData({ showAllAddresses: !this.data.showAllAddresses });
  },

  // 添加新地址
  addAddress: function () {
    wx.navigateTo({ url: '/user-pages/address-edit/address-edit' });
  }
});

