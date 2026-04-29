const api = require('../../utils/api');

Page({
  data: {
    categories: [
      { id: 'all', name: '全部菜品' },
      { id: 'hot', name: '热销推荐' },
      { id: 'dish', name: '特色菜' },
      { id: 'drink', name: '酒水饮料' },
      { id: 'staple', name: '主食' }
    ],
    activeCategory: 'all',
    goodsList: [],
    displayList: [],
    mergedGoodsList: [],
    scrollIntoViewId: '',
    cart: {},
    cartItems: [],
    cartCount: 0,
    totalPrice: 0,
    showCartModal: false,
    showTableModal: false,
    tableNumber: null,
    tableList: [
      { id: 1, name: '1号桌' },
      { id: 2, name: '2号桌' },
      { id: 3, name: '3号桌' },
      { id: 5, name: '5号桌' },
      { id: 6, name: '6号桌' },
      { id: 8, name: '8号桌' }
    ],
    loading: true
  },

  onLoad(options) {
    const tableNumber = options.tableNumber || wx.getStorageSync('tableNumber');
    if (tableNumber) {
      this.setData({ tableNumber });
      wx.setStorageSync('tableNumber', tableNumber);
      wx.showToast({ title: `已绑定桌号${tableNumber}`, icon: 'success' });
    }
    this.getCategories();
    this.initCart();
  },

  getCategories() {
    this.setData({ loading: true });
    api.goods.getCategories({ type: 'food' })
      .then(data => {
        const categories = [
          { id: 'all', name: '全部菜品' },
          ...(data || []).map(cat => ({
            id: String(cat.id),
            name: cat.name
          }))
        ];
        this.setData({ categories });
        this.getGoodsList();
      })
      .catch(err => {
        console.error('获取分类失败:', err);
        this.getGoodsList();
      });
  },

  getGoodsList() {
    this.setData({ loading: true });
    api.goods.getList({ type: 'food', pageSize: 100 })
      .then(data => {
        const list = (data || []).map(item => ({
          ...item,
          image: this.processImageUrl(item.image),
          price: typeof item.price === 'string' ? item.price.replace(/[¥￥]/g, '') : item.price,
          sold: item.sold || 0,
          stock: item.stock || 0
        }));
        
        // 构造用于滚动的合并列表
        const mergedList = [];
        this.data.categories.forEach(cat => {
          if (cat.id === 'all') return;
          const catGoods = list.filter(g => String(g.categoryId) === String(cat.id));
          if (catGoods.length > 0) {
            mergedList.push({ type: 'category', id: cat.id, name: cat.name, uniqueKey: 'cat-' + cat.id });
            catGoods.forEach(g => {
              mergedList.push({ ...g, type: 'goods', uniqueKey: 'goods-' + g.id });
            });
          }
        });

        this.setData({
          goodsList: list,
          displayList: list,
          mergedGoodsList: mergedList,
          loading: false
        });
      })
      .catch(err => {
        console.error('获取菜品列表失败:', err);
        this.setData({ loading: false });
        wx.showToast({ title: '获取列表失败', icon: 'none' });
      });
  },

  processImageUrl(imageUrl) {
    const utils = require('../../utils/utils');
    return utils.media.processUrl(imageUrl);
  },

  initCart() {
    const cart = wx.getStorageSync('orderCart') || {};
    this.setData({ cart });
    this.calculateCart();
  },

  calculateCart() {
    const cart = this.data.cart;
    let count = 0;
    let total = 0;
    const cartItems = [];
    for (let id in cart) {
      count += cart[id].quantity;
      total += cart[id].price * cart[id].quantity;
      cartItems.push(cart[id]);
    }
    this.setData({
      cartCount: count,
      totalPrice: total.toFixed(2),
      cartItems: cartItems
    });
    wx.setStorageSync('orderCart', cart);
  },

  switchCategory(e) {
    const id = e.currentTarget.dataset.category;
    if (!id) return;
    
    this.setData({
      activeCategory: id,
      scrollIntoViewId: id === 'all' ? '' : `category-${id}`
    });
  },

  onScroll(e) {
    // 滚动联动逻辑（可选实现）
  },

  addToCart(e) {
    const id = e.currentTarget.dataset.id;
    const item = this.data.goodsList.find(g => String(g.id) === String(id));
    if (!item) return;

    const cart = this.data.cart;
    if (cart[id]) {
      cart[id].quantity += 1;
    } else {
      cart[id] = {
        id: item.id,
        name: item.name,
        price: parseFloat(item.price),
        image: item.image,
        quantity: 1
      };
    }
    this.setData({ cart });
    this.calculateCart();
  },

  increaseQuantity(e) {
    this.addToCart(e);
  },

  decreaseQuantity(e) {
    const id = e.currentTarget.dataset.id;
    const cart = this.data.cart;
    if (cart[id]) {
      if (cart[id].quantity > 1) {
        cart[id].quantity -= 1;
      } else {
        delete cart[id];
      }
      this.setData({ cart });
      this.calculateCart();
    }
  },

  onQuantityInput(e) {
    const id = e.currentTarget.dataset.id;
    const val = parseInt(e.detail.value) || 0;
    const cart = this.data.cart;
    const item = this.data.goodsList.find(g => String(g.id) === String(id));
    
    if (val > 0 && item) {
      cart[id] = {
        id: item.id,
        name: item.name,
        price: parseFloat(item.price),
        image: item.image,
        quantity: val
      };
    } else {
      delete cart[id];
    }
    this.setData({ cart });
    this.calculateCart();
  },

  selectTable() {
    this.setData({ showTableModal: true });
  },

  hideTableModal() {
    this.setData({ showTableModal: false });
  },

  selectTableNumber(e) {
    const tableId = e.currentTarget.dataset.tableId;
    this.setData({
      tableNumber: tableId,
      showTableModal: false
    });
    wx.setStorageSync('tableNumber', tableId);
    wx.showToast({ title: `已选择${tableId}号桌`, icon: 'success' });
  },

  testScanCode() {
    wx.scanCode({
      success: (res) => {
        // 假设码内容是 tableNumber=5
        console.log('扫码结果:', res);
        wx.showToast({ title: '扫码成功', icon: 'success' });
      }
    });
  },

  clearCart() {
    wx.showModal({
      title: '清空购物车',
      content: '确定要清空已选菜品吗？',
      success: (res) => {
        if (res.confirm) {
          this.setData({ cart: {}, cartCount: 0, totalPrice: 0, cartItems: [] });
          wx.removeStorageSync('orderCart');
        }
      }
    });
  },

  viewCart() {
    if (this.data.cartCount === 0) return wx.showToast({ title: '购物车为空', icon: 'none' });
    this.setData({ showCartModal: true });
  },

  hideCartModal() {
    this.setData({ showCartModal: false });
  },

  checkout() {
    if (this.data.cartCount === 0) return wx.showToast({ title: '购物车为空', icon: 'none' });
    if (!this.data.tableNumber) {
      wx.showModal({
        title: '提示',
        content: '请先选择桌号',
        showCancel: false
      });
      return;
    }
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

  stopPropagation() {
    return;
  }
});

