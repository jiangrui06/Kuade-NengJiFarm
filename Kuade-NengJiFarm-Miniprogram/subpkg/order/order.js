const api = require('../../utils/api');

Page({
  data: {
    activeCategory: 'vegetables',
    categories: [],
    goodsList: {},
    mergedGoodsList: [],
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

    scrollIntoViewId: '',
    isManualScroll: false
  },

  onLoad(options) {
    if (options.tableId && options.secret) {
      const tableNumber = options.tableId;
      this.setData({ tableNumber });
      wx.setStorageSync('tableNumber', tableNumber);
      setTimeout(() => {
        wx.showToast({ title: `已绑定桌台 ${tableNumber}`, icon: 'success' });
      }, 500);
    } else {
      const cart = wx.getStorageSync('orderCart') || {};
      this.restoreCart(cart);
      const storedTableNumber = wx.getStorageSync('tableNumber');
      if (storedTableNumber) {
        this.setData({ tableNumber: storedTableNumber });
      }
    }

    this.getTableList();
    setTimeout(() => {
      this.getOrderData();
    }, 500);
  },

  onShow() {
    try {
      const cart = wx.getStorageSync('orderCart') || {};
      this.restoreCart(cart);
      const tableNumber = wx.getStorageSync('tableNumber');
      if (tableNumber) this.setData({ tableNumber });
      this.syncFromCart();
    } catch (e) {}
  },

  syncFromCart() {
    try {
      const cartList = wx.getStorageSync('cartList') || [];
      const foodItems = cartList.filter(i => i.type === 'food');
      if (foodItems.length === 0) return;
      const newCart = { ...this.data.cart };
      foodItems.forEach(item => {
        const key = String(item.id);
        if (item.count > 0) newCart[key] = { ...item, quantity: item.count, price: +item.price };
        else delete newCart[key];
      });
      this.syncCartState(newCart);
    } catch (e) {}
  },

  getOrderData() {
    wx.showLoading({ title: '加载中...' })
    api.request({
      url: '/api/order',
      data: { categoryId: this.data.activeCategory, page: 1, pageSize: this.data.pageSize }
    }).then(data => {
      const categories = data.categories || [];
      const currentCategory = data.currentCategory || 'vegetables';
      const goods = this.addImageUrlsToGoods(data.goodsList || [], currentCategory);

      this.setData({
        activeCategory: currentCategory,
        categories,
        goodsList: { [currentCategory]: goods },
        pageMap: { [currentCategory]: 1 },
        hasMoreMap: { [currentCategory]: !!data.hasMore },
        loading: false
      });

      this.updateMergedGoodsList();
      this.loadAllCategories();
    }).catch(() => {
      this.setData({ loading: false });
      wx.showToast({ title: '加载失败', icon: 'none' });
    }).finally(() => wx.hideLoading());
  },

  addImageUrlsToGoods(goods, category) {
    return goods.map(item => ({
      ...item,
      price: (item.price || '').toString().replace(/[¥￥]/g, ''),
      category
    }));
  },

  switchCategory(e) {
    const category = e.currentTarget.dataset.category;
    this.setData({ isManualScroll: true, activeCategory: category });
    this.updateMergedGoodsList();
    this.setData({ scrollIntoViewId: `category-${category}` });
    setTimeout(() => {
      this.setData({ isManualScroll: false, scrollIntoViewId: '' });
    }, 500);
  },

  onScroll(e) {
    if (this.data.isManualScroll) return;
    const scrollTop = e.detail.scrollTop;
    const query = wx.createSelectorQuery();
    this.data.categories.forEach(item => {
      query.select(`#category-${item.id}`).boundingClientRect()
    });
    query.select('.goods-list').boundingClientRect();
    query.exec(res => {
      const listRect = res[res.length - 1];
      let curId = '';
      for (let i = 0; i < this.data.categories.length; i++) {
        const rect = res[i];
        if (rect && rect.top <= listRect.top + 10) {
          curId = this.data.categories[i].id;
        }
      }
      if (curId && curId !== this.data.activeCategory) {
        this.setData({ activeCategory: curId });
      }
    })
  },

  loadCategoryGoods(category, isLoadMore) {
    if (isLoadMore && this.data.lazyLoading) return;
    const nextPage = isLoadMore ? (this.data.pageMap[category] || 0) + 1 : 1;
    this.setData({ lazyLoading: isLoadMore });

    api.request({
      url: '/api/order',
      data: { categoryId: category, page: nextPage, pageSize: this.data.pageSize }
    }).then(data => {
      const newGoods = this.addImageUrlsToGoods(data.goodsList || [], category);
      const old = this.data.goodsList[category] || [];
      this.setData({
        [`goodsList.${category}`]: old.concat(newGoods),
        [`pageMap.${category}`]: nextPage,
        [`hasMoreMap.${category}`]: !!data.hasMore,
        lazyLoading: false
      });
      this.updateMergedGoodsList();
    }).catch(() => this.setData({ lazyLoading: false }));
  },

  loadAllCategories() {
    this.data.categories.forEach(c => {
      if (!this.data.goodsList[c.id]) this.loadCategoryGoods(c.id, false);
    });
  },

  updateMergedGoodsList() {
    const { categories, goodsList } = this.data;
    const merged = [];
    categories.forEach(c => {
      merged.push({ type: 'category', id: c.id, name: c.name, uniqueKey: `cat-${c.id}` });
      (goodsList[c.id] || []).forEach(g => merged.push({ ...g, uniqueKey: `g-${g.id}` }));
    });
    this.setData({ mergedGoodsList: merged });
  },

  addToCart(e) {
    const id = e.currentTarget.dataset.id;
    const goods = this.data.mergedGoodsList.find(i => i.id == id);
    if (!goods) return;
    const newCart = { ...this.data.cart };
    const key = String(id);
    if (newCart[key]) {
      if (newCart[key].quantity >= goods.stock) return wx.showToast({ title: '库存不足', icon: 'none' });
      newCart[key].quantity++;
    } else newCart[key] = { ...goods, quantity: 1 };
    this.syncCartState(newCart);
  },

  increaseQuantity(e) {
    const id = e.currentTarget.dataset.id + '';
    const newCart = { ...this.data.cart };
    if (!newCart[id]) return;
    if (newCart[id].quantity >= newCart[id].stock) return wx.showToast({ title: '库存不足', icon: 'none' });
    newCart[id].quantity++;
    this.syncCartState(newCart);
  },

  decreaseQuantity(e) {
    const id = e.currentTarget.dataset.id + '';
    const newCart = { ...this.data.cart };
    if (!newCart[id]) return;
    if (newCart[id].quantity <= 1) delete newCart[id];
    else newCart[id].quantity--;
    this.syncCartState(newCart);
  },

  onQuantityInput(e) {
    const id = e.currentTarget.dataset.id + '';
    const val = Math.max(0, parseInt(e.detail.value) || 0);
    const goods = this.data.mergedGoodsList.find(i => i.id == id);
    if (!goods) return;
    const newCart = { ...this.data.cart };
    if (val === 0) delete newCart[id];
    else newCart[id] = { ...newCart[id], quantity: Math.min(val, goods.stock) };
    this.syncCartState(newCart);
  },

  syncCartState(newCart) {
    let count = 0, total = 0;
    Object.values(newCart).forEach(i => { count += i.quantity; total += i.price * i.quantity });
    this.setData({ cart: newCart, cartItems: Object.values(newCart), cartCount: count, totalPrice: +total.toFixed(2) });
    wx.setStorageSync('orderCart', newCart);
  },

  restoreCart(cart) {
    let count = 0, total = 0;
    Object.values(cart || {}).forEach(i => { count += i.quantity || i.count || 0; total += (i.price || 0) * (i.quantity || i.count || 0) });
    this.setData({ cart: cart || {}, cartItems: Object.values(cart || {}), cartCount: count, totalPrice: +total.toFixed(2) });
  },

  viewCart() {
    if (this.data.cartCount === 0) return wx.showToast({ title: '购物车为空', icon: 'none' });
    this.setData({ showCartModal: true });
  },
  hideCartModal() { this.setData({ showCartModal: false }); },

  checkout() {
    if (this.data.cartCount === 0) return wx.showToast({ title: '购物车为空', icon: 'none' });
    if (!this.data.tableNumber) return wx.showToast({ title: '请选择桌台', icon: 'none' });
    wx.navigateTo({ url: '/subpkg/confirm-order/confirm-order' });
  },

  navigateToGoodsDetail(e) {
    const id = e.currentTarget.dataset.id;
    const goods = this.data.mergedGoodsList.find(i => i.id == id);
    if (!goods) return;
    const p = encodeURIComponent(JSON.stringify({ id: goods.id, sold: goods.sold, stock: goods.stock }));
    wx.navigateTo({ url: `/subpkg/order-foods-detail/order-foods-detail?params=${p}` });
  },

  onReachBottom() {
    const cat = this.data.activeCategory;
    if (!this.data.hasMoreMap[cat] || this.data.lazyLoading) return;
    this.loadCategoryGoods(cat, true);
  },

  selectTable() { this.setData({ showTableModal: true }); },
  hideTableModal() { this.setData({ showTableModal: false }); },
  selectTableNumber(e) {
    const id = e.currentTarget.dataset.tableId;
    this.setData({ tableNumber: id, showTableModal: false });
    wx.setStorageSync('tableNumber', id);
  },

  testScanCode() {
    wx.scanCode({
      success: res => {
        const q = res.result.match(/query=([^&]+)/)?.[1];
        if (!q) return wx.showToast({ title: '无效二维码', icon: 'none' });
        const d = Object.fromEntries(q.split('&').map(kv => kv.split('=').map(decodeURIComponent)));
        if (d.tableId) {
          this.setData({ tableNumber: d.tableId });
          wx.setStorageSync('tableNumber', d.tableId);
          wx.showToast({ title: `桌台 ${d.tableId} 绑定成功`, icon: 'success' });
        }
      }
    });
  },

  getTableList() {
    this.setData({ tableList: [1, 2, 3, 4, 5, 6, 7, 8].map(i => ({ id: String(i), name: `桌台${i}` })) });
  },

  stopPropagation() { return false },
  navigateToService() {
    wx.showModal({ title: '客服', content: '电话：15876534944\n微信：njjtnc15876534944', showCancel: false });
  }
});