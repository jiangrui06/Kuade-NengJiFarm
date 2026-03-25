const api = require('../../utils/api'); 

Page({ 
  data: { 
    cartList: [], 
    totalPrice: '0.00', 
    selectedCount: 0, 
    showModal: false,
    showCartDetail: false,
    selectAll: false,
    addresses: [],
    selectedAddressId: null
  }, 

  onLoad() { 
    this.restoreCart(); 
    this.loadAddresses(); 
  }, 

  onShow() { 
    this.restoreCart(); 
  }, 

  restoreCart() { 
    const cartList = (wx.getStorageSync('cartList') || []).map(item => ({ 
      ...item, 
      checked: !!item.checked, 
      count: Number(item.count || 0) 
    })); 

    this.setData({ cartList }); 
    this.calcTotal(); 
  },

  loadAddresses() {
    // 模拟加载地址列表
    const addresses = [
      { id: 1, name: '张三', phone: '138****1234', address: '北京市朝阳区xxx街道xxx号' },
      { id: 2, name: '李四', phone: '139****5678', address: '上海市浦东新区xxx街道xxx号' }
    ];
    this.setData({ 
      addresses: addresses,
      selectedAddressId: addresses.length > 0 ? addresses[0].id : null
    });
  },

  syncCart(cartList) { 
    const normalizedCartList = cartList 
      .filter(item => Number(item.count || 0) > 0) 
      .map(item => ({ 
        id: String(item.id), 
        name: item.name || '', 
        price: Number(item.price || 0), 
        image: item.image || '', 
        count: Number(item.count || 0), 
        checked: !!item.checked 
      })); 

    this.setData({ cartList: normalizedCartList }); 
    this.calcTotal(); 
    wx.setStorageSync('cartList', normalizedCartList); 

    api.request({ 
      url: '/api/cart/items', 
      method: 'POST', 
      data: { 
        cartList: normalizedCartList 
      } 
    }) 
      .then((data) => { 
        const nextCartList = (data.cartList || []).map(item => ({ 
          ...item, 
          checked: !!item.checked, 
          count: Number(item.count || 0) 
        })); 

        this.setData({ cartList: nextCartList }); 
        this.calcTotal(); 
        wx.setStorageSync('cartList', nextCartList); 
      }) 
      .catch((err) => { 
        console.error('sync cart failed:', err); 
        wx.showToast({ 
          title: '购物车同步失败', 
          icon: 'none' 
        }); 
      }); 
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

    // 更新标签栏徽章
    if (selectedCount > 0) { 
      wx.setTabBarBadge({ 
        index: 2, 
        text: selectedCount.toString() 
      }); 
    } else { 
      wx.removeTabBarBadge({ 
        index: 2 
      }); 
    } 
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

    // 然后显示结算弹窗
    this.setData({ showModal: true }); 
  }, 

  handleConfirmPurchase() { 
    this.setData({ showModal: false }, () => { 
      wx.navigateTo({ 
        url: '../buy/buy?orderId=1' 
      }); 
    }); 
  }, 

  handleCancelModal() { 
    this.setData({ showModal: false }); 
  },

  selectAddress(e) {
    const addressId = e.currentTarget.dataset.id;
    this.setData({ selectedAddressId: addressId });
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
  } 
});