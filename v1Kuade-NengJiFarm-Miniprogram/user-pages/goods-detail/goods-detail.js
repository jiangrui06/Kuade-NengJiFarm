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
      weight: '',
      storage: '',
      videoUrl: ''
    },
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
    totalPrice: '0'
  },

  processImageUrl(imageUrl) {
    const utils = require('../../utils/utils');
    return utils.media.processUrl(imageUrl);
  },

  onLoad(options) {
    const goodsId = options.id;

    if (!goodsId) {
      this.setData({ loading: false });
      wx.showToast({
        title: '缺少商品ID',
        icon: 'none'
      });
      return;
    }

    this.getGoodsDetail(goodsId);
    this.updateCartCount();
    this.getAddressList();
  },

  onShow() {
    this.updateCartCount();
    // 重新获取地址列表，确保从地址页面返回时能看到最新的地址
    this.getAddressList();
  },

  getGoodsDetail(goodsId) {
    wx.showLoading({ title: '加载中...' });

    request({
      url: `/api/goods/detail`,
      method: 'GET',
      data: {
        goodsId: goodsId
      }
    })
      .then((data) => {
        // 视频处理：兼容 videoUrl / video / video_url 字段
        const rawVideoUrl = data.videoUrl || data.video || data.video_url || '';
        let videoUrl = '';
        if (rawVideoUrl) {
          videoUrl = String(rawVideoUrl).startsWith('http') ? String(rawVideoUrl) : this.processImageUrl(String(rawVideoUrl));
        }
        const hasVideo = !!videoUrl;
        
        console.log('商品详情原始数据:', JSON.stringify(data));
        
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
        this.setData({
          goods: {
            id: data.id || goodsId,
            name: data.name || '',
            price: Number((data.price || 0).toString().replace(/[¥￥]/g, '')),
            image: goodsImage,
            detailImage: detailImage,
            description: data.description || '',
            weight: data.weight || '',
            storage: data.storage || '',
            videoUrl: videoUrl
          },
          swiperList: swiperList,
          loading: false,
          hasVideo: hasVideo
        });
      })
      .catch((err) => {
        console.error('获取商品详情失败:', err);
        this.setData({ loading: false });
      })
      .finally(() => {
        wx.hideLoading();
      });
  },

  updateCartCount() {
    const cartList = wx.getStorageSync('cartList') || [];
    const count = cartList.reduce((sum, item) => sum + (item.count || 0), 0);
    this.setData({ cartCount: count });
  },

  addToCart() {
    const goods = this.data.goods;
    const currentCart = wx.getStorageSync('cartList') || [];
    const nextCart = currentCart.map(item => ({ ...item }));
    const targetId = String(goods.id);
    const existingIndex = nextCart.findIndex(item => String(item.id) === targetId);

    if (existingIndex > -1) {
      nextCart[existingIndex].count = (nextCart[existingIndex].count || 0) + 1;
    } else {
      nextCart.push({
        id: targetId,
        name: goods.name,
        price: Number(goods.price || 0),
        image: goods.image,
        count: 1,
        checked: true
      });
    }

    wx.setStorageSync('cartList', nextCart);
    this.updateCartCount();
    wx.showToast({
      title: '已加入购物车',
      icon: 'success'
    });
  },

  buyNow() {
    this.setData({ showBuyModal: true });
    this.calculateTotalPrice();
  },

  hideBuyModal() {
    this.setData({ showBuyModal: false });
  },

  increaseQuantity() {
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
    request({
      url: '/api/user/address',
      method: 'GET'
    })
    .then(data => {
      const addressList = data || [];
      const defaultAddress = addressList.find(addr => addr.isDefault) || (addressList.length > 0 ? addressList[0] : null);
      this.setData({
        addressList,
        selectedAddress: defaultAddress ? defaultAddress.id : null,
        defaultAddress
      });
    })
    .catch(err => {
      console.error('获取地址列表失败:', err);
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

  confirmBuy() {
    if (!this.data.selectedAddress) {
      wx.showToast({ title: '请选择地址', icon: 'none' });
      return;
    }

    const payload = {
      addressId: this.data.selectedAddress,
      items: [{
        id: parseInt(this.data.goods.id),
        price: this.data.goods.price,
        quantity: this.data.quantity
      }]
    };

    wx.showLoading({ title: '创建订单中...' });
    api.order.createCommodity(payload)
    .then(data => {
      wx.hideLoading();
      const orderId = data.orderId || data.id;
      wx.navigateTo({
        url: `/user-pages/pay/pay?orderId=${orderId}&totalPrice=${this.data.totalPrice}`
      });
    })
    .catch(err => {
      wx.hideLoading();
      // 这里的错误提示已经在 request 封装里处理了
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
    const urls = [this.data.goods.detailImage, this.data.goods.image].filter(Boolean);
    wx.previewImage({
      current,
      urls
    });
  },

  goToCart() {
    wx.switchTab({ url: '/pages/cart/cart' });
  }
});

