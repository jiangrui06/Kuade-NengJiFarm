// pages/cart/cart.js
const { request } = require('../../utils/api');

Page({

  data: {
    cartList: [],
    regions: {
      food: { name: '点餐', items: [], selected: false, totalPrice: 0, previewImages: [], moreCount: 0, checkedItemNames: [] },
      goods: { name: '商品', items: [], selected: false, totalPrice: 0, previewImages: [], moreCount: 0, checkedItemNames: [] }
    },
    totalPrice: 0,
    selectedCount: 0,
    selectAll: false,
    canSettle: false,
    tableNumber: null,
    hasSelectedFood: false,

    // 弹窗控制
    showModal: false,
    showCartDetail: false,
    showSeparateSettleModal: false,

    // 收货地址
    addressList: [],
    selectedAddress: null,
    defaultAddress: null,
    showAllAddresses: false
  },

  onShow() {
    this.restoreCart();
    this.getUserAddressList();
    this.loadTableNumber();
    if (typeof this.getTabBar === 'function' && this.getTabBar()) {
      this.getTabBar().init();
    }
  },

  // ========== 购物车数据恢复 ==========
  restoreCart() {
    let cartList = [];

    const goodsCart = (wx.getStorageSync('cartList') || []).map(item => ({
      ...item,
      checked: !!item.checked,
      count: Number(item.count || 0),
      price: Number((item.price || 0).toString().replace(/[¥￥]/g, '')),
      stock: Number(item.stock || 0),
      type: 'goods',
      _cartKey: 'goods_' + String(item.id),
      image: this.processImageUrl(item.image || '')
    }));
    cartList.push(...goodsCart);

    const orderCart = wx.getStorageSync('orderCart') || {};
    for (const id in orderCart) {
      const item = orderCart[id];
      cartList.push({
        id: String(id),
        name: item.name || '',
        price: Number((item.price || 0).toString().replace(/[¥￥]/g, '')),
        image: this.processImageUrl(item.image || ''),
        count: Number(item.count || item.quantity || 0),
        checked: !!item.checked,
        type: 'food',
        stock: Number(item.stock || 0),
        _cartKey: 'food_' + String(id)
      });
    }

    this.setData({ cartList });
    this.groupItemsByRegion(cartList);
    this.calcTotal();
  },

  processImageUrl(imageUrl) {
    const utils = require('../../utils/utils');
    return utils.media.processUrl(imageUrl);
  },

  // ========== 分组 ==========
  groupItemsByRegion(cartList) {
    const regions = {
      food: { name: '点餐', items: [], selected: false, hasChecked: false, totalPrice: 0, previewImages: [], moreCount: 0, checkedItemNames: [] },
      goods: { name: '商品', items: [], selected: false, hasChecked: false, totalPrice: 0, previewImages: [], moreCount: 0, checkedItemNames: [] }
    };

    cartList.forEach(item => {
      const regionKey = item.type === 'food' ? 'food' : 'goods';
      regions[regionKey].items.push(item);
    });

    // 更新区域选中状态
    ['food', 'goods'].forEach(key => {
      const items = regions[key].items;
      if (items.length > 0) {
        regions[key].selected = items.every(i => i.checked);
        const checkedItems = items.filter(i => i.checked);
        regions[key].hasChecked = checkedItems.length > 0;
        regions[key].totalPrice = checkedItems.reduce((sum, i) => sum + i.price * i.count, 0).toFixed(2);
        regions[key].previewImages = checkedItems.slice(0, 3).map(i => i.image);
        regions[key].moreCount = Math.max(0, checkedItems.length - 3);
        regions[key].checkedItemNames = checkedItems.map(i => i.name);
      }
    });

    this.setData({
      regions,
      hasSelectedFood: regions.food.items.length > 0
    });
  },

  // ========== 计算总价和选中数量 ==========
  calcTotal() {
    const { cartList } = this.data;
    let totalPrice = 0;
    let selectedCount = 0;
    let selectAll = cartList.length > 0;

    cartList.forEach(item => {
      if (item.checked) {
        totalPrice += item.price * item.count;
        selectedCount += item.count;
      } else {
        selectAll = false;
      }
    });

    this.setData({
      totalPrice: totalPrice.toFixed(2),
      selectedCount,
      selectAll,
      canSettle: selectedCount > 0
    });
  },

  // ========== 同步购物车到 Storage ==========
  syncCart(cartList) {
    const normalizedCartList = cartList
      .filter(item => Number(item.count || 0) > 0)
      .map(item => ({
        id: String(item.id),
        name: item.name || '',
        price: Number((item.price || 0).toString().replace(/[¥￥]/g, '')),
        image: item.image || '',
        count: Number(item.count || 0),
        checked: !!item.checked,
        type: item.type || 'goods',
        stock: Number(item.stock || 0),
        _cartKey: (item.type || 'goods') + '_' + String(item.id)
      }));

    // 分离 goods 和 food
    const goodsItems = normalizedCartList.filter(i => i.type === 'goods');
    const foodItems = normalizedCartList.filter(i => i.type === 'food');

    // 同步商品到 Storage
    wx.setStorageSync('cartList', goodsItems.map(i => ({
      id: i.id,
      name: i.name,
      price: i.price,
      image: i.image,
      count: i.count,
      checked: i.checked,
      stock: i.stock
    })));

    // 同步点餐到 Storage
    const orderCart = {};
    foodItems.forEach(i => {
      orderCart[i.id] = {
        name: i.name,
        price: i.price,
        image: i.image,
        count: i.count,
        quantity: i.count,
        stock: i.stock,
        checked: i.checked
      };
    });
    wx.setStorageSync('orderCart', orderCart);
  },

  // ========== 单选/取消 ==========
  toggleSelect(e) {
    const id = String(e.currentTarget.dataset.id);
    const type = e.currentTarget.dataset.type;
    const cartList = this.data.cartList;
    const index = cartList.findIndex(i => String(i.id) === id && i.type === type);
    if (index === -1) return;

    cartList[index].checked = !cartList[index].checked;
    this.setData({ cartList });
    this.groupItemsByRegion(cartList);
    this.calcTotal();
    this.syncCart(cartList);
  },

  // ========== 区域全选 ==========
  toggleRegionSelect(e) {
    const region = e.currentTarget.dataset.region;
    const regions = this.data.regions;
    const cartList = this.data.cartList;

    const currentSelected = regions[region].selected;
    const newSelected = !currentSelected;

    cartList.forEach(item => {
      const itemRegion = item.type === 'food' ? 'food' : 'goods';
      if (itemRegion === region) {
        item.checked = newSelected;
      }
    });

    this.setData({ cartList });
    this.groupItemsByRegion(cartList);
    this.calcTotal();
    this.syncCart(cartList);
  },

  // ========== 全选 ==========
  handleSelectAll() {
    const selectAll = !this.data.selectAll;
    const cartList = this.data.cartList.map(item => ({
      ...item,
      checked: selectAll
    }));

    this.setData({ cartList });
    this.groupItemsByRegion(cartList);
    this.calcTotal();
    this.syncCart(cartList);
  },

  // ========== 清空选中的商品 ==========
  handleClearCart() {
    const { cartList } = this.data;
    const selectedItems = cartList.filter(i => i.checked);
    
    if (selectedItems.length === 0) {
      wx.showToast({ title: '请先选择要删除的商品', icon: 'none' });
      return;
    }

    wx.showModal({
      title: '提示',
      content: `确定删除选中的 ${selectedItems.length} 件商品吗？`,
      success: (res) => {
        if (res.confirm) {
          // 只保留未选中的商品
          const remainingItems = cartList.filter(i => !i.checked);
          this.setData({ cartList: remainingItems });
          this.groupItemsByRegion(remainingItems);
          this.calcTotal();
          this.syncCart(remainingItems);
        }
      }
    });
  },

  // ========== 数量加减 ==========
  handleMinus(e) {
    const id = String(e.currentTarget.dataset.id);
    const type = e.currentTarget.dataset.type;
    const cartList = this.data.cartList;
    const index = cartList.findIndex(i => String(i.id) === id && i.type === type);
    if (index === -1) return;

    if (cartList[index].count <= 1) {
      // 数量为1再减则删除
      wx.showModal({
        title: '提示',
        content: '确定删除该商品吗？',
        success: (res) => {
          if (res.confirm) {
            cartList.splice(index, 1);
            this.setData({ cartList });
            this.groupItemsByRegion(cartList);
            this.calcTotal();
            this.syncCart(cartList);
          }
        }
      });
    } else {
      cartList[index].count--;
      this.setData({ cartList });
      this.groupItemsByRegion(cartList);
      this.calcTotal();
      this.syncCart(cartList);
    }
  },

  handlePlus(e) {
    const id = String(e.currentTarget.dataset.id);
    const type = e.currentTarget.dataset.type;
    const cartList = this.data.cartList;
    const index = cartList.findIndex(i => String(i.id) === id && i.type === type);
    if (index === -1) return;

    // 检查库存
    if (cartList[index].stock && cartList[index].count >= cartList[index].stock) {
      wx.showToast({ title: '库存不足', icon: 'none' });
      return;
    }

    cartList[index].count++;
    this.setData({ cartList });
    this.groupItemsByRegion(cartList);
    this.calcTotal();
    this.syncCart(cartList);
  },

  // ========== 加载桌号 ==========
  loadTableNumber() {
    const tableNumber = wx.getStorageSync('tableNumber');
    this.setData({ tableNumber: tableNumber ? Number(tableNumber) : null });
  },

  // ========== 获取地址列表（API） ==========
  getUserAddressList() {
    request({
      url: '/api/address/list',
      method: 'GET'
    }).then((data) => {
      const addressList = Array.isArray(data) ? data : [];
      const processedAddressList = addressList.map((address, index) => ({
        id: address.id || String(index + 1),
        name: address.name || '',
        phone: address.phone || '',
        address: address.address || '',
        isDefault: address.isDefault || false
      }));

      const defaultAddress = processedAddressList.find(item => item.isDefault) || (processedAddressList.length > 0 ? processedAddressList[0] : null);
      const selectedAddress = defaultAddress ? defaultAddress.id : null;

      this.setData({
        addressList: processedAddressList,
        selectedAddress,
        defaultAddress
      });
    }).catch((err) => {
      console.error('获取地址列表失败:', err);
      this.setData({
        addressList: [],
        selectedAddress: null,
        defaultAddress: null
      });
    });
  },

  // ========== 选择地址 ==========
  selectAddress(e) {
    const id = e.currentTarget.dataset.id;
    const selectedAddressInfo = this.data.addressList.find(item => item.id === id);
    this.setData({
      selectedAddress: id,
      defaultAddress: selectedAddressInfo || this.data.defaultAddress,
      showAllAddresses: false
    });
  },

  // ========== 展开/收起地址列表 ==========
  toggleAddressList() {
    this.setData({ showAllAddresses: !this.data.showAllAddresses });
  },

  // ========== 设为默认地址 ==========
  setDefaultAddress(e) {
    const id = e.currentTarget.dataset.id;
    const addressList = this.data.addressList;
    addressList.forEach(a => {
      a.isDefault = a.id === id;
    });
    const defaultAddress = addressList.find(a => a.id === id);
    this.setData({ addressList, selectedAddress: id, defaultAddress });
  },

  // ========== 编辑地址 ==========
  editAddress(e) {
    const addressId = e.currentTarget.dataset.id;
    wx.navigateTo({
      url: '/user-pages/address/address?id=' + addressId
    });
  },

  // ========== 添加地址 ==========
  addAddress() {
    wx.navigateTo({
      url: '/user-pages/address/address'
    });
  },

  // ========== 跳转到选择桌号 ==========
  goToOrder() {
    wx.navigateTo({
      url: '/user-pages/order/order'
    });
  },

  // ========== 购物车图标点击 ==========
  handleCartIconClick() {
    this.setData({ showCartDetail: true });
  },

  // ========== 结算入口 ==========
  handleSettle() {
    const { cartList, hasSelectedFood, tableNumber, regions } = this.data;
    const selectedItems = cartList.filter(i => i.checked);
    if (selectedItems.length === 0) {
      wx.showToast({ title: '请先选择商品', icon: 'none' });
      return;
    }

    // 检查是否有选中的点餐商品但没有桌号
    const hasFoodSelected = regions.food.items.some(i => i.checked);
    if (hasFoodSelected && !tableNumber) {
      wx.showToast({ title: '请先选择桌号', icon: 'none' });
      return;
    }

    // 判断是否需要分别结算（同时选了点餐和商品）
    const hasGoodsSelected = regions.goods.items.some(i => i.checked);
    if (hasFoodSelected && hasGoodsSelected) {
      this.setData({ showSeparateSettleModal: true });
    } else if (hasFoodSelected) {
      // 只有点餐，直接跳转确认订单
      wx.navigateTo({
        url: '/user-pages/confirm-order/confirm-order?type=food&tableNumber=' + (tableNumber || '')
      });
    } else {
      // 只有商品，显示确认购买弹窗
      this.setData({ showModal: true });
    }
  },

  // ========== 确认购买弹窗 ==========
  handleConfirmPurchase() {
    const { regions } = this.data;
    const hasFoodSelected = regions.food.items.some(i => i.checked);
    const hasGoodsSelected = regions.goods.items.some(i => i.checked);

    if (hasGoodsSelected && !hasFoodSelected) {
      // 只有商品，直接创建订单
      if (!this.data.selectedAddress) {
        wx.showToast({ title: '请先选择收货地址', icon: 'none' });
        return;
      }
      this.createGoodsOrder();
    } else if (hasFoodSelected && !hasGoodsSelected) {
      // 只有点餐，走点餐下单
      this.createOrderByType('food');
    } else {
      // 两种都有，分别结算
      this.setData({ showModal: false, showSeparateSettleModal: true });
    }
  },

  // ========== 按类型下单 ==========
  createOrderByType(type) {
    const { cartList, tableNumber } = this.data;
    const items = cartList.filter(i => i.checked && (type === 'food' ? i.type === 'food' : i.type === 'goods'));

    if (items.length === 0) return;

    if (type === 'food') {
      // 点餐下单，跳转到确认订单
      wx.navigateTo({
        url: '/user-pages/confirm-order/confirm-order?type=food&tableNumber=' + (tableNumber || '')
      });
    }
  },

  // ========== 点餐结算（分别结算弹窗中） ==========
  settleFood() {
    this.setData({ showSeparateSettleModal: false }, () => {
      wx.navigateTo({
        url: '/user-pages/confirm-order/confirm-order?type=food&tableNumber=' + (this.data.tableNumber || '')
      });
    });
  },

  // ========== 商品结算（分别结算弹窗中） ==========
  settleGoods() {
    // 检查是否选择了收货地址
    if (!this.data.selectedAddress) {
      wx.showToast({ title: '请先选择收货地址', icon: 'none' });
      return;
    }
    
    this.createGoodsOrder();
  },

  // ========== 创建商品订单 ==========
  createGoodsOrder() {
    const { cartList, selectedAddress } = this.data;
    const items = cartList.filter(i => i.checked && i.type === 'goods');
    
    if (items.length === 0) return;

    const payload = {
      addressId: parseInt(selectedAddress),
      items: items.map(item => ({
        id: parseInt(item.id || '0'),
        price: Number((item.price || 0).toString().replace(/[¥￥]/g, '')),
        quantity: Number(item.count || 1)
      }))
    };

    wx.showLoading({ title: '创建订单中...' });
    
    const api = require('../../utils/api').api || require('../../utils/api');
    api.order.createCommodity(payload)
      .then((data) => {
        wx.hideLoading();
        const orderId = data.orderId || data.id;
        if (!orderId) {
          wx.showToast({ title: '创建订单失败', icon: 'none' });
          return;
        }
        // 清空已下单的商品
        this.clearOrderedGoods(items);
        wx.redirectTo({
          url: '/user-pages/orders/orders?tab=pending'
        });
      })
      .catch(() => {
        wx.hideLoading();
        // 这里的错误提示已经在 request 封装里处理了
      });
  },

  // ========== 清空已下单的商品 ==========
  clearOrderedGoods(orderedItems) {
    try {
      const cartList = this.data.cartList.filter(cartItem => {
        if (cartItem.type !== 'goods' || !cartItem.checked) {
          return true;
        }
        const cartItemId = String(cartItem.id);
        return !orderedItems.some(item => String(item.id) === cartItemId);
      });
      
      this.setData({ cartList });
      this.syncCart(cartList);
    } catch (e) {
      console.error('清空已下单商品失败', e);
    }
  },

  // ========== 关闭弹窗 ==========
  handleCancelModal() {
    this.setData({ showModal: false });
  },

  handleCloseCartDetail() {
    this.setData({ showCartDetail: false });
  },

  handleCloseSeparateSettleModal() {
    this.setData({ showSeparateSettleModal: false });
  }
});

