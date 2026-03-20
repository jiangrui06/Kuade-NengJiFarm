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

    // 你原来的桌台功能 👇
    tableNumber: null,
    showTableModal: false,
    tableList: []
  },

  onLoad() {
    // 恢复购物车
    const cart = wx.getStorageSync('orderCart') || {};
    this.restoreCart(cart);
    // 获取桌台
    this.getTableList();
    // 获取商品数据
    this.getOrderData();
  },

  // ======================
  // 后端接口获取数据（已修复）
  // ======================
  getOrderData() {
    wx.showLoading({ title: '加载中...' })

    api.request({
      url: '/api/DemoApi/orders',
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
        loading: false
      })
    }).catch(err => {
      console.error("加载失败:", err)
      this.setData({ loading: false })
      wx.showToast({ title: '加载失败', icon: 'none' })
    }).finally(() => {
      wx.hideLoading()
    })
  },

  // ======================
  // 切换分类
  // ======================
  switchCategory(e) {
    const category = e.currentTarget.dataset.category
    if (category === this.data.activeCategory) return

    this.setData({ activeCategory: category })

    if (this.data.goodsList[category]) {
      return
    }

    this.loadCategoryGoods(category, false)
  },

  loadCategoryGoods(category, isLoadMore) {
    if (isLoadMore && this.data.lazyLoading) return

    const nextPage = isLoadMore ? (this.data.pageMap[category] || 0) + 1 : 1

    this.setData({
      loading: !isLoadMore,
      lazyLoading: isLoadMore
    })

    api.request({
      url: '/api/DemoApi/orders',
      method: 'GET',
      data: {
        categoryId: category,
        page: nextPage,
        pageSize: this.data.pageSize
      }
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
  },

  // ======================
  // 购物车逻辑（完全保留你原来的）
  // ======================
  addToCart(e) {
    const { category, index } = e.currentTarget.dataset
    const goods = this.data.goodsList[category][index]
    if (!goods) return

    const key = String(goods.id)
    const newCart = JSON.parse(JSON.stringify(this.data.cart))

    if (newCart[key]) {
      newCart[key].quantity += 1
    } else {
      newCart[key] = { ...goods, quantity: 1 }
    }

    this.syncCartState(newCart)
  },

  increaseQuantity(e) {
    const id = e.currentTarget.dataset.id + ''
    const newCart = JSON.parse(JSON.stringify(this.data.cart))
    if (!newCart[id]) return

    newCart[id].quantity += 1
    this.syncCartState(newCart)
  },

  decreaseQuantity(e) {
    const id = e.currentTarget.dataset.id + ''
    const newCart = JSON.parse(JSON.stringify(this.data.cart))
    if (!newCart[id]) return

    if (newCart[id].quantity <= 1) {
      delete newCart[id]
    } else {
      newCart[id].quantity -= 1
    }

    this.syncCartState(newCart)
  },

  syncCartState(newCart) {
    let count = 0
    let total = 0
    for (let k in newCart) {
      count += newCart[k].quantity
      total += newCart[k].price * newCart[k].quantity
    }

    this.setData({
      cart: newCart,
      cartItems: Object.values(newCart),
      cartCount: count,
      totalPrice: total
    })

    wx.setStorageSync('orderCart', newCart)
  },

  restoreCart(cart) {
    let count = 0
    let total = 0
    for (let k in cart) {
      count += cart[k].quantity
      total += cart[k].price * cart[k].quantity
    }
    this.setData({
      cart,
      cartItems: Object.values(cart),
      cartCount: count,
      totalPrice: total
    })
  },

  viewCart() {
    if (this.data.cartCount === 0) {
      wx.showToast({ title: '购物车为空', icon: 'none' })
      return
    }
    this.setData({
      cartItems: Object.values(this.data.cart),
      showCartModal: true
    })
  },

  hideCartModal() {
    this.setData({ showCartModal: false })
  },

  checkout() {
    if (this.data.cartCount === 0) {
      wx.showToast({ title: '购物车为空', icon: 'none' })
      return
    }
    wx.navigateTo({ url: '/pages/confirm-order/confirm-order' })
  },

  navigateToGoodsDetail(e) {
    const id = e.currentTarget.dataset.id
    wx.navigateTo({ url: `/pages/goods-detail/goods-detail?id=${id}` })
  },

  onReachBottom() {
    const cat = this.data.activeCategory
    if (this.data.lazyLoading || !this.data.hasMoreMap[cat]) return
    this.loadCategoryGoods(cat, true)
  },

  // ======================
  // 你原来的 桌台 + 扫码 功能（完全保留）
  // ======================
  selectTable() {
    this.setData({ showTableModal: true })
  },

  hideTableModal() {
    this.setData({ showTableModal: false })
  },

  selectTableNumber(e) {
    const tableId = e.currentTarget.dataset.tableId
    this.setData({
      tableNumber: tableId,
      showTableModal: false
    })
  },

  testScanCode() {
    this.setData({ tableNumber: '5' })
    wx.showToast({ title: '扫码成功', icon: 'success' })
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
  }
})