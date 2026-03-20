const api = require('../../utils/api'); 

Page({ 
  data: { 
    cartList: [], 
    totalPrice: '0.00', 
    selectedCount: 0, 
    showModal: false,
    selectAll: false
  }, 

  onLoad() { 
    this.restoreCart(); 
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
      url: '/api/AppCart/Appcart', 
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

    cartList[itemIndex].count += 1; 
    this.syncCart(cartList); 
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

    this.setData({ showModal: true }); 
  }, 

  handleConfirmPurchase() { 
    this.setData({ showModal: false }, () => { 
      wx.navigateTo({ 
        url: '../buy/buy' 
      }); 
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
  } 
});