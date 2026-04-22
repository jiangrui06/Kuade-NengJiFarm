const api = require('../../utils/api').api;

Page({
  data: {
    goods: {},
    swiperList: [],
    loading: true,
    cart: {},
    cartCount: 0,
    totalPrice: 0,
    showTableModal: false
  },

  processImageUrl(imageUrl) {
    if (!imageUrl) return '';
    const cleaned = String(imageUrl).replace(/[`\s]/g, '');
    if (cleaned.startsWith('http://') || cleaned.startsWith('https://')) {
      return cleaned;
    }
    return 'http://192.168.203.56' + cleaned;
  },

  onLoad(options) {
    let goodsData = {};
    
    // 解析从点餐页面传递过来的参数
    if (options.params) {
      try {
        goodsData = JSON.parse(decodeURIComponent(options.params));
      } catch (e) {
        console.error('解析参数失败', e);
      }
    }
    
    const id = options.id || goodsData.id;
    if (id) {
      this.getGoodsDetail(id, goodsData);
    }
    // 恢复购物车
    this.restoreCart();
  },

  onShow() {
    // 页面显示时更新购物车数据
    this.restoreCart();
  },

  getGoodsDetail(id, goodsData = {}) {
    wx.showLoading({ title: '加载中...' });
    const videoUrl = 'http://192.168.203.56/api/file/video/farm_intro.mp4';
    // 调用后端API获取商品详情
    api.goods.getDetail(id)
      .then(data => {
        const goodsImage = this.processImageUrl(data.image) || '';
        const detailImage = this.processImageUrl(data.detailImage) || goodsImage;
        const apiSwiperList = (data.swiperList || []).map(item => ({
          ...item,
          image: this.processImageUrl(item.image)
        }));
        let swiperList = apiSwiperList;
        if (swiperList.length === 0 && detailImage) {
          swiperList = [
            { id: 1, image: detailImage },
            { id: 2, image: goodsImage }
          ];
        }
        // 优先使用从点餐页面传递过来的已售和库存数据，确保数据一致
        const goods = {
          ...data,
          sold: goodsData.sold !== undefined ? goodsData.sold : (data.sold || data.sales || 0),
          stock: goodsData.stock !== undefined ? goodsData.stock : (data.stock || 0),
          price: data.price ? data.price.toString().replace(/[¥￥]/g, '') : data.price,
          image: goodsImage,
          detailImage: detailImage,
          videoUrl: videoUrl
        };
        this.setData({
          goods: goods,
          swiperList: swiperList,
          loading: false
        });
      })
      .catch(err => {
        console.error('获取商品详情失败', err);
        wx.showToast({ title: '获取商品详情失败', icon: 'none' });
        this.setData({ loading: false });
      })
      .finally(() => {
        wx.hideLoading();
      });
  },

  addToCart() {
    const goods = this.data.goods;
    if (!goods?.id) return;

    const key = String(goods.id);
    const newCart = { ...this.data.cart };

    if (newCart[key]) {
      if (newCart[key].quantity >= goods.stock) {
        wx.showToast({ title: '库存不足', icon: 'none' });
        return;
      }
      newCart[key].quantity += 1;
    } else {
      newCart[key] = { ...goods, quantity: 1 };
    }
    this.syncCartState(newCart);
    wx.showToast({ title: '已加入购物车', icon: 'success' });
  },

  buyNow() {
    const goods = this.data.goods;
    if (!goods?.id) return;

    // 将当前商品加入购物车
    const key = String(goods.id);
    const newCart = { ...this.data.cart };
    newCart[key] = { ...goods, quantity: 1 };
    this.syncCartState(newCart);
    
    // 跳转到确认订单页面
    wx.navigateTo({ url: '/subpkg/confirm-order/confirm-order' });
  },

  // 关闭桌台提示浮窗
  closeTableModal() {
    this.setData({ showTableModal: false });
  },

  // 确定按钮点击事件，跳转到点餐页面
  confirmTableModal() {
    this.setData({ showTableModal: false });
    wx.redirectTo({ url: '/subpkg/order/order' });
  },

  viewCart() {
    // 点击购物车图标返回点餐页面
    wx.redirectTo({ url: '/subpkg/order/order' });
  },

  // 监听页面卸载，确保返回点餐页面
  onUnload() {
    // 不做任何操作，让系统默认返回
  },

  // 自定义返回按钮点击事件
  goBack() {
    wx.redirectTo({ url: '/subpkg/order/order' });
  },

  // 监听返回按钮点击事件
  onBackPress() {
    wx.redirectTo({ url: '/subpkg/order/order' });
    return true; // 阻止默认返回行为
  },

  syncCartState(newCart) {
    let count = 0, total = 0;
    Object.values(newCart || {}).forEach(item => {
      if (!item) return;
      const quantity = item.quantity || 0;
      const price = item.price || 0;
      count += quantity;
      total += price * quantity;
    });
    this.setData({
      cart: newCart,
      cartCount: count,
      totalPrice: parseFloat(total.toFixed(2))
    });
    try { 
      wx.setStorageSync('orderCart', newCart);
    } catch (e) {
      console.error('存储购物车失败', e);
    }
  },

  restoreCart() {
    try {
      const cart = wx.getStorageSync('orderCart') || {};
      let count = 0, total = 0;
      Object.values(cart || {}).forEach(item => {
        if (!item) return;
        const quantity = item.quantity || 0;
        const price = item.price || 0;
        count += quantity;
        total += price * quantity;
      });
      this.setData({
        cart: cart || {},
        cartCount: count,
        totalPrice: parseFloat(total.toFixed(2))
      });
    } catch (e) {
      console.error('恢复购物车失败', e);
    }
  },

  contactService() {
    wx.showModal({
      title: '能记家庭农场客服',
      content: '手机号：15876534944\n     微信号：njjtnc15876534944',
      showCancel: false
    });
  }
});