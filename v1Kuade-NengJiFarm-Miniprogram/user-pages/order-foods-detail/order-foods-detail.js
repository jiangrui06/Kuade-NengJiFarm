const api = require('../../utils/api');

Page({
  data: {
    goods: {},
    food: {},
    loading: true,
    count: 1,
    cartCount: 0,
    showTableModal: false,
    swiperList: [],
    hasVideo: false
  },

  onLoad(options) {
    const id = options.id;
    if (id) {
      this.getFoodDetail(id);
    }
    this.updateCartCount();
  },

  getFoodDetail(id) {
    this.setData({ loading: true });
    // api.goods.getDetail(id) 可能不存在，建议直接用 request
    const { request } = require('../../utils/api');
    request({
      url: `/api/goods/${id}`,
      method: 'GET'
    })
      .then(data => {
        // 视频处理：兼容 videoUrl / video / video_url 字段
        const rawVideoUrl = data.videoUrl || data.video || data.video_url || '';
        let videoUrl = '';
        if (rawVideoUrl) {
          videoUrl = String(rawVideoUrl).startsWith('http') ? String(rawVideoUrl) : this.processImageUrl(String(rawVideoUrl));
        }
        const hasVideo = !!videoUrl;
        
        const goods = {
          ...data,
          image: this.processImageUrl(data.image),
          detailImage: this.processImageUrl(data.detailImage || data.image),
          price: typeof data.price === 'string' ? data.price.replace(/[¥￥]/g, '') : data.price,
          videoUrl: videoUrl
        };
        const swiperList = (data.swiperList || []).map((item, index) => ({
          id: item.id || index,
          image: this.processImageUrl(item.image)
        }));
        if (swiperList.length === 0) {
          swiperList.push({ id: 0, image: goods.image });
        }
        this.setData({
          goods,
          food: goods,
          swiperList,
          hasVideo: hasVideo,
          loading: false
        });
      })
      .catch(err => {
        console.error('获取菜品详情失败:', err);
        this.setData({ loading: false });
        wx.showToast({ title: '加载失败', icon: 'none' });
      });
  },

  processImageUrl(imageUrl) {
    const utils = require('../../utils/utils');
    return utils.media.processUrl(imageUrl);
  },

  updateCartCount() {
    const cart = wx.getStorageSync('orderCart') || {};
    let count = 0;
    for (let id in cart) {
      count += cart[id].quantity || 0;
    }
    this.setData({ cartCount: count });
  },

  minusCount() {
    if (this.data.count > 1) {
      this.setData({ count: this.data.count - 1 });
    }
  },

  plusCount() {
    this.setData({ count: this.data.count + 1 });
  },

  addToCart() {
    const tableNumber = wx.getStorageSync('tableNumber');
    if (!tableNumber) {
      this.setData({ showTableModal: true });
      return;
    }

    const { goods, count } = this.data;
    const cart = wx.getStorageSync('orderCart') || {};
    const id = String(goods.id);
    
    if (cart[id]) {
      cart[id].quantity += count;
      cart[id].count = cart[id].quantity;
    } else {
      cart[id] = {
        id: id,
        name: goods.name,
        price: parseFloat(goods.price),
        image: goods.image,
        quantity: count,
        count: count,
        type: 'food',
        checked: true,
        stock: goods.stock || 999
      };
    }

    try {
      wx.setStorageSync('orderCart', cart);
      this.updateCartCount();
      wx.showToast({ title: '已加入购物车', icon: 'success' });
    } catch (e) {
      console.error('存储购物车失败', e);
      wx.showToast({ title: '加入购物车失败', icon: 'none' });
    }
  },

  buyNow() {
    const tableNumber = wx.getStorageSync('tableNumber');
    if (!tableNumber) {
      this.setData({ showTableModal: true });
      return;
    }
    
    // 点餐逻辑通常是先加购物车再结算，或者直接跳确认订单
    this.addToCart();
    wx.switchTab({ url: '/pages/cart/cart' });
  },

  viewCart() {
    wx.switchTab({ url: '/pages/cart/cart' });
  },

  goToCart() {
    this.viewCart();
  },

  closeTableModal() {
    this.setData({ showTableModal: false });
  },

  confirmTableModal() {
    this.setData({ showTableModal: false });
    wx.switchTab({ url: '/pages/index/index' });
  },

  previewSwiperImage(e) {
    const current = e.currentTarget.dataset.url;
    const urls = this.data.swiperList.map(item => item.image);
    wx.previewImage({ current, urls });
  },

  previewDetailImage(e) {
    const current = e.currentTarget.dataset.url;
    const urls = [this.data.goods.image, this.data.goods.detailImage].filter(Boolean);
    wx.previewImage({ current, urls });
  }
});