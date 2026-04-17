const api = require('../../utils/api'); 

Page({ 
  data: { 
    cartList: [], 
    totalPrice: '0.00', 
    selectedCount: 0, 
    showModal: false,
    showSeparateSettleModal: false,
    showCartDetail: false,
    selectAll: false,
    addressList: [],
    selectedAddress: null,
    // 按类型分组
    regions: {
      food: { name: '点餐', selected: false, items: [], totalPrice: '0.00' },
      goods: { name: '商品', selected: false, items: [], totalPrice: '0.00' }
    },
    // 到店吃选项
    isTakeAway: false
  }, 

  onLoad() { 
    this.restoreCart(); 
    this.getUserAddressList();
  }, 

  onShow() { 
    this.restoreCart(); 
    this.getUserAddressList();
  },

  getUserAddressList() {
    api.api.user.getAddressList()
      .then((data) => {
        const addresses = Array.isArray(data) ? data : [];
        console.log('用户地址列表:', addresses);
        if (addresses && addresses.length > 0) {
          const defaultAddress = addresses.find(item => item.isDefault) || addresses[0];
          this.setData({
            addressList: addresses,
            selectedAddress: defaultAddress.id
          });
        } else {
          this.setData({
            addressList: [],
            selectedAddress: null
          });
        }
      })
      .catch((err) => {
        console.error('获取地址列表失败:', err);
        this.setData({
          addressList: [],
          selectedAddress: null
        });
      });
  }, 

  restoreCart() { 
    const cartList = (wx.getStorageSync('cartList') || []).map(item => ({ 
      ...item, 
      checked: !!item.checked, 
      count: Number(item.count || 0),
      price: Number((item.price || 0).toString().replace(/[¥￥]/g, '')),
      // 添加默认值
      type: item.type || 'goods' // food: 点餐, goods: 商品
    })); 

    this.setData({ cartList }); 
    this.groupItemsByRegion();
    this.calcTotal(); 
  },

  // 按类型分组商品
  groupItemsByRegion() {
    const cartList = this.data.cartList;
    const regions = {
      food: { 
        name: '点餐', 
        selected: false, 
        items: [], 
        totalPrice: '0.00',
        checkedItems: [],
        checkedItemNames: [],
        previewImages: [],
        moreCount: 0
      }, 
      goods: { 
        name: '商品', 
        selected: false, 
        items: [], 
        totalPrice: '0.00',
        checkedItems: [],
        checkedItemNames: [],
        previewImages: [],
        moreCount: 0
      }
    };

    // 按照商品类型分组
    cartList.forEach(item => {
      if (item.type === 'food') {
        regions.food.items.push(item);
      } else {
        regions.goods.items.push(item);
      }
    });

    // 检查每个区域是否有选中的商品，并计算每个区域的总价格
    Object.keys(regions).forEach(key => {
      const region = regions[key];
      // 获取选中的商品
      const checkedItems = region.items.filter(item => item.checked);
      region.checkedItems = checkedItems;
      region.selected = checkedItems.length > 0;
      // 计算区域总价格
      const totalPrice = checkedItems.reduce((total, item) => total + (item.price * item.count), 0);
      region.totalPrice = totalPrice.toFixed(2);
      // 获取选中商品名称
      region.checkedItemNames = checkedItems.map(item => item.name);
      // 获取预览图片（最多3张）
      region.previewImages = checkedItems.slice(0, 3).map(item => item.image);
      // 计算超出数量
      region.moreCount = checkedItems.length > 3 ? checkedItems.length - 3 : 0;
    });

    this.setData({ regions });
  },



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
        type: item.type || 'goods' 
      })); 

    this.setData({ cartList: normalizedCartList }); 
    this.groupItemsByRegion();
    this.calcTotal(); 
    wx.setStorageSync('cartList', normalizedCartList); 

    // 购物车同步暂时使用本地存储，后端API暂未实现
    console.log('购物车已同步到本地存储');
    wx.setStorageSync('cartList', normalizedCartList); 
  }, 

  handleMinus(e) { 
    const id = String(e.currentTarget.dataset.id); 
    const cartList = this.data.cartList.map(item => ({ ...item })); 
    const itemIndex = cartList.findIndex(item => String(item.id) === id); 

    if (itemIndex === -1) { 
      return; 
    } 

    if (cartList[itemIndex].count <= 1) { 
      cartList.splice(itemIndex, 1); 
    } else { 
      cartList[itemIndex].count -= 1; 
    } 

    this.syncCart(cartList); 
  }, 

  handlePlus(e) {
    const id = String(e.currentTarget.dataset.id);
    const cartList = this.data.cartList.map(item => ({ ...item }));
    const itemIndex = cartList.findIndex(item => String(item.id) === id);

    if (itemIndex === -1) {
      return;
    }

    // 确保商品数量不超过库存上限
    const stock = cartList[itemIndex].stock || 10;
    if (cartList[itemIndex].count < stock) {
      cartList[itemIndex].count += 1;
      this.syncCart(cartList);
    } else {
      wx.showToast({ 
        title: '已达到库存上限', 
        icon: 'none' 
      });
    }
  }, 

  toggleSelect(e) { 
    const id = String(e.currentTarget.dataset.id); 
    const cartList = this.data.cartList.map(item => ({ ...item })); 
    const itemIndex = cartList.findIndex(item => String(item.id) === id); 

    if (itemIndex === -1) { 
      return; 
    } 

    cartList[itemIndex].checked = !cartList[itemIndex].checked; 
    this.syncCart(cartList); 
  }, 

  calcTotal() { 
    let totalPrice = 0; 
    let selectedCount = 0; 

    this.data.cartList.forEach((item) => { 
      if (item.checked) { 
        totalPrice += Number(item.price || 0) * Number(item.count || 0); 
        selectedCount += Number(item.count || 0); 
      } 
    }); 

    const selectAll = this.data.cartList.length > 0 && this.data.cartList.every(item => item.checked); 

    this.setData({ 
      totalPrice: totalPrice.toFixed(2), 
      selectedCount,
      selectAll
    }); 

 
  }, 

  getCheckedItemsByType(type) {
    return (this.data.cartList || []).filter(item => {
      if (!item.checked) {
        return false;
      }

      if (!type) {
        return true;
      }

      return (item.type || 'goods') === type;
    });
  },

  buildAddressPayload() {
    const selected = (this.data.addressList || []).find(
      item => String(item.id) === String(this.data.selectedAddress)
    );

    if (!selected) {
      return null;
    }

    return {
      addressId: selected.id,
      name: selected.name || '',
      phone: selected.phone || '',
      address: selected.address || ''
    };
  },

  createOrderByType(type) {
    const items = this.getCheckedItemsByType(type);
    if (!items.length) {
      wx.showToast({
        title: '请选择商品',
        icon: 'none'
      });
      return;
    }

    const needAddress = type === 'goods' || (type === 'food' && !this.data.isTakeAway);
    const address = this.buildAddressPayload();
    if (needAddress && !address) {
      wx.showToast({
        title: '请选择收货地址',
        icon: 'none'
      });
      return;
    }

    const quantity = items.reduce((sum, item) => sum + Number(item.count || 0), 0);
    const totalPrice = items.reduce(
      (sum, item) => sum + Number(item.price || 0) * Number(item.count || 0),
      0
    );

    const payload = {
      sourceType: type,
      sourceName: type === 'food' ? '点餐' : '商品',
      quantity: quantity > 0 ? quantity : 1,
      totalPrice: Number(totalPrice.toFixed(2)),
      items: items.map(item => ({
        id: String(item.id || ''),
        name: item.name || (type === 'food' ? '点餐' : '商品'),
        price: Number(item.price || 0),
        quantity: Number(item.count || 1),
        image: item.image || ''
      }))
    };

    if (address) {
      payload.address = address;
    }

    wx.showLoading({ title: '下单中...' });
    api.api.order.create(payload)
      .then((orderData) => {
        const orderId = orderData.orderId || orderData.id;
        if (!orderId) {
          wx.showToast({
            title: '创建订单失败',
            icon: 'none'
          });
          return;
        }

        const checkoutMap = new Set(
          items.map(item => `${item.type || 'goods'}:${String(item.id)}`)
        );
        const remain = (this.data.cartList || []).filter(
          item => !checkoutMap.has(`${item.type || 'goods'}:${String(item.id)}`)
        );
        this.syncCart(remain);

        wx.navigateTo({
          url: '/subpkg/orders/orders?tab=pending'
        });
      })
      .catch((err) => {
        console.error('创建购物车订单失败:', err);
        wx.showToast({
          title: '下单失败',
          icon: 'none'
        });
      })
      .finally(() => {
        wx.hideLoading();
      });
  },

  handleSettle() { 
    if (this.data.selectedCount === 0) { 
      wx.showToast({ 
        title: '请先选择商品', 
        icon: 'none' 
      }); 
      return; 
    } 

    // 先关闭购物车详情弹窗
    if (this.data.showCartDetail) {
      this.setData({ showCartDetail: false });
    }

    // 检查选中的区域
    const hasFood = this.data.regions.food.selected;
    const hasGoods = this.data.regions.goods.selected;

    // 如果同时选中了点餐和商品区域，显示分别结算弹窗
    if (hasFood && hasGoods) {
      this.setData({ showSeparateSettleModal: true });
    } else {
      // 否则显示确认购买弹窗
      this.setData({ showModal: true });
    }
  }, 

  handleConfirmPurchase() { 
    // 检查是否选择了地址（仅外卖需要）
    if (!this.data.isTakeAway && this.data.regions.food.selected && !this.data.selectedAddress) {
      wx.showToast({
        title: '请选择收货地址',
        icon: 'none'
      });
      return;
    }

    // 检查是否有选中的商品
    if (this.data.selectedCount === 0) {
      wx.showToast({
        title: '请选择商品',
        icon: 'none'
      });
      return;
    }

    // 检查选中的区域
    const hasFood = this.data.regions.food.selected;
    const hasGoods = this.data.regions.goods.selected;

    this.setData({ showModal: false }, () => {
      if (hasFood && hasGoods) {
        this.setData({ showSeparateSettleModal: true });
        return;
      }

      if (hasFood) {
        this.createOrderByType('food');
        return;
      }

      if (hasGoods) {
        this.createOrderByType('goods');
      }
    });
  }, 

  handleCancelModal() { 
    this.setData({ showModal: false }); 
  },

 

  navTo(e) { 
    const pageMap = { 
      home: '/pages/index/index', 
      activity: '/pages/activity/activity', 
      cart: '/pages/cart/cart', 
      mine: '/pages/profile/profile' 
    }; 

    wx.switchTab({ 
      url: pageMap[e.currentTarget.dataset.page] 
    }); 
  },

  handleSelectAll() {
    const selectAll = !this.data.selectAll;
    const cartList = this.data.cartList.map(item => ({
      ...item,
      checked: selectAll
    }));
    this.syncCart(cartList);
  },

  handleClearCart() {
    wx.showModal({
      title: '确认清空',
      content: '确定要清空购物车吗？',
      success: (res) => {
        if (res.confirm) {
          this.setData({ 
            cartList: [],
            selectAll: false
          });
          this.calcTotal();
          wx.removeStorageSync('cartList');
          wx.showToast({ title: '购物车已清空', icon: 'success' });
        }
      }
    });
  },

  handleCartIconClick() {
    this.setData({ showCartDetail: true });
  },

  handleCloseCartDetail() {
    this.setData({ showCartDetail: false });
  },

  selectAddress(e) {
    const addressId = e.currentTarget.dataset.id;
    this.setData({
      selectedAddress: addressId
    });
  },

  addAddress() {
    wx.navigateTo({
      url: '/subpkg/address/address'
    });
  },

  // 切换区域选中状态
  toggleRegionSelect(e) {
    const region = e.currentTarget.dataset.region;
    const currentRegionSelected = this.data.regions[region].selected;
    const cartList = this.data.cartList.map(item => ({
      ...item,
      // 如果商品属于当前区域，设置选中状态为当前状态的反值
      checked: item.type === region ? !currentRegionSelected : item.checked
    }));
    
    this.syncCart(cartList);
  },

  // 切换到店吃选项
  toggleTakeAway(e) {
    this.setData({
      isTakeAway: e.detail.value
    });
  },

  // 关闭分别结算弹窗
  handleCloseSeparateSettleModal() {
    this.setData({ showSeparateSettleModal: false });
  },

  // 结算点餐区域
  settleFood() {
    // 检查是否选择了地址（仅外卖需要）
    if (!this.data.isTakeAway && !this.data.selectedAddress) {
      wx.showToast({
        title: '请选择收货地址',
        icon: 'none'
      });
      return;
    }

    // 获取选中的点餐商品
    const foodItems = this.getCheckedItemsByType('food');
    if (!foodItems.length) {
      wx.showToast({
        title: '请选择点餐商品',
        icon: 'none'
      });
      return;
    }

    // 同步到 orderCart，格式转换为 order 页面需要的格式
    const orderCart = {};
    foodItems.forEach(item => {
      orderCart[item.id] = {
        ...item,
        quantity: item.count,
        price: parseFloat(item.price)
      };
    });

    try {
      wx.setStorageSync('orderCart', orderCart);
    } catch (e) {}

    this.setData({ showSeparateSettleModal: false }, () => {
      // 点餐跳转到 order 页面
      wx.navigateTo({
        url: '/subpkg/order/order'
      });
    });
  },

  // 结算商品区域
  settleGoods() {
    const goodsItems = this.getCheckedItemsByType('goods');
    if (!goodsItems.length) {
      wx.showToast({
        title: '请选择商品',
        icon: 'none'
      });
      return;
    }

    this.setData({ showSeparateSettleModal: false }, () => {
      // 商品跳转到 orders 支付页面
      this.createOrderByType('goods');
    });
  }
});