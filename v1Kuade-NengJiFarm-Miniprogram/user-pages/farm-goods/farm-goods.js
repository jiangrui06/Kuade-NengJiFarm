const api = require('../../utils/api');

Page({
  data: {
    categories: [],
    currentCategory: 'all',
    goodsList: [],
    acreList: [],
    currentCategoryGoods: [],
    loading: true,
    cartCount: 0,
    searchKeyword: '',
    showCategory: false,
    showFilterDrawer: false,
    minPrice: '',
    maxPrice: '',
    loadingMore: false,
    filterIconUrl: '/images/PriceFilter.png'
  },

  onLoad: function () {
    this.getCategories();
    this.getGoodsList();
    this.updateCartCount();
  },

  getCategories: function () {
    api.farmGoods.getCategories()
      .then(data => {
        const list = Array.isArray(data) ? data : (data.categories || data.list || []);
        const categories = list.map(item => ({
          id: item.id || item.categoryId || '',
          name: item.name || item.categoryName || ''
        })).filter(item => item.id && item.name);

        this.setData({
          categories: [
            { id: 'all', name: '全部商品' },
            ...categories
          ]
        });
      })
      .catch(err => {
        this.setData({
          categories: [
            { id: 'all', name: '全部商品' }
          ]
        });
      });
  },

  onShow: function () {
    this.restoreCart();
    this.updateCartCount();
  },

  getGoodsList: function () {
    this.setData({ loading: true });

    // 使用集成API获取所有商品（普通商品 + 认购商品）
    api.farmGoods.getList({ type: 'all' })
      .then(data => {
        const list = data.list || [];

        // 分离普通商品和认购商品
        const goodsList = list.filter(item => item.type === 'goods').map(item => ({
          ...item,
          id: String(item.id),  // 确保ID为字符串
          image: this.processImageUrl(item.image),
          price: typeof item.price === 'string' ? item.price.replace(/[¥￥]/g, '') : item.price,
          originalPrice: typeof item.originalPrice === 'string' ? item.originalPrice.replace(/[¥￥]/g, '') : item.originalPrice,
          tags: item.tags || [],
          stock: item.stock || 0
        }));

        const acreList = list.filter(item => item.type === 'acre').map(item => ({
          ...item,
          id: String(item.id),  // 确保ID为字符串
          image: this.processImageUrl(item.image || item.cover),
          price: typeof item.price === 'string' ? item.price.replace(/[¥￥]/g, '') : item.price,
          tags: item.tags || [],
          stock: item.stock || 0
        }));


        this.setData({
          goodsList: goodsList,
          acreList: acreList,
          loading: false
        }, () => {
          // 更新当前分类商品列表
          this.filterGoods();
        });
      })
      .catch(err => {
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
    const { currentCategory, searchKeyword, minPrice, maxPrice, goodsList, acreList } = this.data;

    let list = [];

    if (currentCategory === 'all') {
      list = [...goodsList, ...acreList];
    } else {
      list = goodsList.filter(item => item.categoryId === currentCategory || item.category === currentCategory);
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
    const goods = this.findGoodsById(id);

    if (!goods) {
      return;
    }


    if (goods.type === 'acre') {
      // 认购商品跳转到统一商品详情页
      wx.navigateTo({
        url: `/user-pages/goods-detail/goods-detail?id=${id}&isFarmGood=1`
      });
    } else {
      // 普通商品跳转到商品详情页
      wx.navigateTo({
        url: `/user-pages/goods-detail/goods-detail?id=${id}`
      });
    }
  },

  findGoodsById(id) {
    const goods = this.data.goodsList.find(g => String(g.id) === String(id));
    if (goods) return goods;
    return this.data.acreList.find(g => String(g.id) === String(id));
  },

  addToCart(e) {
    const id = e.currentTarget.dataset.id;
    const goods = this.findGoodsById(id);
    if (!goods) return;

    if (goods.stock === 0) {
      return wx.showToast({ title: '暂无库存', icon: 'none' });
    }

    const newCart = { ...this.data.cart };
    const key = String(id);
    if (newCart[key]) {
      if (newCart[key].quantity >= goods.stock) {
        return wx.showToast({ title: '库存不足', icon: 'none' });
      }
      newCart[key].quantity++;
    } else {
      newCart[key] = { ...goods, quantity: 1 };
    }
    this.syncCartState(newCart);
  },

  increaseQuantity(e) {
    const id = String(e.currentTarget.dataset.id);
    const goods = this.findGoodsById(id);
    if (!goods) return;

    if (goods.stock === 0) {
      return wx.showToast({ title: '暂无库存', icon: 'none' });
    }

    const newCart = { ...this.data.cart };
    if (!newCart[id]) {
      newCart[id] = { ...goods, quantity: 1 };
    } else {
      if (newCart[id].quantity >= goods.stock) {
        return wx.showToast({ title: '库存不足', icon: 'none' });
      }
      newCart[id].quantity++;
    }
    this.syncCartState(newCart);
  },

  decreaseQuantity(e) {
    const id = String(e.currentTarget.dataset.id);
    const newCart = { ...this.data.cart };
    if (!newCart[id]) return;

    if (newCart[id].quantity <= 1) {
      delete newCart[id];
    } else {
      newCart[id].quantity--;
    }
    this.syncCartState(newCart);
  },

  onQuantityInput(e) {
    const id = String(e.currentTarget.dataset.id);
    const val = Math.max(0, parseInt(e.detail.value) || 0);
    const goods = this.findGoodsById(id);
    if (!goods) return;

    const newCart = { ...this.data.cart };
    if (val === 0) {
      delete newCart[id];
    } else {
      if (!newCart[id]) {
        newCart[id] = { ...goods, quantity: Math.min(val, goods.stock) };
      } else {
        newCart[id].quantity = Math.min(val, goods.stock);
      }
    }
    this.syncCartState(newCart);
  },

  syncCartState(newCart) {
    let count = 0;
    const cartWithChecked = {};
    for (const key in newCart) {
      const item = newCart[key];
      // 认购商品(type: 'acre')在购物车中统一转为普通商品(type: 'goods')，
      // 因为购物车系统(pages/cart/cart.js)只识别 'goods' 和 'food' 两种类型
      const cartType = item.type === 'acre' ? 'goods' : (item.type || 'goods');
      cartWithChecked[key] = {
        ...item,
        type: cartType,
        checked: true
      };
      count += cartWithChecked[key].quantity;
    }
    this.setData({ cart: newCart, cartCount: count });
    wx.setStorageSync('cartList', cartWithChecked);
  },

  restoreCart() {
    const cart = wx.getStorageSync('cartList') || {};
    const restoredCart = {};
    Object.values(cart || {}).forEach(i => {
      const key = String(i.id);
      restoredCart[key] = {
        ...i,
        checked: i.checked !== false
      };
    });
    this.setData({ cart: restoredCart });
  },

  stopPropagation() {
    return false;
  },

  onReachBottom() {
  },

  processImageUrl(imageUrl) {
    const utils = require('../../utils/utils');
    return utils.media.processUrl(imageUrl);
  },

  updateCartCount() {
    const cart = this.data.cart;
    let count = 0;
    for (const key in cart) {
      count += cart[key].quantity || 0;
    }
    this.setData({ cartCount: count });
  },

  goToCart() {
    wx.switchTab({ url: '/pages/cart/cart' });
  }
});