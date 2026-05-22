const { api, request } = require('../../utils/api');

Page({
  data: {
    goods: {
      id: '',
      name: '',
      price: 0,
      image: '',
      detailImage: '',
      description: '',
      spec: '',
      weight: '',
      storage: '',
      videoUrl: '',
      stock: 0
    },
    cartQuantity: 0,
    swiperList: [],
    hasVideo: false,
    loading: true,
    cartCount: 0,
    showBuyModal: false,
    addressList: [],
    selectedAddress: null,
    defaultAddress: null,
    showAllAddresses: false,
    quantity: 1,
    totalPrice: '0',
    isFarmGood: false
  },

  processImageUrl(imageUrl) {
    const utils = require('../../utils/utils');
    return utils.media.processUrl(imageUrl);
  },

  onLoad(options) {
    const goodsId = options.id;
    const isFarmGood = options.isFarmGood === '1';

    if (!goodsId) {
      this.setData({ loading: false });
      wx.showToast({
        title: '缺少商品ID',
        icon: 'none'
      });
      return;
    }

    this.setData({ isFarmGood: isFarmGood || false });
    this.getGoodsDetail(goodsId);
    this.updateCartCount();
  },

  onShow() {
    this.updateCartCount();
    // 重新获取商品详情，从后端获取真实库存
    if (this.data.goods.id) {
      this.getGoodsDetail(this.data.goods.id);
    }
  },

  getGoodsDetail(goodsId) {
    wx.showLoading({ title: '加载中...' });

    // 尝试多个可能的接口
    const { request } = require('../../utils/api');
    const doRequest = (url, params) => new Promise(resolve => {
      request({ url, method: 'GET', data: params, showLoading: false })
        .then(resolve)
        .catch(() => resolve(null));
    });

    Promise.all([
      doRequest(`/api/dish/detail`, { id: goodsId }),
      doRequest(`/api/goods/detail`, { goodsId })
    ]).then(([dishDetail, goodsDetail]) => {
      // dishDetail 优先（包含 carouselMedia、specImages 等完整字段）
      const data = dishDetail || goodsDetail || {};
      if (!data || !data.id) {
        // 完全无数据时兜底
        this.setData({ loading: false });
        wx.hideLoading();
        return;
      }

      // 视频处理
      const rawVideoUrl = data.videoUrl || data.video || data.video_url || '';
      let videoUrl = '';
      if (rawVideoUrl) {
        videoUrl = String(rawVideoUrl).startsWith('http') ? String(rawVideoUrl) : this.processImageUrl(String(rawVideoUrl));
      }
      const hasVideo = !!videoUrl;

      const goodsImage = this.processImageUrl(data.image) || '';
      const detailImage = this.processImageUrl(data.detailImage) || '';

      // 轮播图：兼容多种字段名
      const rawSwiper =
        data.swiperList || data.swiperImages || data.swiperImgs ||
        data.carouselMedia || data.carouselList || data.carouselImages ||
        data.bannerList || data.banners || data.slides || [];
      let swiperList = (Array.isArray(rawSwiper) ? rawSwiper : []).map(item => ({
        ...item,
        image: this.processImageUrl(item.image || item.url || item.src || (typeof item === 'string' ? item : ''))
      }));
      if (swiperList.length === 0 && detailImage) {
        swiperList = [
          { id: 1, image: detailImage },
          { id: 2, image: goodsImage }
        ];
      }

      // 规格图/详情图：兼容多种字段名（跳过空数组）
      let rawDetailImages;
      if (data.specImages && data.specImages.length > 0) {
        rawDetailImages = data.specImages;
      } else if (data.detailImages && data.detailImages.length > 0) {
        rawDetailImages = data.detailImages;
      } else if (data.images && data.images.length > 0) {
        rawDetailImages = data.images;
      } else {
        rawDetailImages = data.detail_image || data.detailImgs ||
          data.goodsImages || data.imageList || data.pictures || data.imgList ||
          data.goods_image || data.goodsImg || [];
      }
      let detailImages = [];
      if (Array.isArray(rawDetailImages)) {
        detailImages = rawDetailImages.map(item => {
          if (typeof item === 'string') return item;
          if (item && typeof item === 'object') return item.image || item.url || item.src || '';
          return '';
        });
      } else if (typeof rawDetailImages === 'string') {
        detailImages = rawDetailImages.split(',').filter(Boolean);
      }
      detailImages = detailImages.map(url => this.processImageUrl(url)).filter(Boolean);
      // 如果没取到规格图，兜底用 detailImage + goodsImage
      if (detailImages.length === 0 && detailImage) {
        detailImages = [detailImage, goodsImage].filter(Boolean);
      }

      const isAcre = data.type === 'acre' || data.isAcre;
      this.setData({
        goods: {
          id: data.id || goodsId,
          name: data.name || '',
          price: Number((data.price || 0).toString().replace(/[¥￥]/g, '')),
          image: goodsImage,
          detailImage: detailImage,
          detailImages,  // 规格图数组
          description: data.description || '',
          spec: data.spec || '',
          weight: data.weight || '',
          storage: data.storage || '',
          videoUrl: videoUrl,
          stock: Number(data.stock || 0),
          type: data.type || '',
          isAcre: isAcre
        },
        swiperList: swiperList,
        loading: false,
        hasVideo: hasVideo,
        isFarmGood: isAcre
      });
    })
      .catch((err) => {
        this.setData({ loading: false });
      })
      .finally(() => {
        wx.hideLoading();
      });
  },

  updateCartCount() {
    const cart = wx.getStorageSync('cartList') || {};
    let count = 0;
    for (const key in cart) {
      count += cart[key].count || cart[key].quantity || 0;
    }
    
    const goodsId = String(this.data.goods.id);
    const cartQuantity = cart[goodsId] ? (cart[goodsId].count || cart[goodsId].quantity || 0) : 0;
    
    this.setData({ cartCount: count, cartQuantity: cartQuantity });
  },

  addToCart() {
    const goods = this.data.goods;
    const isFarmGood = this.data.isFarmGood;
    const currentCart = wx.getStorageSync('cartList') || {};
    const targetId = String(goods.id);
    const currentQuantity = currentCart[targetId] ? (currentCart[targetId].count || currentCart[targetId].quantity || 0) : 0;

    if (goods.stock <= 0) {
      wx.showToast({ title: '库存不足', icon: 'none' });
      return;
    }

    if (currentQuantity >= goods.stock) {
      wx.showToast({ title: '库存不足', icon: 'none' });
      return;
    }

    const nextCart = { ...currentCart };

    if (nextCart[targetId]) {
      nextCart[targetId].count = currentQuantity + 1;
      nextCart[targetId].quantity = nextCart[targetId].count;
    } else {
      nextCart[targetId] = {
        id: targetId,
        name: goods.name,
        price: Number(goods.price || 0),
        image: goods.image,
        count: 1,
        quantity: 1,
        checked: true,
        stock: goods.stock,
        isFarmGood: isFarmGood
      };
    }

    wx.setStorageSync('cartList', nextCart);
    this.updateCartCount();
    wx.showToast({
      title: '已加入购物车',
      icon: 'success'
    });
  },

  buyNow() {
    if (this.data.goods.stock <= 0) {
      wx.showToast({ title: '库存不足', icon: 'none' });
      return;
    }

    // 从购物车获取当前数量
    const cart = wx.getStorageSync('cartList') || {};
    const goodsId = String(this.data.goods.id);
    const cartQuantity = cart[goodsId] ? (cart[goodsId].count || cart[goodsId].quantity || 0) : 0;

    // 如果购物车中有该商品，使用购物车数量；否则使用1
    const quantity = cartQuantity > 0 ? cartQuantity : 1;

    this.setData({
      showBuyModal: true,
      quantity: quantity
    }, () => {
      this.calculateTotalPrice();
    });
  },

  hideBuyModal() {
    this.setData({ showBuyModal: false });
  },

  increaseQuantity() {
    const stock = this.data.goods.stock;
    if (this.data.quantity >= stock) {
      wx.showToast({ title: `库存仅剩 ${stock} 件`, icon: 'none' });
      return;
    }
    this.setData({ quantity: this.data.quantity + 1 }, () => {
      this.calculateTotalPrice();
    });
  },

  decreaseQuantity() {
    if (this.data.quantity > 1) {
      this.setData({ quantity: this.data.quantity - 1 }, () => {
        this.calculateTotalPrice();
      });
    }
  },

  calculateTotalPrice() {
    const total = (this.data.goods.price * this.data.quantity).toFixed(2);
    this.setData({ totalPrice: total });
  },

  onQuantityInput(e) {
    let val = e.detail.value;
    if (val === '') return;
    let num = parseInt(val);
    if (isNaN(num)) num = 1;
    const stock = this.data.goods.stock;
    if (num > stock) num = stock;
    this.setData({ quantity: num }, () => {
      this.calculateTotalPrice();
    });
  },

  onQuantityBlur(e) {
    let val = e.detail.value;
    let num = parseInt(val);
    if (isNaN(num) || num < 1) num = 1;
    this.setData({ quantity: num }, () => {
      this.calculateTotalPrice();
    });
  },

  getAddressList() {
    api.user.getAddresses()
    .then(data => {
      const addressList = data || [];
      const defaultAddress = addressList.find(addr => addr.isDefault) || (addressList.length > 0 ? addressList[0] : null);

      // 如果有选中的地址ID，优先使用选中的地址
      let selectedAddressId = this.data.selectedAddress;
      let selectedAddress = null;

      if (selectedAddressId) {
        selectedAddress = addressList.find(addr => addr.id === selectedAddressId);
      }

      // 如果没有选中的地址或选中的地址不存在，使用默认地址
      if (!selectedAddress && defaultAddress) {
        selectedAddressId = defaultAddress.id;
        selectedAddress = defaultAddress;
      }

      this.setData({
        addressList,
        selectedAddress: selectedAddressId,
        defaultAddress: selectedAddress || defaultAddress
      });
    })
    .catch(err => {
      // 用户未登录时不显示错误提示，避免影响商品详情展示
    });
  },

  selectAddress(e) {
    const id = e.currentTarget.dataset.id;
    const address = this.data.addressList.find(addr => addr.id === id);
    this.setData({
      selectedAddress: id,
      defaultAddress: address,
      showAllAddresses: false
    });
  },

  toggleAddressList() {
    this.setData({ showAllAddresses: !this.data.showAllAddresses });
  },

  addAddress() {
    wx.navigateTo({
      url: '/user-pages/address-edit/address-edit'
    });
  },

  editAddress(e) {
    const id = e.currentTarget.dataset.id;
    wx.navigateTo({
      url: `/user-pages/address-edit/address-edit?id=${id}`
    });
  },

  // 跳转到地址选择页面
  goToAddressSelect() {
    wx.navigateTo({
      url: '/user-pages/address/address?from=buy'
    });
  },

  confirmBuy() {
    // 下单前校验库存
    if (this.data.goods.stock <= 0) {
      wx.showToast({ title: '库存不足', icon: 'none' });
      return;
    }
    if (this.data.quantity > this.data.goods.stock) {
      wx.showToast({ title: `库存仅剩 ${this.data.goods.stock} 件`, icon: 'none' });
      return;
    }

    // 保存商品数据到临时存储，跳转确认订单页
    const tempItem = {
      id: String(this.data.goods.id),
      name: this.data.goods.name,
      price: this.data.goods.price,
      image: this.data.goods.image,
      quantity: this.data.quantity,
      stock: this.data.goods.stock
    };
    wx.setStorageSync('tempBuyNowItem', tempItem);

    this.setData({ showBuyModal: false });
    wx.navigateTo({
      url: '/user-pages/confirm-order/confirm-order?type=goods&from=buyNow'
    });
  },

  previewImage(e) {
    const current = e.currentTarget.dataset.url;
    const urls = this.data.swiperList.map(item => item.image);
    wx.previewImage({
      current,
      urls
    });
  },

  previewDetailImages(e) {
    const current = e.currentTarget.dataset.url;
    const urls = (this.data.goods.detailImages || []).filter(Boolean);
    wx.previewImage({
      current,
      urls
    });
  },

  goToCart() {
    wx.switchTab({ url: '/pages/cart/cart' });
  }
});

