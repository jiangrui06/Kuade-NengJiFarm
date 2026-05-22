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
      this.setData({ _foodId: id });
      this.getFoodDetail(id);
    }
    this.updateCartCount();
  },

  onShow() {
    // 从其他页面返回时刷新数据和购物车
    if (this.data._foodId) {
      this.getFoodDetail(this.data._foodId);
    }
    this.updateCartCount();
  },

  getFoodDetail(id) {
    this.setData({ loading: true });
    const { request } = require('../../utils/api');

    const doRequest = (url, params) => new Promise(resolve => {
      request({ url, method: 'GET', data: params, showLoading: false })
        .then(resolve)
        .catch(() => resolve(null));
    });

    // 同时请求所有可能的接口，按可信度排序
    Promise.all([
      doRequest(`/api/dish/detail`, { id }),         // 主接口：正确返回 specImages
      doRequest(`/api/goods/detail`, { goodsId: id }),
      doRequest(`/api/goods/detail`, { goods_id: id }),
      doRequest(`/api/goods/${id}`, { type: 'food' })
    ]).then(([dishDetail, detail1, detail2, basic]) => {
      // 取第一个非空的响应（dishDetail 优先）
      const data = dishDetail || detail1 || detail2 || basic || {};

      // 视频处理
      const rawVideoUrl = data.videoUrl || data.video || data.video_url || '';
      let videoUrl = '';
      if (rawVideoUrl) {
        videoUrl = String(rawVideoUrl).startsWith('http') ? String(rawVideoUrl) : this.processImageUrl(String(rawVideoUrl));
      }
      const hasVideo = !!videoUrl;

      // 规格图：兼容所有可能的字段名（跳过空数组）
      let rawImages;
      if (data.specImages && data.specImages.length > 0) {
        rawImages = data.specImages;
      } else if (data.detailImages && data.detailImages.length > 0) {
        rawImages = data.detailImages;
      } else if (data.images && data.images.length > 0) {
        rawImages = data.images;
      } else {
        rawImages = data.detail_image || data.detailImgs ||
          data.goodsImages || data.imageList || data.pictures || data.imgList ||
          data.goods_image || data.goodsImg || [];
      }

      // 统一转成字符串数组
      let detailImages = [];
      if (Array.isArray(rawImages)) {
        detailImages = rawImages.map(item => {
          if (typeof item === 'string') return item;
          if (item && typeof item === 'object') return item.image || item.url || item.src || '';
          return '';
        });
      } else if (typeof rawImages === 'string') {
        // 可能是逗号分隔的字符串
        detailImages = rawImages.split(',').filter(Boolean);
      }
      detailImages = detailImages.map(url => this.processImageUrl(url)).filter(Boolean);

      // 如果取到的规格图数量 < 轮播图数量，可能字段对应错了，尝试从 swiperList 以后台配置为准
      // 但如果上面取到了就直接用

      const goods = {
        ...data,
        image: this.processImageUrl(data.image),
        detailImages,
        price: typeof data.price === 'string' ? data.price.replace(/[¥￥]/g, '') : data.price,
        videoUrl
      };

      // 轮播图：兼容多种字段名
      const rawSwiper =
        data.swiperList || data.swiperImages || data.swiperImgs ||
        data.carouselMedia || data.carouselList || data.carouselImages ||
        data.bannerList || data.banners || data.slides || [];
      const swiperList = (Array.isArray(rawSwiper) ? rawSwiper : []).map((item, index) => ({
        id: item.id || index,
        image: this.processImageUrl(item.image || item.url || item.src || (typeof item === 'string' ? item : ''))
      }));
      if (swiperList.length === 0) {
        swiperList.push({ id: 0, image: goods.image });
      }

      this.setData({
        goods,
        food: goods,
        swiperList,
        hasVideo,
        loading: false
      });
    }).catch(err => {
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

    // 检查库存
    const stock = goods.stock || 0;
    if (stock <= 0) {
      wx.showToast({ title: '商品已售空', icon: 'none' });
      return;
    }
    const currentInCart = cart[id] ? (cart[id].quantity || cart[id].count || 0) : 0;
    if (currentInCart + count > stock) {
      wx.showToast({ title: '商品已售空', icon: 'none' });
      return;
    }

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
      wx.showToast({ title: '加入购物车失败', icon: 'none' });
    }
  },

  buyNow() {
    const tableNumber = wx.getStorageSync('tableNumber');
    if (!tableNumber) {
      this.setData({ showTableModal: true });
      return;
    }

    const { goods, count } = this.data;

    // 检查库存：购物车中已加的数量 + 本次购买数量 <= 库存
    const stock = goods.stock || 0;
    const cart = wx.getStorageSync('orderCart') || {};
    const id = String(goods.id);
    const currentInCart = cart[id] ? (cart[id].quantity || cart[id].count || 0) : 0;

    if (currentInCart + count > stock) {
      wx.showToast({ title: '商品库存不足', icon: 'none' });
      return;
    }

    // 保存立即购买的商品数据到临时存储（不影响购物车数据）
    const buyNowItem = {
      id: id,
      name: goods.name,
      price: parseFloat(goods.price),
      image: goods.image,
      quantity: count,
      count: count,
      type: 'food',
      stock: goods.stock || 999,
      checked: true
    };
    wx.setStorageSync('tempBuyNowItem', buyNowItem);

    // 直接跳转到确认订单页面，不经过购物车
    wx.navigateTo({
      url: `/user-pages/confirm-order/confirm-order?type=food&from=buyNow&tableNumber=${tableNumber}`
    });
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
    wx.navigateTo({ url: '/user-pages/order/order' });
  },

  previewSwiperImage(e) {
    const current = e.currentTarget.dataset.url;
    const urls = this.data.swiperList.map(item => item.image);
    wx.previewImage({ current, urls });
  },

  previewDetailImages(e) {
    const current = e.currentTarget.dataset.url;
    const urls = (this.data.goods.detailImages || []).filter(Boolean);
    wx.previewImage({ current, urls });
  }
});