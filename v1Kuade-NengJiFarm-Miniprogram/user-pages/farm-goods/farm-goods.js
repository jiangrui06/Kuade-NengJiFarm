const api = require('../../utils/api');



Page({
  data: {
    showCategory: false,
    showCategoryView: false,
    currentCategory: 'all',
    categories: [],
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
    filterIconUrl: '',
    // 认购一亩田
    acreList: [],
    acreLoading: true,
    showAcreSection: false  // 是否聚焦展示认购区域（切换 Tab 用）
  },

  onLoad() {
    this.getFarmGoodsData();
    this.loadAcreData();
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

      // 处理分类数据，确保至少有一个"全部商品"分类
      let categories = Array.isArray(data.categories) && data.categories.length ? data.categories : [];
      
      // 检查是否已有"全部商品"分类
      const hasAllCategory = categories.some(cat => cat.id === 'all');
      if (!hasAllCategory) {
        // 添加"全部商品"分类作为第一个分类
        categories.unshift({ id: 'all', name: '全部商品', color: '#4CAF50', icon: '全' });
      }
      // 末尾追加「认购专区」Tab（若尚未存在）
      const hasAcreTab = categories.some(cat => cat.id === 'acre');
      if (!hasAcreTab) {
        categories.push({ id: 'acre', name: '认购专区', color: '#8BC34A', icon: '亩' });
      }

      this.setData({
        categories: categories,
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
        imageUrl = `http://192.168.101.47/api/file/image/${specificGoodsImages[item.name]}`;
      } else {
        // 对于其他餐品，使用默认图片
        imageUrl = `http://192.168.101.47/api/file/image/farm_0000000000009.jpg`;
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
    const { minPrice, maxPrice, goodsCache, currentCategory, searchKeyword } = this.data;

    let sourceGoods = goodsCache[currentCategory] || [];

    if (sourceGoods.length === 0) {
      sourceGoods = this.data.currentCategoryGoods || [];
    }

    if (!minPrice && !maxPrice) {
      let filteredGoods = [...sourceGoods];

      if (searchKeyword && searchKeyword.trim()) {
        filteredGoods = filteredGoods.filter(item => {
          const name = item.name || '';
          return name.includes(searchKeyword.trim());
        });
      }

      this.setData({ currentCategoryGoods: filteredGoods });
      return;
    }

    if (minPrice && maxPrice) {
      const min = parseFloat(minPrice);
      const max = parseFloat(maxPrice);

      if (!isNaN(min) && !isNaN(max) && min > max) {
        return;
      }
    }

    let filteredGoods = [...sourceGoods];

    if (searchKeyword && searchKeyword.trim()) {
      filteredGoods = filteredGoods.filter(item => {
        const name = item.name || '';
        return name.includes(searchKeyword.trim());
      });
    }

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

  onQuantityInput(e) {
    const id = e.currentTarget.dataset.id;
    const value = parseInt(e.detail.value) || 0;
    const goods = this.data.currentCategoryGoods.find(item => item.id === id);
    if (!goods) return;

    const key = String(id);
    const newCart = { ...this.data.cart };

    if (value <= 0) {
      delete newCart[key];
    } else {
      const quantity = Math.min(value, goods.stock);
      if (newCart[key]) {
        newCart[key].quantity = quantity;
      } else {
        newCart[key] = {
          ...goods,
          quantity: quantity,
          goodsId: goods.id
        };
      }
    }

    this.syncCartState(newCart);
  },

  stopPropagation() {
    return false;
  },

  syncCartState(newCart) {
    let count = 0;
    const cartArray = Object.values(newCart).map(item => ({
      ...item,
      count: item.quantity, // 统一使用count字段
      goodsId: item.id, // 添加goodsId字段，确保API调用正确
      type: 'goods' // 设置商品类型为goods
    }));
    
    cartArray.forEach(item => {
      count += item.count;
    });

    this.setData({ cart: newCart, cartCount: count });

    // 先获取现有的cartList，只保留非goods类型的商品
    const existingCartList = wx.getStorageSync('cartList') || [];
    const nonGoodsItems = existingCartList.filter(item => item.type !== 'goods');
    // 合并现有非goods商品和新的goods商品
    const updatedCartList = [...nonGoodsItems, ...cartArray];
    
    try {
      wx.setStorageSync('cartList', updatedCartList);
    } catch (e) {}
  },

  updateCartCount() {
    const cartList = wx.getStorageSync('cartList') || [];
    let totalCount = 0;
    const cart = {};
    // 只处理type为goods的商品
    cartList.filter(item => item.type === 'goods').forEach(item => {
      // 处理来自首页购物车的数据，确保字段一致
      const quantity = item.quantity || item.count || 0;
      totalCount += quantity;
      const itemId = item.id || item.goodsId;
      if (itemId) {
        cart[String(itemId)] = {
          ...item,
          id: itemId,
          quantity: quantity, // 统一使用quantity字段
          goodsId: item.goodsId || itemId, // 确保goodsId存在
          type: 'goods' // 确保type为goods
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
    if (categoryId === 'acre') {
      // 切换到认购专区，仅高亮并滚动
      this.setData({
        currentCategory: 'acre',
        showCategory: false,
        showCategoryView: true,
        showAcreSection: true
      });
      wx.pageScrollTo({ selector: '#acre-section', duration: 300 });
      return;
    }
    this.setData({
      showCategory: false,
      showCategoryView: true,
      showAcreSection: false
    });
    this.loadCategoryGoods(categoryId);
  },

  // ===== 认购一亩田数据 =====
  loadAcreData() {
    this.setData({ acreLoading: true });
    api.request({
      url: '/api/acres/index',
      method: 'GET',
      showLoading: false
    }).then(res => {
      const list = (res.list || []).map(item => ({
        ...item,
        price: typeof item.price === 'string' ? item.price.replace(/[¥￥]/g, '') : item.price
      }));
      this.setData({ acreList: list, acreLoading: false });
    }).catch(() => {
      this.setData({ acreLoading: false });
    });
  },

  navigateToAcreDetail(e) {
    const id = e.currentTarget.dataset.id;
    wx.navigateTo({ url: '/user-pages/acre-detail/acre-detail?id=' + id });
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
    wx.navigateTo({ url: '/user-pages/goods-detail/goods-detail?id=' + goodsId });
  },

  onReachBottom() {
    if (!this.data.loadingMore && this.data.hasMore) {
      this.loadCategoryGoods(this.data.currentCategory, true);
    }
  },

  // 获取筛选图标
  getFilterIcon() {
    const iconName = 'PriceFilter.png';
    const cacheKey = `filter_icon_${iconName}`;
    
    // 首先检查本地缓存
    try {
      const cachedIcon = wx.getStorageSync(cacheKey);
      if (cachedIcon) {
        this.setData({ filterIconUrl: cachedIcon });
        return;
      }
    } catch (e) {
      console.error('读取缓存失败:', e);
    }
    
    // 如果没有缓存，从API获取
    const iconUrl = `http://192.168.101.47/api/file/image/${iconName}`;
    
    // 下载图标到本地
    wx.downloadFile({
      url: iconUrl,
      success: (res) => {
        if (res.statusCode === 200) {
          // 缓存到本地
          try {
            wx.setStorageSync(cacheKey, res.tempFilePath);
            this.setData({ filterIconUrl: res.tempFilePath });
          } catch (e) {
            console.error('缓存图标失败:', e);
            // 即使缓存失败，也使用临时路径
            this.setData({ filterIconUrl: res.tempFilePath });
          }
        } else {
          // 下载失败，使用默认URL
          this.setData({ filterIconUrl: iconUrl });
        }
      },
      fail: (err) => {
        console.error('下载图标失败:', err);
        // 失败时使用默认URL
        this.setData({ filterIconUrl: iconUrl });
      }
    });
  }
});
