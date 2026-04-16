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
    cart: {},
    filterIconUrl: ''
  },

  onLoad() {
    this.getFarmGoodsData();
    this.updateCartCount();
    this.getFilterIcon();
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
      let items = Array.isArray(data.items) ? data.items : [];
      
      // 为餐品添加图片URL
      items = this.addImageUrlsToGoods(items);
      
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

  // 为餐品添加图片URL
  addImageUrlsToGoods(goods) {
    // 特定餐品的图片映射
    const specificGoodsImages = {
      '有机生菜': 'Farm_32.jpg',
      '黄金甜玉米': 'Farm_28.jpg',
      '农家番茄': 'Farm_53.jpg',
      '散养土鸡蛋': 'Farm_34.jpg',
      '黑猪梅花肉': 'Farm_48.jpg',
      '农家花生油': 'Farm_27.jpg',
      '农家橘子': 'Farm_14.jpg'
    };
    
    // 为每个餐品分配图片URL
    return goods.map((item) => {
      let imageUrl = '';
      
      // 检查是否为特定餐品
      if (item.name && specificGoodsImages[item.name]) {
        imageUrl = `http://192.168.203.56/api/file/image/${specificGoodsImages[item.name]}`;
      } else {
        // 对于其他餐品，使用默认图片
        imageUrl = `http://192.168.203.56/api/file/image/farm_0000000000009.jpg`;
      }
      
      return {
        ...item,
        image: imageUrl,
        price: item.price ? item.price.toString().replace(/[¥￥]/g, '') : item.price,
        originalPrice: item.originalPrice ? item.originalPrice.toString().replace(/[¥￥]/g, '') : item.originalPrice
      };
    });
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
    }).then((data) => {
      let items = Array.isArray(data.items) ? data.items : [];
      // 为餐品添加图片URL
      items = this.addImageUrlsToGoods(items);
      return items;
    });
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
      // 应用搜索
      const keyword = this.data.searchKeyword.trim();
      if (keyword) {
        this.performSearch(keyword);
      }
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
      // 应用搜索
      const keyword = this.data.searchKeyword.trim();
      if (keyword) {
        this.performSearch(keyword);
      }
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

    // 检查最低价格是否高于最高价格
    if (minPrice && maxPrice) {
      const min = parseFloat(minPrice);
      const max = parseFloat(maxPrice);
      
      // 确保输入是有效的数字
      if (!isNaN(min) && !isNaN(max)) {
        if (min > max) {
          // 如果最低价格高于最高价格，不进行筛选
          return;
        }
      }
    }

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

    // 只在当前分类下搜索
    const currentCategory = this.data.currentCategory;
    let currentGoods = this.data.goodsCache[currentCategory] || [];

    if (currentGoods.length === 0) {
      currentGoods = this.data.currentCategoryGoods || [];
    }

    const result = currentGoods.filter(item => {
      const name = item.name || '';
      return name.includes(keyword);
    });

    // 去重，确保每个商品只显示一次
    const uniqueResult = [];
    const seenIds = new Set();
    result.forEach(item => {
      if (item.id && !seenIds.has(item.id)) {
        seenIds.add(item.id);
        uniqueResult.push(item);
      }
    });

    this.setData({
      searchResults: uniqueResult,
      currentCategoryGoods: uniqueResult
    });

    wx.hideLoading();

    if (uniqueResult.length === 0) {
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
    const { minPrice, maxPrice } = this.data;
    
    // 检查最低价格是否高于最高价格
    if (minPrice && maxPrice) {
      const min = parseFloat(minPrice);
      const max = parseFloat(maxPrice);
      
      // 确保输入是有效的数字
      if (!isNaN(min) && !isNaN(max)) {
        if (min > max) {
          wx.showToast({ title: '最低价格不能高于最高价格', icon: 'none' });
          return;
        }
      }
    }
    
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
  },

  // 获取筛选图标
  getFilterIcon() {
    // 使用本地图标路径，确保在真机调试下也能正常显示
    const localIconPath = '/images/PriceFilter.png';
    this.setData({ filterIconUrl: localIconPath });
    console.log('使用本地筛选图标:', localIconPath);
  }
});
