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

  onShow: function () {
    this.restoreCart();
    this.updateCartCount();
  },

  getCategories: function () {
    api.farmGoods.getCategories()
      .then(categories => {
        let finalCategories = categories || [];

        const hasAll = finalCategories.some(c => c.id === 'all');
        if (!hasAll) {
          finalCategories.unshift({ id: 'all', name: '全部商品' });
        }

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

    api.farmGoods.getList({ type: 'goods' })
      .then(goodsData => {
        const goodsList = (goodsData || []).map(item => ({
          ...item,
          image: this.processImageUrl(item.image),
          price: typeof item.price === 'string' ? item.price.replace(/[¥￥]/g, '') : item.price,
          originalPrice: typeof item.originalPrice === 'string' ? item.originalPrice.replace(/[¥￥]/g, '') : item.originalPrice,
          tags: item.tags || [],
          stock: item.stock || 0
        }));

        this.setData({
          goodsList: goodsList,
          currentCategoryGoods: goodsList,
          loading: false
        });

        api.acre.getList({ showLoading: false })
          .then(acreData => {
            const acreArray = Array.isArray(acreData) ? acreData : (acreData?.list || acreData?.records || []);
            const acreList = acreArray.map(item => ({
              ...item,
              image: this.processImageUrl(item.image || item.cover),
              price: typeof item.price === 'string' ? item.price.replace(/[¥￥]/g, '') : item.price,
              tags: ['可认购'],
              stock: item.stock || 0
            }));
            this.setData({ acreList: acreList });
          })
          .catch(err => {
            console.error('获取田地列表失败:', err);
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
    const { currentCategory, searchKeyword, minPrice, maxPrice, goodsList, acreList } = this.data;

    let list = goodsList;

    if (currentCategory === 'acre') {
      list = acreList;
    } else if (currentCategory !== 'all') {
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
      cartWithChecked[key] = {
        ...newCart[key],
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