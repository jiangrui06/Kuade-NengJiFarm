const api = require('../../utils/api');

const FALLBACK_CATEGORIES = [
  { id: 'all', name: '全部商品', color: '#4CAF50', icon: '全' },
  { id: 'vegetables', name: '蔬菜', color: '#4CAF50', icon: '菜' },
  { id: 'fruits', name: '水果', color: '#FF9800', icon: '果' },
  { id: 'meat', name: '肉类', color: '#F44336', icon: '肉' },
  { id: 'grains', name: '粮油', color: '#9C27B0', icon: '粮' }
];

Page({
  data: {
    showCategory: false,
    showCategoryView: false,
    currentCategory: 'all',
    categories: FALLBACK_CATEGORIES,
    currentCategoryGoods: [],
    loading: true,
    loadingMore: false,
    page: 1,
    pageSize: 10,
    hasMore: true,
    goodsCache: {},
    searchKeyword: '',
    searchResults: [],
    showFilterDrawer: false,
    minPrice: '',
    maxPrice: '',
    cartCount: 0,
    cart: {}
  },

  onLoad() {
    this.getFarmGoodsData();
    this.updateCartCount();
  },

  onShow() {
    // 每次页面显示时更新购物车数据，确保与首页购物车同步
    this.updateCartCount();
  },

  getFarmGoodsData() {
    wx.showLoading({
      title: '加载中...',
      mask: true
    });

    this.setData({
      currentCategory: 'all',
      currentCategoryGoods: [],
      loading: true,
      loadingMore: false,
      page: 1,
      hasMore: true
    });

    api.request({
      url: '/api/farm-goods',
      method: 'GET',
      data: {
        category: 'all'
      }
    }).then((data) => {
      const items = Array.isArray(data.items) ? data.items : [];
      const firstPageGoods = this.sliceGoodsPage(items, 1);

      this.setData({
        categories: Array.isArray(data.categories) && data.categories.length ? data.categories : FALLBACK_CATEGORIES,
        currentCategory: data.category || 'all',
        currentCategoryGoods: firstPageGoods,
        goodsCache: {
          ...this.data.goodsCache,
          all: items
        },
        loading: false,
        page: 1,
        hasMore: items.length > this.data.pageSize
      });
    }).catch((err) => {
      this.setData({ loading: false });
      wx.showToast({
        title: err.message || '农场优选加载失败',
        icon: 'none'
      });
    }).finally(() => {
      wx.hideLoading();
    });
  },

  sliceGoodsPage(goodsList, page) {
    const end = page * this.data.pageSize;
    return goodsList.slice(0, end);
  },

  applyCategoryPage(categoryId, page) {
    const categoryGoods = this.data.goodsCache[categoryId] || [];
    const start = (page - 1) * this.data.pageSize;
    const end = page * this.data.pageSize;
    const newItems = categoryGoods.slice(start, end);

    this.setData({
      currentCategory: categoryId,
      currentCategoryGoods: page === 1
        ? newItems
        : [...this.data.currentCategoryGoods, ...newItems],
      page,
      hasMore: categoryGoods.length > end,
      loading: false,
      loadingMore: false
    });
  },

  fetchCategoryGoods(categoryId) {
    return api.request({
      url: '/api/farm-goods/category',
      method: 'GET',
      data: {
        category: categoryId
      }
    }).then((data) => Array.isArray(data.items) ? data.items : []);
  },

  loadCategoryGoods(categoryId, isLoadMore = false) {
    const nextPage = isLoadMore ? this.data.page + 1 : 1;
    const cachedGoods = this.data.goodsCache[categoryId];

    if (cachedGoods) {
      if (isLoadMore) {
        this.setData({ loadingMore: true });
      } else {
        this.setData({ loading: true });
      }
      this.applyCategoryPage(categoryId, nextPage);
      // 应用价格筛选
      this.applyPriceFilter();
      return;
    }

    if (isLoadMore) {
      this.setData({ loadingMore: true });
    } else {
      this.setData({
        loading: true,
        currentCategory: categoryId,
        currentCategoryGoods: [],
        page: 1,
        hasMore: true
      });
    }

    this.fetchCategoryGoods(categoryId).then((goodsList) => {
      this.setData({
        goodsCache: {
          ...this.data.goodsCache,
          [categoryId]: goodsList
        }
      });
      this.applyCategoryPage(categoryId, nextPage);
      // 应用价格筛选
      this.applyPriceFilter();
    }).catch((err) => {
      this.setData({ loading: false, loadingMore: false });
      wx.showToast({
        title: err.message || '分类商品加载失败',
        icon: 'none'
      });
    });
  },

  applyPriceFilter() {
    const { minPrice, maxPrice, currentCategoryGoods } = this.data;
    if (!minPrice && !maxPrice) return;

    let filteredGoods = [...currentCategoryGoods];

    if (minPrice) {
      filteredGoods = filteredGoods.filter(item => item.price >= parseFloat(minPrice));
    }
    if (maxPrice) {
      filteredGoods = filteredGoods.filter(item => item.price <= parseFloat(maxPrice));
    }

    this.setData({ currentCategoryGoods: filteredGoods });
  },

  search() {
    const keyword = this.data.searchKeyword.trim();
    if (keyword) {
      this.performSearch(keyword);
    } else {
      this.loadCategoryGoods(this.data.currentCategory);
    }
  },

  onSearchInput(e) {
    const keyword = e.detail.value;
    this.setData({ searchKeyword: keyword });

    if (keyword.trim()) {
      this.performSearch(keyword.trim());
    } else {
      this.loadCategoryGoods(this.data.currentCategory);
    }
  },

  performSearch(keyword) {
    wx.showLoading({ title: '搜索中...' });

    let allGoods = [];
    Object.values(this.data.goodsCache).forEach(list => {
      if (Array.isArray(list)) {
        allGoods = allGoods.concat(list);
      }
    });

    if (allGoods.length === 0) {
      allGoods = this.data.currentCategoryGoods || [];
    }

    const result = allGoods.filter(item => {
      const name = item.name || '';
      return name.includes(keyword);
    });

    this.setData({
      searchResults: result,
      currentCategoryGoods: result
    });

    wx.hideLoading();

    if (result.length === 0) {
      wx.showToast({
        title: '目前没找到您搜索的商品',
        icon: 'none'
      });
    }
  },

  showFilterDrawer() {
    this.setData({ showFilterDrawer: true });
    wx.setPageStyle({ style: { overflow: 'hidden' } });
  },

  hideFilterDrawer() {
    this.setData({ showFilterDrawer: false });
    wx.setPageStyle({ style: { overflow: 'auto' } });
  },

  onMinPriceInput(e) {
    this.setData({ minPrice: e.detail.value });
  },

  onMaxPriceInput(e) {
    this.setData({ maxPrice: e.detail.value });
  },

  resetFilter() {
    this.setData({ minPrice: '', maxPrice: '' });
  },

  applyFilter() {
    this.hideFilterDrawer();
    // 应用价格筛选
    this.applyPriceFilter();
  },

  increaseQuantity(e) {
    const id = e.currentTarget.dataset.id;
    const goods = this.data.currentCategoryGoods.find(item => item.id === id);
    if (!goods) return;

    const newCart = { ...this.data.cart };
    const key = String(id);

    if (newCart[key]) {
      if (newCart[key].quantity >= goods.stock) {
        wx.showToast({ title: '库存不足', icon: 'none' });
        return;
      }
      newCart[key].quantity += 1;
    } else {
      newCart[key] = { 
        ...goods, 
        quantity: 1, 
        goodsId: goods.id // 确保goodsId存在
      };
    }

    this.syncCartState(newCart);
  },

  decreaseQuantity(e) {
    const id = e.currentTarget.dataset.id;
    const newCart = { ...this.data.cart };
    const key = String(id);

    if (!newCart[key]) return;

    if (newCart[key].quantity <= 1) {
      delete newCart[key];
    } else {
      newCart[key].quantity -= 1;
    }

    this.syncCartState(newCart);
  },

  syncCartState(newCart) {
    let count = 0;
    const cartArray = Object.values(newCart).map(item => ({
      ...item,
      count: item.quantity, // 统一使用count字段
      goodsId: item.id // 添加goodsId字段，确保API调用正确
    }));
    
    cartArray.forEach(item => {
      count += item.count;
    });

    this.setData({ cart: newCart, cartCount: count });

    try {
      wx.setStorageSync('cartList', cartArray);
    } catch (e) {}
  },

  updateCartCount() {
    const cartList = wx.getStorageSync('cartList') || [];
    let totalCount = 0;
    const cart = {};
    cartList.forEach(item => {
      // 处理来自首页购物车的数据，确保字段一致
      const quantity = item.quantity || item.count || 0;
      totalCount += quantity;
      const itemId = item.id || item.goodsId;
      if (itemId) {
        cart[String(itemId)] = {
          ...item,
          id: itemId,
          quantity: quantity, // 统一使用quantity字段
          goodsId: item.goodsId || itemId // 确保goodsId存在
        };
      }
    });
    this.setData({ cart, cartCount: totalCount });
  },

  goToCart() {
    wx.switchTab({ url: '/pages/cart/cart' });
  },

  stopPropagation() {
    return false;
  },

  toggleCategory() {
    const nextState = !this.data.showCategoryView;
    this.setData({
      showCategory: nextState,
      showCategoryView: nextState
    });
  },

  selectCategory(e) {
    const categoryId = e.currentTarget.dataset.id;
    this.setData({
      showCategory: false,
      showCategoryView: true
    });
    this.loadCategoryGoods(categoryId);
  },

  getCurrentCategoryName() {
    const category = this.data.categories.find(item => item.id === this.data.currentCategory);
    return category ? category.name : '商品分类';
  },

  viewMore() {
    this.setData({
      showCategory: false,
      showCategoryView: true
    });
    this.loadCategoryGoods(this.data.currentCategory || 'all');
  },

  viewGoodsDetail(e) {
    const goodsId = e.currentTarget.dataset.id;
    wx.navigateTo({ url: '/subpkg/goods-detail/goods-detail?id=' + goodsId });
  },

  onReachBottom() {
    if (!this.data.loadingMore && this.data.hasMore) {
      this.loadCategoryGoods(this.data.currentCategory, true);
    }
  }
});
