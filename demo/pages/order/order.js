const api = require('../../utils/api');

Page({
  data: {
    activeCategory: 'vegetables',
    categories: [],
    goodsList: {},
    pageMap: {},
    hasMoreMap: {},
    pageSize: 6,

    cart: {},
    cartItems: [],
    cartCount: 0,
    totalPrice: 0,

    loading: true,
    lazyLoading: false,
    showCartModal: false,

    tableNumber: null,
    showTableModal: false,
    tableList: [],

    showQrcodeModal: false,
    qrcodeUrl: '',
    qrcodeTableNumber: '6'
  },

  onLoad(options) {
     // 恢复购物车
     const cart = wx.getStorageSync('orderCart') || {};
     this.restoreCart(cart);
     // 获取桌台
     const tableNumber = wx.getStorageSync('tableNumber');
     if (tableNumber) {
       this.setData({ tableNumber });
     }
 
    // 处理扫码进入的情况
    if (options.scene) {
      try {
        const scene = decodeURIComponent(options.scene);
        if (scene.startsWith('table:')) {
          const tableNum = scene.split(':')[1];
          
          // 显示确认对话框
          wx.showModal({
            title: '确认桌台',
            content: `是否确认使用桌台 ${tableNum}？`,
            success: (res) => {
              if (res.confirm) {
                // 点击确认后设置桌台号码
                this.setData({ 
                  tableNumber: tableNum,
                  showQrcodeModal: false // 关闭二维码弹窗
                });
                wx.setStorageSync('tableNumber', tableNum);
                wx.showToast({ title: '扫码成功', icon: 'success' });
              }
            }
          });
        }
      } catch (e) {
        console.error('解析扫码参数失败', e);
      }
    }

    try {
      const cart = wx.getStorageSync('orderCart') || {};
      this.restoreCart(cart);
    } catch (e) {}
    this.getTableList();
    this.getOrderData();
  },

  onShow() {
    // 页面显示时更新购物车数据和桌台号码
    try {
      const cart = wx.getStorageSync('orderCart') || {};
      this.restoreCart(cart);
      const tableNumber = wx.getStorageSync('tableNumber');
      if (tableNumber) {
        this.setData({ tableNumber });
      }
    } catch (e) {}
  },

  getOrderData() {
    wx.showLoading({ title: '加载中...' })
    api.request({
      url: '/api/order',
      method: 'GET',
      data: {
        categoryId: this.data.activeCategory,
        page: 1,
        pageSize: this.data.pageSize
      }
    }).then(data => {
      console.log("后端返回:", data)
      const categories = data.categories || []
      const currentCategory = data.currentCategory || 'vegetables'
      const goods = data.goodsList || []

      this.setData({
        activeCategory: currentCategory,
        categories: categories,
        goodsList: {
          [currentCategory]: goods
        },
        pageMap: {
          [currentCategory]: 1
        },
        hasMoreMap: {
          [currentCategory]: !!data.hasMore
        },


        goodsList: { [currentCategory]: goods },
        pageMap: { [currentCategory]: 1 },
        hasMoreMap: { [currentCategory]: !!data.hasMore },
        loading: false
      })
    }).catch(err => {
      console.error("加载失败", err)
      this.setData({ loading: false })
      wx.showToast({ title: '加载失败', icon: 'none' })
  

    }).finally(() => wx.hideLoading())
  },

  switchCategory(e) {
    const category = e.currentTarget.dataset.category
    if (category === this.data.activeCategory) return
    this.setData({ activeCategory: category })
    if (this.data.goodsList[category]) return
    this.loadCategoryGoods(category, false)
  },

  loadCategoryGoods(category, isLoadMore) {
    if (isLoadMore && this.data.lazyLoading) return
    const nextPage = isLoadMore ? (this.data.pageMap[category] || 0) + 1 : 1
    this.setData({ loading: !isLoadMore, lazyLoading: isLoadMore })

    setTimeout(() => {
      api.request({
        url: '/api/order',
        method: 'GET',
        data: { categoryId: category, page: nextPage, pageSize: this.data.pageSize }
      }).then(data => {
        const newGoods = data.goodsList || []
        const oldGoods = isLoadMore ? (this.data.goodsList[category] || []) : []
        this.setData({
          [`goodsList.${category}`]: oldGoods.concat(newGoods),
          [`pageMap.${category}`]: nextPage,
          [`hasMoreMap.${category}`]: !!data.hasMore,
          loading: false,
          lazyLoading: false
        })
      }).catch(() => {
        this.setData({ loading: false, lazyLoading: false })
      })
    }, 200)
  },

  addToCart(e) {
    if (!this.data.tableNumber) {
      wx.showToast({ title: '请选择桌台号码', icon: 'none' })
      return
    }
    const { category, index } = e.currentTarget.dataset
    const goods = this.data.goodsList[category][index]
    if (!goods) return
    const key = String(goods.id)
    const newCart = { ...this.data.cart }

    if (newCart[key]) {
      if (newCart[key].quantity >= goods.stock) {
        wx.showToast({ title: '库存不足', icon: 'none' })
        return
      }
      newCart[key].quantity += 1
    } else {
      newCart[key] = { ...goods, quantity: 1 }
    }
    this.syncCartState(newCart)
  },

  increaseQuantity(e) {
    const id = e.currentTarget.dataset.id + ''
    const newCart = { ...this.data.cart }
    if (!newCart[id]) return
    if (newCart[id].quantity >= newCart[id].stock) {
      wx.showToast({ title: '库存不足', icon: 'none' })
      return
    }
    newCart[id].quantity += 1
    this.syncCartState(newCart)
  },

  decreaseQuantity(e) {
    const id = e.currentTarget.dataset.id + ''
    const newCart = { ...this.data.cart }
    if (!newCart[id]) return
    if (newCart[id].quantity <= 1) {
      delete newCart[id]
    } else {
      newCart[id].quantity -= 1
    }
    this.syncCartState(newCart)
  },

  syncCartState(newCart) {
    let count = 0, total = 0
    Object.values(newCart).forEach(item => {
      count += item.quantity
      total += item.price * item.quantity
    })
    this.setData({
      cart: newCart,
      cartItems: Object.values(newCart),
      cartCount: count,
      totalPrice: parseFloat(total.toFixed(2))
    })
    try { wx.setStorageSync('orderCart', newCart) } catch (e) {}
  },

  restoreCart(cart) {
    let count = 0, total = 0
    Object.values(cart || {}).forEach(item => {
      count += item.quantity || 0
      total += (item.price || 0) * (item.quantity || 0)
    })
    this.setData({
      cart: cart || {},
      cartItems: Object.values(cart || {}),
      cartCount: count,
      totalPrice: parseFloat(total.toFixed(2))
    })
  },

  viewCart() {
    if (this.data.cartCount === 0) {
      wx.showToast({ title: '购物车为空', icon: 'none' })
      return
    }
    this.setData({ showCartModal: true })
  },

  hideCartModal() {
    this.setData({ showCartModal: false })
  },

  checkout() {
    if (this.data.cartCount === 0) {
      wx.showToast({ title: '购物车为空', icon: 'none' })
      return
    }
    if (!this.data.tableNumber) {
      wx.showToast({ title: '请选择桌台号码', icon: 'none' })
      return
    }
    wx.navigateTo({ url: '/pages/confirm-order/confirm-order' })
  },

  navigateToGoodsDetail(e) {
    const id = e.currentTarget.dataset.id
    wx.navigateTo({ url: `/pages/order-detail/order-detail?id=${id}` })
  },

  onReachBottom() {
    const cat = this.data.activeCategory
    if (this.data.lazyLoading || !this.data.hasMoreMap[cat]) return
    this.loadCategoryGoods(cat, true)
  },

  selectTable() {
    this.setData({ showTableModal: true })
  },

  hideTableModal() {
    this.setData({ showTableModal: false })
  },

  selectTableNumber(e) {
    const tableId = e.currentTarget.dataset.tableId
    this.setData({ tableNumber: tableId, showTableModal: false })
    wx.setStorageSync('tableNumber', tableId)
  },

  testScanCode() {
    // 生成包含桌台号码的二维码
    const tableNumber = '6'; // 默认桌台号码为6号
    const qrCodeUrl = `https://api.qrserver.com/v1/create-qr-code/?size=200x200&data=table:${tableNumber}`;
    
    this.setData({
      qrcodeUrl: qrCodeUrl,
      qrcodeTableNumber: tableNumber,
      showQrcodeModal: true
    });
  },

  hideQrcodeModal() {
    this.setData({ showQrcodeModal: false });
  },

  getTableList() {
    this.setData({
      tableList: [
        { id: '1', name: '桌台1' },{ id: '2', name: '桌台2' },
        { id: '3', name: '桌台3' },{ id: '4', name: '桌台4' },
        { id: '5', name: '桌台5' },{ id: '6', name: '桌台6' },
        { id: '7', name: '桌台7' },{ id: '8', name: '桌台8' }
      ]
    })
  },

  stopAll() {
    return false
  }
})