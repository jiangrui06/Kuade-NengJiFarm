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
    remark: '',
    remarkMaxLength: 200,
    // 配送方式
    deliveryMethod: 'express', // 'express' 快递 | 'pickup' 到店自取
    deliveryMethods: [
      { id: 'express', name: '快递配送', desc: '快递配送到家' },
      { id: 'pickup', name: '到店自取', desc: '到店出示核销码' }
    ],
    // 价格明细
    goodsAmount: 0,
    freight: 0,
    actualPay: 0
  },

  onLoad: function (options) {
    const orderType = options.type || 'food';
    const tableNumber = options.tableNumber;
    const fromBuyNow = options.from === 'buyNow';

    this.setData({ orderType, fromBuyNow });

    if (tableNumber) {
      wx.setStorageSync('tableNumber', tableNumber);
    }

    // 初始化页面状态
    this.initPageState();

    // 检查是否有临时订单数据（从商品详情页直接购买）
    const tempOrderData = wx.getStorageSync('tempOrderData');
    if (tempOrderData && tempOrderData.type === 'goods' && orderType === 'goods') {
      // 使用临时订单数据
      this.setData({
        orderInfo: {
          items: tempOrderData.items,
          totalPrice: tempOrderData.totalPrice,
          totalCount: tempOrderData.items.reduce((sum, item) => sum + Number(item.quantity || 0), 0)
        },
        selectedAddress: tempOrderData.selectedAddress
      });
      // 清除临时数据
      wx.removeStorageSync('tempOrderData');
    } else {
      // 使用购物车数据（或立即购买临时数据）
      this.loadCartData(orderType);
    }

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
    this.loadCartData(this.data.orderType);

    // 从支付页返回时恢复按钮状态（无论是否 buyNow）
    if (this.data.isCreatingOrder) {
      this.setData({ loading: false, isCreatingOrder: false });
    }

    if (this.data.orderType === 'food') {
      const tableNumber = wx.getStorageSync('tableNumber');
      this.getTableList();
      this.setData({ tableNumber: tableNumber || null });
    } else {
      // 检查是否有从地址选择页面返回的选中地址
      const selectedAddressId = wx.getStorageSync('selectedAddressId');
      if (selectedAddressId) {
        wx.removeStorageSync('selectedAddressId');
        this.setData({ selectedAddress: selectedAddressId });
        // 立即在当前地址列表中查找并更新显示
        const { addressList } = this.data;
        const found = addressList.find(addr => String(addr.id) === String(selectedAddressId));
        if (found) {
          this.setData({ defaultAddress: found });
        }
      }
      this.getUserAddressList();
    }
  },

  // 加载购物车数据
  loadCartData: function (orderType) {
    // 立即购买模式：使用临时存储的商品数据，不影响购物车
    if (this.data.fromBuyNow) {
      const tempItem = wx.getStorageSync('tempBuyNowItem');
      if (tempItem) {
        const price = Number((tempItem.price || 0).toString().replace(/[¥￥]/g, ''));
        const totalPrice = Number((price * Number(tempItem.quantity || 0)).toFixed(2));
        this.setData({
          orderInfo: {
            items: [tempItem],
            totalPrice,
            totalCount: Number(tempItem.quantity || 0)
          }
        }, () => {
          this.calcPriceBreakdown();
        });
      } else {
        // 临时数据已被清除（如订单已创建后退回），显示空
        this.setData({
          orderInfo: { items: [], totalPrice: 0, totalCount: 0 }
        }, () => {
          this.calcPriceBreakdown();
        });
      }
      return;
    }

    let cartItems = [];
    let totalPrice = 0;
    let totalCount = 0;

    if (orderType === 'food') {
      const cart = wx.getStorageSync('orderCart') || {};
      cartItems = Object.values(cart).filter(item => item && item.checked);
      cartItems.forEach(item => {
        const price = Number((item.price || 0).toString().replace(/[¥￥]/g, ''));
        totalPrice += price * Number(item.quantity || 0);
        totalCount += Number(item.quantity || 0);
      });
    } else {
      const rawCartList = wx.getStorageSync('cartList') || [];
      // 兼容对象格式（goods-detail 存储格式）和数组格式
      const cartList = Array.isArray(rawCartList) ? rawCartList : Object.values(rawCartList);
      cartItems = cartList.filter(item => item && item.checked);
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
    }, () => {
      // 计算价格明细
      this.calcPriceBreakdown();
    });
  },

  onHide: function () {
  },

  onUnload: function () {
    // 清除待执行的跳转计时器，防止页面卸载后仍跳转到支付页
    if (this._navigateTimer) {
      clearTimeout(this._navigateTimer);
      this._navigateTimer = null;
    }
  },

  // 初始化页面状态
  initPageState: function () {
    this.setData({
      loading: false,
      isCreatingOrder: false,
      selectedPayment: 'wechat',
      showTableModal: false,
      deliveryMethod: this.data.orderType === 'food' ? 'pickup' : 'express'
    });
  },

  // 选择配送方式
  selectDeliveryMethod: function (e) {
    const method = e.currentTarget.dataset.method;
    this.setData({ deliveryMethod: method }, () => {
      this.calcPriceBreakdown();
    });
  },

  // 计算价格明细
  calcPriceBreakdown: function () {
    const totalPrice = Number(this.data.orderInfo.totalPrice || 0);
    const deliveryMethod = this.data.deliveryMethod;
    const freight = 0;
    const actualPay = totalPrice + freight;
    this.setData({
      goodsAmount: totalPrice,
      freight: freight,
      actualPay: actualPay
    });
  },
  
  selectPayment: function (e) {
    const paymentId = e.currentTarget.dataset.id;
    if (!paymentId) return;
    this.setData({ selectedPayment: paymentId });
  },

  // 获取桌台列表
  getTableList: function () {
    api.table.getList()
      .then(data => {
        this.setData({ tableList: (data || []).map(t => ({ id: String(t.id), name: t.name })) });
      })
      .catch(() => {
        this.setData({ tableList: [] });
      });
  },

  // 显示桌台选择弹窗
  showTableModal: function () {
    this.setData({ showTableModal: true });
  },

  // 隐藏桌台选择弹窗
  hideTableModal: function () {
    this.setData({ showTableModal: false });
  },

  // 选择桌台号码
  selectTableNumber: function (e) {
    const tableId = e.currentTarget.dataset.tableId;
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

  // 备注输入
  onRemarkInput: function (e) {
    this.setData({ remark: e.detail.value });
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
      // 快递配送需选择地址，到店自取不需要
      if (this.data.deliveryMethod === 'express' && !this.data.selectedAddress) {
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
      // 兼容接口格式：items 用大写开头字段，后端以 sourceType='food' 识别为点餐
      const payload = {
        sourceType: 'food',
        sourceName: '点餐',
        tableId: tableNumber,
        remark: this.data.remark,
        quantity: items.reduce((sum, item) => sum + Number(item.quantity || 0), 0),
        totalPrice: items.reduce((sum, item) => {
          return sum + Number((item.price || 0).toString().replace(/[¥￥]/g, '')) * Number(item.quantity || 0);
        }, 0),
        items: items.map(item => ({
          Id: parseInt(item.id || '0'),
          Name: item.name || '',
          Price: Number((item.price || 0).toString().replace(/[¥￥]/g, '')),
          Quantity: Number(item.quantity || 1),
          Image: item.image || ''
        }))
      };
      promise = api.order.createDish(payload);
    } else {
      const deliveryMethod = this.data.deliveryMethod || 'express';
      const payload = {
        items: items.map(item => ({
          id: parseInt(item.id || '0'),
          price: Number((item.price || 0).toString().replace(/[¥￥]/g, '')),
          quantity: Number(item.quantity || item.count || 1)
        })),
        deliveryMethod: deliveryMethod
      };
      // 快递配送需要传地址
      if (deliveryMethod === 'express' && this.data.selectedAddress) {
        payload.addressId = parseInt(this.data.selectedAddress);
      }
      promise = api.order.createCommodityV2(payload);
    }

    promise
      .then((data) => {
        const orderNo = data.orderNo || data.orderId || data.orderNumber || data.id;
        if (!orderNo) {
          wx.showToast({ title: '下单失败', icon: 'none' });
          this.setData({ loading: false, isCreatingOrder: false });
          return;
        }

        wx.showToast({ title: '订单创建成功', icon: 'success' });

        // 清理购物车
        this.clearCartByType(orderType);

        // 不重置 loading/isCreatingOrder，防止用户在跳转支付前的窗口期内重复点击创建订单

        this._navigateTimer = setTimeout(() => {
          // 跳转到支付页面，使用 navigateTo 保留页面栈，并添加 from 参数
          wx.navigateTo({
            url: `/user-pages/pay/pay?orderNo=${orderNo}&totalPrice=${this.data.orderInfo.totalPrice}&type=${orderType}&from=cart`
          });
        }, 1500);
      })
      .catch((err) => {
        this.setData({ loading: false, isCreatingOrder: false });
      });
  },

  // 根据类型清理购物车
  clearCartByType: function (type) {
    // 立即购买模式：只清理临时数据，不碰购物车
    if (this.data.fromBuyNow) {
      wx.removeStorageSync('tempBuyNowItem');
      return;
    }

    if (type === 'food') {
      const orderCart = wx.getStorageSync('orderCart') || {};
      const remainingItems = {};
      for (const key in orderCart) {
        if (!orderCart[key].checked) {
          remainingItems[key] = orderCart[key];
        }
      }
      wx.setStorageSync('orderCart', remainingItems);
    } else {
      const cartList = wx.getStorageSync('cartList') || [];
      const remainingItems = cartList.filter(item => !item.checked);
      wx.setStorageSync('cartList', remainingItems);
    }
  },

  // 获取地址列表
  getUserAddressList: function () {
    wx.showLoading({ title: '加载中...' });
    api.user.getAddresses()
    .then(data => {
      const addressList = data || [];
      let selectedAddress = this.data.selectedAddress;
      let defaultAddress = addressList.find(addr => addr.isDefault) || (addressList.length > 0 ? addressList[0] : null);

      // 确保选中的地址存在于列表中（ID 统一转字符串比较）
      if (selectedAddress) {
        const found = addressList.some(addr => String(addr.id) === String(selectedAddress));
        if (!found && defaultAddress) {
          selectedAddress = defaultAddress.id;
        }
      } else if (defaultAddress) {
        selectedAddress = defaultAddress.id;
      }

      // 更新显示的默认地址为选中地址
      if (selectedAddress) {
        defaultAddress = addressList.find(addr => String(addr.id) === String(selectedAddress)) || defaultAddress;
      }

      this.setData({
        addressList,
        selectedAddress,
        defaultAddress
      });
    })
    .catch(err => {
      wx.showToast({ title: '加载地址失败', icon: 'none' });
    })
    .finally(() => {
      wx.hideLoading();
    });
  },

  // 跳转到地址选择页面
  goToAddress: function () {
    wx.navigateTo({
      url: '/user-pages/address/address?from=buy'
    });
  }
});

