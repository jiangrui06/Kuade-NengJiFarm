const api = require('../../utils/api');

Page({
  data: {
    categories: [],
    currentCategory: 'all',
    goodsList: [],
    currentCategoryGoods: [],
    loading: true,
    cartCount: 0,
    searchKeyword: '',
    showCategory: false,
    showFilterDrawer: false,
    minPrice: '',
    maxPrice: '',
    acreList: [],
    acreLoading: false,
    loadingMore: false,
    cart: {},
    filterIconUrl: '/images/filter.png'
  },

  onLoad: function () {
    this.getCategories();
    this.getGoodsList();
    this.updateCartCount();
  },

  onShow: function () {
    this.updateCartCount();
  },

  getCategories: function () {
    api.farmGoods.getCategories()
      .then(categories => {
        // 确保有全部商品和认购专区
        let finalCategories = categories || [];
        
        // 检查是否已有全部商品
        const hasAll = finalCategories.some(c => c.id === 'all');
        if (!hasAll) {
          finalCategories.unshift({ id: 'all', name: '全部商品' });
        }
        
        // 检查是否已有认购专区
        const hasAcre = finalCategories.some(c => c.id === 'acre');
        if (!hasAcre) {
          finalCategories.push({ id: 'acre', name: '认购专区' });
        }
        
        this.setData({ categories: finalCategories });
      })
      .catch(err => {
        console.error('获取分类失败:', err);
       
       
      });
  },

  getGoodsList: function () {
    this.setData({ loading: true });
    
    // 同时获取商品列表和认购列表
    Promise.all([
      api.farmGoods.getList({ type: 'goods' }),
      this.getAcreList()
    ])
      .then(([goodsData]) => {
        const list = (goodsData || []).map(item => ({
          ...item,
          image: this.processImageUrl(item.image),
          price: typeof item.price === 'string' ? item.price.replace(/[¥￥]/g, '') : item.price,
          originalPrice: typeof item.originalPrice === 'string' ? item.originalPrice.replace(/[¥￥]/g, '') : item.originalPrice,
          tags: item.tags || []
        }));
        this.setData({
          goodsList: list,
          currentCategoryGoods: list,
          loading: false
        });
      })
      .catch(err => {
        console.error('获取商品列表失败:', err);
        this.setData({ loading: false });
        wx.showToast({ title: '加载失败', icon: 'none' });
      });
  },

  getAcreList: function () {
    this.setData({ acreLoading: true });
    return api.acre.getList()
      .then(data => {
        const list = (data || []).map(item => ({
          ...item,
          image: this.processImageUrl(item.image || item.cover)
        }));
        this.setData({
          acreList: list,
          acreLoading: false
        });
        return list;
      })
      .catch(err => {
        console.error('获取认购列表失败:', err);
        this.setData({ acreLoading: false });
        return [];
      });
  },

  onSearchInput(e) {
    const keyword = e.detail.value;
    this.setData({ searchKeyword: keyword });
    this.filterGoods();
  },

  selectCategory(e) {
    const id = e.currentTarget.dataset.id || 'all';
    this.setData({
      currentCategory: id,
      showCategory: false
    });
    this.filterGoods();
  },

  filterGoods() {
    const { currentCategory, searchKeyword, minPrice, maxPrice, goodsList, acreList } = this.data;
    
    // 如果是认购专区，不需要过滤商品列表
    if (currentCategory === 'acre') {
      this.setData({ currentCategoryGoods: [] });
      return;
    }

    let list = goodsList;

    if (currentCategory !== 'all') {
      list = list.filter(item => item.categoryId === currentCategory || item.category === currentCategory);
    }

    if (searchKeyword) {
      list = list.filter(item => item.name.includes(searchKeyword));
    }

    if (minPrice !== '') {
      list = list.filter(item => parseFloat(item.price) >= parseFloat(minPrice));
    }

    if (maxPrice !== '') {
      list = list.filter(item => parseFloat(item.price) <= parseFloat(maxPrice));
    }

    this.setData({ currentCategoryGoods: list });
  },

  showFilterDrawer() {
    this.setData({ showFilterDrawer: true });
  },

  hideFilterDrawer() {
    this.setData({ showFilterDrawer: false });
  },

  toggleCategory() {
    this.setData({ showCategory: !this.data.showCategory });
  },

  onMinPriceInput(e) {
    this.setData({ minPrice: e.detail.value });
  },

  onMaxPriceInput(e) {
    this.setData({ maxPrice: e.detail.value });
  },

  resetFilter() {
    this.setData({
      minPrice: '',
      maxPrice: '',
      searchKeyword: '',
      currentCategory: 'all'
    });
    this.filterGoods();
  },

  applyFilter() {
    this.filterGoods();
    this.hideFilterDrawer();
  },

  viewGoodsDetail(e) {
    const id = e.currentTarget.dataset.id;
    wx.navigateTo({
      url: `/user-pages/goods-detail/goods-detail?id=${id}`
    });
  },

  increaseQuantity(e) {
    const id = e.currentTarget.dataset.id;
    const cart = this.data.cart;
    if (!cart[id]) {
      cart[id] = { quantity: 1 };
    } else {
      cart[id].quantity += 1;
    }
    this.setData({ cart });
    this.syncToCartList(id, cart[id].quantity);
  },

  decreaseQuantity(e) {
    const id = e.currentTarget.dataset.id;
    const cart = this.data.cart;
    if (cart[id] && cart[id].quantity > 0) {
      cart[id].quantity -= 1;
      if (cart[id].quantity === 0) {
        delete cart[id];
      }
      this.setData({ cart });
      this.syncToCartList(id, cart[id] ? cart[id].quantity : 0);
    }
  },

  onQuantityInput(e) {
    const id = e.currentTarget.dataset.id;
    const val = parseInt(e.detail.value) || 0;
    const cart = this.data.cart;
    if (val > 0) {
      cart[id] = { quantity: val };
    } else {
      delete cart[id];
    }
    this.setData({ cart });
    this.syncToCartList(id, val);
  },

  syncToCartList(id, quantity) {
    const goods = this.data.goodsList.find(g => String(g.id) === String(id));
    if (!goods) return;

    let cartList = wx.getStorageSync('cartList') || [];
    const index = cartList.findIndex(i => String(i.id) === String(id));

    if (quantity > 0) {
      if (index > -1) {
        cartList[index].count = quantity;
      } else {
        cartList.push({
          id: goods.id,
          name: goods.name,
          price: parseFloat(goods.price),
          image: goods.image,
          count: quantity,
          checked: true,
          type: 'goods'
        });
      }
    } else if (index > -1) {
      cartList.splice(index, 1);
    }

    wx.setStorageSync('cartList', cartList);
    this.updateCartCount();
  },

  stopPropagation() {
    return;
  },

  onReachBottom() {
    // 触底加载逻辑
  },

  navigateToAcreDetail(e) {
    const id = e.currentTarget.dataset.id;
    wx.navigateTo({
      url: `/user-pages/acre-detail/acre-detail?id=${id}`
    });
  },

  processImageUrl(imageUrl) {
    const utils = require('../../utils/utils');
    return utils.media.processUrl(imageUrl);
  },

  updateCartCount() {
    const cartList = wx.getStorageSync('cartList') || [];
    const count = cartList.reduce((sum, item) => sum + (item.count || 0), 0);
    this.setData({ cartCount: count });
  },

  goToCart() {
    wx.switchTab({ url: '/pages/cart/cart' });
  }
});

