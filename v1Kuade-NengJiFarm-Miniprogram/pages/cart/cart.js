// pages/cart/cart.js
const { request } = require('../../utils/api');

Page({

  data: {
    cartList: [],
    regions: {
      food: { name: '点餐', items: [], selected: false, hasChecked: false, totalPrice: 0, previewImages: [], moreCount: 0, checkedItemNames: [] },
      goods: { name: '商品', items: [], selected: false, hasChecked: false, totalPrice: 0, previewImages: [], moreCount: 0, checkedItemNames: [] }
    },
    totalPrice: 0,
    selectedCount: 0,
    selectAll: false,
    canSettle: false,
    tableNumber: null,
    hasSelectedFood: false,

    // 商品类型结算专用字段
    goodsCheckedCount: 0,
    goodsTotalPrice: 0,

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
    
    // 检查是否有从地址选择页面返回的选中地址
    const selectedAddressId = wx.getStorageSync('selectedAddressId');
    if (selectedAddressId) {
      wx.removeStorageSync('selectedAddressId');
      // 更新选中的地址
      this.setData({
        selectedAddress: selectedAddressId
      });
      // 刷新地址列表以更新默认地址显示
      this.getUserAddressList();
    }
    
    if (typeof this.getTabBar === 'function' && this.getTabBar()) {
      this.getTabBar().init();
    }
  },

  onHide() {
    this.syncCart(this.data.cartList);
  },

  // ========== 购物车数据恢复 ==========
  restoreCart() {
    try {
      let cartList = [];

      const rawCartList = wx.getStorageSync('cartList');
      const goodsArray = Array.isArray(rawCartList) ? rawCartList : Object.values(rawCartList || {});
      
      const goodsCart = goodsArray.map(item => {
        if (!item || typeof item !== 'object') {
          return null;
        }
        const itemQuantity = Number(item.count || item.quantity || 0);
        return {
          ...item,
          checked: !!item.checked,
          count: itemQuantity,
          quantity: itemQuantity,
          price: Number((item.price || 0).toString().replace(/[¥￥]/g, '')),
          stock: Number(item.stock || 0),
          type: item.type || 'goods',
          _cartKey: 'goods_' + String(item.id),
          image: this.processImageUrl(item.image || ''),
          isFarmGood: !!item.isFarmGood
        };
      }).filter(Boolean);
      cartList.push(...goodsCart);

      const orderCart = wx.getStorageSync('orderCart') || {};
      if (orderCart && typeof orderCart === 'object' && orderCart !== null) {
        for (const key in orderCart) {
          const item = orderCart[key];
          if (!item || typeof item !== 'object') continue;
          const itemCount = Number(item.count || item.quantity || 0);
          if (itemCount <= 0) continue;
          const itemId = String(item.id || key);
          cartList.push({
            id: itemId,
            name: item.name || '',
            price: Number((item.price || 0).toString().replace(/[¥￥]/g, '')),
            image: this.processImageUrl(item.image || ''),
            count: itemCount,
            quantity: itemCount,
            checked: !!item.checked,
            type: 'food',
            stock: Number(item.stock || 0),
            _cartKey: 'food_' + itemId
          });
        }
      }

      this.setData({ cartList });
      this.groupItemsByRegion(cartList);
      this.calcTotal();
    } catch (error) {
      console.log('恢复购物车出错:', error);
      this.setData({ cartList: [] });
      this.groupItemsByRegion([]);
      this.calcTotal();
    }
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
    
    // 商品类型结算专用字段
    let goodsCheckedCount = 0;
    let goodsTotalPrice = 0;

    cartList.forEach(item => {
      if (item.checked) {
        totalPrice += item.price * item.count;
        selectedCount += item.count;
        
        // 计算商品类型的金额和数量
        if (item.type === 'goods') {
          goodsCheckedCount += item.count;
          goodsTotalPrice += item.price * item.count;
        }
      } else {
        selectAll = false;
      }
    });

    this.setData({
      totalPrice: totalPrice.toFixed(2),
      selectedCount,
      selectAll,
      canSettle: selectedCount > 0,
      goodsCheckedCount,
      goodsTotalPrice: goodsTotalPrice.toFixed(2)
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
        quantity: Number(item.count || 0),
        checked: !!item.checked,
        type: item.type || 'goods',
        stock: Number(item.stock || 0),
        _cartKey: (item.type || 'goods') + '_' + String(item.id),
        isFarmGood: !!item.isFarmGood
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
      quantity: i.count,
      checked: i.checked,
      stock: i.stock,
      isFarmGood: i.isFarmGood
    })));

    // 同步点餐到 Storage
    const orderCart = {};
    foodItems.forEach(item => {
      const itemId = String(item.id || item._cartKey || '');
      if (!itemId) return;
      orderCart[itemId] = {
        id: itemId,
        name: item.name || '',
        price: item.price,
        image: item.image || '',
        count: item.count,
        quantity: item.count,
        stock: item.stock || 0,
        checked: item.checked
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
      cartList[index].quantity = cartList[index].count;
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

    if (cartList[index].isFarmGood) {
      const purchasedFarmGoods = wx.getStorageSync('purchasedFarmGoods') || [];
      if (purchasedFarmGoods.includes(id)) {
        wx.showToast({ title: '该商品每人限购一份', icon: 'none' });
        return;
      }
    }

    if (cartList[index].isFarmGood && cartList[index].count >= 1) {
      wx.showToast({ title: '该商品每人限购一份', icon: 'none' });
      return;
    }

    if (cartList[index].stock && cartList[index].count >= cartList[index].stock) {
      wx.showToast({ title: '库存不足', icon: 'none' });
      return;
    }

    cartList[index].count++;
    cartList[index].quantity = cartList[index].count;
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
        detail: address.detail || '',
        isDefault: address.isDefault || false
      }));

      const defaultAddress = processedAddressList.find(item => item.isDefault) || (processedAddressList.length > 0 ? processedAddressList[0] : null);
      
      // 优先使用已选中的地址，如果没有选中的地址则使用默认地址
      let selectedAddress = this.data.selectedAddress;
      if (!selectedAddress && defaultAddress) {
        selectedAddress = defaultAddress.id;
      }
      
      // 确保选中的地址存在于地址列表中
      const isValidAddress = processedAddressList.some(item => item.id === selectedAddress);
      if (!isValidAddress && defaultAddress) {
        selectedAddress = defaultAddress.id;
      }

      // 更新默认地址显示为选中的地址
      let displayAddress = defaultAddress;
      if (selectedAddress) {
        displayAddress = processedAddressList.find(item => item.id === selectedAddress) || defaultAddress;
      }

      this.setData({
        addressList: processedAddressList,
        selectedAddress,
        defaultAddress: displayAddress
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

  // ========== 跳转到地址选择页面 ==========
  goToAddress() {
    wx.navigateTo({
      url: '/user-pages/address/address?from=buy'
    });
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
    const { cartList, hasSelectedFood, tableNumber, regions, selectedCount } = this.data;

    // 用 selectedCount（与底部结算按钮显示一致）作为首选判断
    // 兜底用 regions 中各区域 checked 状态二次确认
    const hasFoodChecked = regions.food.items.some(i => i.checked);
    const hasGoodsChecked = regions.goods.items.some(i => i.checked);

    if (selectedCount <= 0 && !hasFoodChecked && !hasGoodsChecked) {
      wx.showToast({ title: '请先选择商品', icon: 'none' });
      return;
    }

    if (hasFoodChecked && !tableNumber) {
      wx.showToast({ title: '请先选择桌号', icon: 'none' });
      return;
    }

    if (hasFoodChecked && hasGoodsChecked) {
      this.setData({ showSeparateSettleModal: true });
    } else if (hasFoodChecked) {
      wx.navigateTo({
        url: '/user-pages/confirm-order/confirm-order?type=food&tableNumber=' + (tableNumber || '')
      });
    } else {
      this.setData({ showModal: true });
    }
  },

  // ========== 确认购买弹窗 ==========
  handleConfirmPurchase() {
    // 先保存当前购物车状态
    this.syncCart(this.data.cartList);
    
    const { cartList } = this.data;
    const goodsItems = cartList.filter(i => i.type === 'goods' && i.checked);
    const foodItems = cartList.filter(i => i.type === 'food' && i.checked);

    console.log('[cart] handleConfirmPurchase - goodsItems:', goodsItems.length, 'foodItems:', foodItems.length);
    
    if (goodsItems.length > 0) {
      if (!this.data.selectedAddress) {
        wx.showToast({ title: '请先选择收货地址', icon: 'none' });
        return;
      }
      this.createGoodsOrder();
    } else if (foodItems.length > 0) {
      this.createOrderByType('food');
    } else {
      wx.showToast({ title: '请选择商品', icon: 'none' });
    }
  },

  // ========== 同时创建点餐和商品订单 ==========
  createBothOrders() {
    const { cartList, selectedAddress, tableNumber } = this.data;
    const foodItems = cartList.filter(i => i.checked && i.type === 'food');
    const goodsItems = cartList.filter(i => i.checked && i.type === 'goods');
    
    // 检查商品地址
    if (goodsItems.length > 0 && !selectedAddress) {
      wx.showToast({ title: '请先选择收货地址', icon: 'none' });
      return;
    }

    // 检查点餐桌号
    if (foodItems.length > 0 && !tableNumber) {
      wx.showToast({ title: '请先选择桌号', icon: 'none' });
      return;
    }

    const api = require('../../utils/api').api || require('../../utils/api');
    let ordersCreated = 0;
    const totalOrders = (foodItems.length > 0 ? 1 : 0) + (goodsItems.length > 0 ? 1 : 0);

    const checkAndRedirect = () => {
      ordersCreated++;
      if (ordersCreated >= totalOrders) {
        wx.redirectTo({
          url: '/user-pages/orders/orders?tab=pending'
        });
      }
    };

    // 创建点餐订单
    if (foodItems.length > 0) {
      const foodPayload = {
        sourceType: 'food',
        sourceName: '点餐',
        tableId: tableNumber,
        quantity: foodItems.reduce((sum, item) => sum + Number(item.count || 0), 0),
        totalPrice: foodItems.reduce((sum, item) => sum + Number(item.price || 0) * Number(item.count || 0), 0),
        items: foodItems.map(item => ({
          Id: parseInt(item.id || '0'),
          Name: item.name || '',
          Price: Number((item.price || 0).toString().replace(/[¥￥]/g, '')),
          Quantity: Number(item.count || 1),
          Image: item.image || ''
        }))
      };
      
      api.order.createDish(foodPayload)
        .then(() => {
          checkAndRedirect();
        })
        .catch(() => {
          checkAndRedirect();
        });
    }

    // 创建商品订单
    if (goodsItems.length > 0) {
      const goodsPayload = {
        addressId: parseInt(selectedAddress),
        items: goodsItems.map(item => ({
          id: parseInt(item.id || '0'),
          price: Number((item.price || 0).toString().replace(/[¥￥]/g, '')),
          quantity: Number(item.count || 1)
        }))
      };
      
      api.order.createCommodityV2(goodsPayload)
        .then(() => {
          checkAndRedirect();
        })
        .catch(() => {
          checkAndRedirect();
        });
    }
  },

  // ========== 按类型下单 ==========
  createOrderByType(type) {
    const { cartList, tableNumber } = this.data;
    const items = cartList.filter(i => i.checked && (type === 'food' ? i.type === 'food' : i.type === 'goods'));

    if (items.length === 0) return;

    if (type === 'food') {
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
    if (!this.data.selectedAddress) {
      wx.showToast({ title: '请先选择收货地址', icon: 'none' });
      return;
    }
    
    // 保存当前购物车状态，确保选中状态不会丢失
    this.syncCart(this.data.cartList);
    
    // 关闭分别结算弹窗，显示确认购买弹窗
    this.setData({ showSeparateSettleModal: false }, () => {
      this.setData({ showModal: true });
    });
  },

  // ========== 创建商品订单 ==========
  createGoodsOrder() {
    const { cartList, selectedAddress } = this.data;
    const items = cartList.filter(i => i.checked && i.type === 'goods');
    
    console.log('[cart] createGoodsOrder 开始, 选中商品:', items.length, '购物车总数:', cartList.length);
    
    if (items.length === 0) return;

    const payload = {
      addressId: parseInt(selectedAddress),
      items: items.map(item => ({
        id: parseInt(item.id || '0'),
        price: Number((item.price || 0).toString().replace(/[¥￥]/g, '')),
        quantity: Number(item.count || 1)
      }))
    };

    const api = require('../../utils/api').api || require('../../utils/api');
    api.order.createCommodityV2(payload)
      .then((data) => {
        const orderId = data.orderId || data.id;
        console.log('[cart] 订单创建成功, orderId:', orderId);
        
        if (!orderId) {
          wx.showToast({ title: '创建订单失败', icon: 'none' });
          return;
        }
        
        // 创建订单成功后，移除已选中的商品
        const remainingItems = cartList.filter(i => !(i.checked && i.type === 'goods'));
        this.setData({ cartList: remainingItems });
        this.groupItemsByRegion(remainingItems);
        this.calcTotal();
        this.syncCart(remainingItems);
        
        // 计算订单金额
        const totalPrice = items.reduce((sum, item) => {
          return sum + Number(item.price || 0) * Number(item.count || 1);
        }, 0);
        
        // 关闭弹窗并跳转到支付页面
        this.setData({ showModal: false });
        wx.redirectTo({
          url: `/user-pages/pay/pay?orderId=${orderId}&type=goods&totalPrice=${totalPrice}`
        });
      })
      .catch((err) => {
        console.error('[cart] 创建订单失败:', err);
        wx.showToast({ title: '创建订单失败', icon: 'none' });
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
  },

  // ========== 跳转到商品/点餐详情页 ==========
  goToDetail(e) {
    const id = e.currentTarget.dataset.id;
    const type = e.currentTarget.dataset.type;
    
    if (type === 'food') {
      // 点餐跳转到菜品详情页
      wx.navigateTo({
        url: '/user-pages/order-foods-detail/order-foods-detail?id=' + id
      });
    } else {
      // 商品跳转到商品详情页
      wx.navigateTo({
        url: '/user-pages/goods-detail/goods-detail?id=' + id
      });
    }
  }
});