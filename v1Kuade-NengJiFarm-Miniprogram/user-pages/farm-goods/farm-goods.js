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
    // 模拟分类数据，实际应从接口获取
    const categories = [
      { id: 'all', name: '全部商品'},
      { id: 'vegetable', name: '新鲜蔬菜'},
      { id: 'fruit', name: '时令水果' },
      { id: 'meat', name: '禽畜肉蛋'},
      { id: 'dry', name: '干货特产' }
    ];
    this.setData({ categories });
  },

  getGoodsList: function () {
    this.setData({ loading: true });
    api.goods.getList({ type: 'goods' })
      .then(data => {
        const list = (data || []).map(item => ({
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
    let list = this.data.goodsList;
    const { currentCategory, searchKeyword, minPrice, maxPrice } = this.data;

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

