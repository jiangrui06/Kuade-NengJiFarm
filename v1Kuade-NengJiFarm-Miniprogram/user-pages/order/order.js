const api = require('../../utils/api');

let globalKeyCounter = 0;

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
    let pendingTableId = null;
    let pendingTableFullName = null;
    if (options.tableId && options.secret) {
      pendingTableId = options.tableId;
      pendingTableFullName = options.tableId;
    } else {
      const cart = wx.getStorageSync('orderCart') || {};
      this.restoreCart(cart);
      const storedTableNumber = wx.getStorageSync('tableNumber');
      if (storedTableNumber) {
        this.setData({ tableNumber: storedTableNumber });
      }
    }

    this.getTableList(pendingTableId, pendingTableFullName);
    setTimeout(() => {
      this.getOrderData();
    }, 500);
  },

  onShow() {
    try {
      const cart = wx.getStorageSync('orderCart') || {};
      this.restoreCart(cart);
      const stored = wx.getStorageSync('tableNumber');
      if (stored) this.setData({ tableNumber: stored });
      this.syncFromCart();
      // 返回页面时静默刷新所有数据（分类、菜品、库存）
      this.silentRefreshAll();
    } catch (e) {}
  },

  syncFromCart() {
    try {
      const cart = wx.getStorageSync('orderCart') || {};
      this.restoreCart(cart);
    } catch (e) {
    }
  },

  // 核心：替换为对方的 api 接口，其他完全用你的逻辑
  getOrderData() {
    wx.showLoading({ title: '加载中...' })
    // 使用对方的获取分类接口
    api.goods.getCategories({ type: 'food' })
      .then(data => {
        const categories = [
          { id: 'all', name: '全部菜品' },
          ...(data || []).map(cat => ({
            id: String(cat.id),
            name: cat.name
          }))
        ];
        const currentCategory = 'all';
        this.setData({
          activeCategory: currentCategory,
          categories,
          pageMap: { [currentCategory]: 1 },
          hasMoreMap: { [currentCategory]: true },
          loading: false
        });

        this.updateMergedGoodsList();
        this.loadAllCategories();
      })
      .catch(err => {
        this.setData({ loading: false });
        wx.showToast({ title: '加载失败', icon: 'none' });
      })
      .finally(() => wx.hideLoading());
  },

  // 图片处理（沿用对方的工具方法）
  processImageUrl(imageUrl) {
    const utils = require('../../utils/utils');
    return utils.media.processUrl(imageUrl);
  },

  addImageUrlsToGoods(goods) {
    return goods.map(item => ({
      ...item,
      image: this.processImageUrl(item.image),
      price: (item.price || '').toString().replace(/[¥￥]/g, ''),
      stock: item.stock || 0,
      sold: item.sold || 0
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
    const query = wx.createSelectorQuery();
    
    this.data.categories.forEach(item => {
      query.select(`#category-${item.id}`).boundingClientRect()
    });
    query.select('.goods-list').boundingClientRect();
  
    query.exec(res => {
      const listRect = res[res.length - 1];
      let curId = this.data.activeCategory;
  
      // 👇 核心终极逻辑：只要上一个分类完全跑出去，立刻切下一个
      for (let i = 0; i < this.data.categories.length; i++) {
        const rect = res[i];
        if (!rect) continue;
        
        // 只要标签的底部 低于 容器顶部（标签已经进入屏幕）
        if (rect.bottom < listRect.top + 100) {
          curId = this.data.categories[i].id;
        }
      }
  
      if (curId !== this.data.activeCategory) {
        this.setData({ activeCategory: curId });
      }
    })
  },

  // 加载菜品：使用对方的 api.goods.getList
  loadCategoryGoods(category, isLoadMore) {
    if (isLoadMore && this.data.lazyLoading) return;
    const nextPage = isLoadMore ? (this.data.pageMap[category] || 0) + 1 : 1;
    this.setData({ lazyLoading: isLoadMore });

    let reqData = { type: 'food', pageSize: 100 };
    if (category !== 'all') {
      reqData.categoryId = category;
    }

    api.goods.getList(reqData)
      .then(data => {
        let newGoods = this.addImageUrlsToGoods(data || []);
        const old = this.data.goodsList[category] || [];
        this.setData({
          [`goodsList.${category}`]: isLoadMore ? old.concat(newGoods) : newGoods,
          [`pageMap.${category}`]: nextPage,
          [`hasMoreMap.${category}`]: newGoods.length >= this.data.pageSize,
          lazyLoading: false
        });
        this.updateMergedGoodsList();
      }).catch(() => {
        this.setData({ lazyLoading: false });
        wx.showToast({ title: '加载失败', icon: 'none' });
      });
  },

  loadAllCategories() {
    this.data.categories.forEach(c => {
      if (!this.data.goodsList[c.id]) this.loadCategoryGoods(c.id, false);
    });
  },

  // 静默刷新全部数据（分类 + 菜品 + 库存），onShow 时调用
  silentRefreshAll() {
    if (this._refreshingAll) return;
    this._refreshingAll = true;
    api.goods.getCategories({ type: 'food' })
      .then(data => {
        const newCategories = [
          { id: 'all', name: '全部菜品' },
          ...(data || []).map(cat => ({
            id: String(cat.id),
            name: cat.name
          }))
        ];
        this.setData({ categories: newCategories });
        // 刷新所有分类的商品库存
        this.refreshStock();
      })
      .catch(() => {})
      .finally(() => { this._refreshingAll = false; });
  },

  // 后台刷新所有分类的菜品库存（不显示loading）
  refreshStock() {
    this.data.categories.forEach(c => {
      let reqData = { type: 'food', pageSize: 100 };
      if (c.id !== 'all') {
        reqData.categoryId = c.id;
      }
      api.goods.getList(reqData)
        .then(data => {
          let refreshedGoods = this.addImageUrlsToGoods(data || []);
          this.setData({
            [`goodsList.${c.id}`]: refreshedGoods
          });
          this.updateMergedGoodsList();
        })
        .catch(() => {});
    });
  },

  updateMergedGoodsList() {
    const { categories, goodsList } = this.data;
    const merged = [];
    let index = 0;
    categories.forEach(c => {
      merged.push({ type: 'category', id: c.id, name: c.name, uniqueKey: `cat-${c.id}-${index}` });
      index++;
      (goodsList[c.id] || []).forEach((g) => {
        merged.push({ ...g, uniqueKey: `item-${index}` });
        index++;
      });
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
      newCart[key].count = newCart[key].quantity;
    } else {
      if (goods.stock <= 0) return wx.showToast({ title: '库存不足', icon: 'none' });
      newCart[key] = { ...goods, quantity: 1, count: 1 };
    }
    this.syncCartState(newCart);
  },

  increaseQuantity(e) {
    const id = e.currentTarget.dataset.id + '';
    const newCart = { ...this.data.cart };
    if (!newCart[id]) return;
    if (newCart[id].quantity >= newCart[id].stock) return wx.showToast({ title: '库存不足', icon: 'none' });
    newCart[id].quantity++;
    newCart[id].count = newCart[id].quantity;
    this.syncCartState(newCart);
  },

  decreaseQuantity(e) {
    const id = e.currentTarget.dataset.id + '';
    const newCart = { ...this.data.cart };
    if (!newCart[id]) return;
    if (newCart[id].quantity <= 1) delete newCart[id];
    else {
      newCart[id].quantity--;
      newCart[id].count = newCart[id].quantity;
    }
    this.syncCartState(newCart);
  },

  onQuantityInput(e) {
    const id = e.currentTarget.dataset.id + '';
    const val = Math.max(0, parseInt(e.detail.value) || 0);
    const goods = this.data.mergedGoodsList.find(i => i.id == id);
    if (!goods) return;
    const newCart = { ...this.data.cart };
    const finalQty = Math.min(val, goods.stock);
    if (finalQty === 0) delete newCart[id];
    else {
      newCart[id] = { ...newCart[id], quantity: finalQty, count: finalQty };
    }
    this.syncCartState(newCart);
  },

  syncCartState(newCart) {
    let count = 0, total = 0;
    const cartWithChecked = {};
    for (const key in newCart) {
      const item = newCart[key];
      const itemQuantity = item.quantity || item.count || 0;
      cartWithChecked[key] = {
        ...item,
        id: String(item.id || key),
        quantity: itemQuantity,
        count: itemQuantity,
        checked: true
      };
      count += itemQuantity;
      total += (item.price || 0) * itemQuantity;
    }
    this.setData({ cart: cartWithChecked, cartItems: Object.values(cartWithChecked), cartCount: count, totalPrice: +total.toFixed(2) });
    wx.setStorageSync('orderCart', cartWithChecked);
  },

  restoreCart(cart) {
    let count = 0, total = 0;
    const restoredCart = {};
    Object.entries(cart || {}).forEach(([key, item]) => {
      const itemQuantity = item.quantity || item.count || 0;
      if (itemQuantity <= 0) return;
      const itemId = String(item.id || key);
      restoredCart[itemId] = {
        ...item,
        id: itemId,
        image: this.processImageUrl(item.image || ''),
        quantity: itemQuantity,
        count: itemQuantity,
        checked: item.checked !== false
      };
      count += itemQuantity;
      total += (item.price || 0) * itemQuantity;
    });
    this.setData({ cart: restoredCart, cartItems: Object.values(restoredCart), cartCount: count, totalPrice: +total.toFixed(2) });
  },

  viewCart() {
    if (this.data.cartCount === 0) return wx.showToast({ title: '购物车为空', icon: 'none' });
    this.setData({ showCartModal: true });
  },
  hideCartModal() { this.setData({ showCartModal: false }); },

  checkout() {
    if (this.data.cartCount === 0) return wx.showToast({ title: '购物车为空', icon: 'none' });
    if (!this.data.tableNumber) return wx.showToast({ title: '请选择桌台', icon: 'none' });

    // 未登录跳转到登录页面
    const token = wx.getStorageSync('token');
    if (!token) {
      wx.showToast({ title: '请先登录', icon: 'none' });
      setTimeout(() => {
        wx.navigateTo({ url: '/pages/login/login' });
      }, 500);
      return;
    }

    // 自动勾选所有点餐商品，确保结算时能看到所有商品
    const updatedCart = {};
    for (const key in this.data.cart) {
      updatedCart[key] = {
        ...this.data.cart[key],
        checked: true
      };
    }

    // 更新本地数据和存储
    this.setData({ cart: updatedCart, cartItems: Object.values(updatedCart) });
    wx.setStorageSync('orderCart', updatedCart);

    wx.navigateTo({
      url: `/user-pages/confirm-order/confirm-order?type=food&tableNumber=${this.data.tableNumber}`
    });
  },

  navigateToGoodsDetail(e) {
    const id = e.currentTarget.dataset.id;
    wx.navigateTo({
      url: `/user-pages/order-foods-detail/order-foods-detail?id=${id}`
    });
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
    wx.showToast({ title: `已选择${id}`, icon: 'success' });
  },

  testScanCode() {
    wx.scanCode({
      success: res => {
        const q = res.result.match(/query=([^&]+)/)?.[1];
        if (!q) return wx.showToast({ title: '无效二维码', icon: 'none' });
        const d = Object.fromEntries(q.split('&').map(kv => kv.split('=').map(decodeURIComponent)));
        if (d.tableId) {
          const tableId = d.tableId;
          const tableFullName = d.tableId;
          // 通过详情接口校验桌台是否可用（停用或不存在返回 404）
          api.table.getDetail(tableFullName).then(() => {
            this.setData({ tableNumber: tableId });
            wx.setStorageSync('tableNumber', tableId);
            wx.showToast({ title: `已选择${tableId}`, icon: 'success' });
          }).catch(() => {
            wx.showToast({ title: '该桌台已停用', icon: 'none' });
          });
        }
      }
    });
  },

  getTableList(pendingTableId, pendingTableFullName) {
    api.table.getList({}, { skipAuthCheck: true })
      .then(data => {
        const seen = new Set();
        const list = (data || []).reduce((acc, t) => {
          if (!seen.has(t.name)) {
            seen.add(t.name);
            acc.push({ id: t.name, name: t.name });
          }
          return acc;
        }, []);
        list.sort((a, b) => {
          const na = parseInt(a.name.replace(/[^0-9]/g, ''), 10) || 0;
          const nb = parseInt(b.name.replace(/[^0-9]/g, ''), 10) || 0;
          return na - nb;
        });
        this.setData({ tableList: list });

        // 通过详情接口校验桌台是否可用（停用或不存在返回 404）
        if (pendingTableId) {
          api.table.getDetail(pendingTableFullName || pendingTableId).then(() => {
            this.setData({ tableNumber: pendingTableId });
            wx.setStorageSync('tableNumber', pendingTableId);
            setTimeout(() => wx.showToast({ title: `已选择${pendingTableId}`, icon: 'success' }), 500);
          }).catch(() => {
            wx.showToast({ title: '该桌台已停用', icon: 'none' });
          });
        }
      })
      .catch(() => {
        this.setData({ tableList: [] });
        if (pendingTableId) {
          this.setData({ tableNumber: pendingTableId });
          wx.setStorageSync('tableNumber', pendingTableId);
        }
      });
  },

  stopPropagation() { return false },
  navigateToService() {
    wx.showModal({ title: '客服', content: '电话：15876534944\n微信：njjtnc15876534944', showCancel: false });
  }
});