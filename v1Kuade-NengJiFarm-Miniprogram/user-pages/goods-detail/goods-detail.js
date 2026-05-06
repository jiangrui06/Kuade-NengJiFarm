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
            videoUrl: videoUrl,
            stock: Number(data.stock || 0)
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
    const currentCart = wx.getStorageSync('cartList') || {};
    const targetId = String(goods.id);
    const currentQuantity = currentCart[targetId] ? (currentCart[targetId].count || currentCart[targetId].quantity || 0) : 0;

    if (goods.stock > 0 && currentQuantity >= goods.stock) {
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
        stock: goods.stock
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
      method: 'GET',
      showLoading: false
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
    api.order.createCommodityV2(payload)
    .then(data => {
      wx.hideLoading();
      const orderId = data.orderId || data.id;
      wx.showToast({ title: '订单创建成功', icon: 'success' });
      // 关闭购买弹窗
      this.setData({ showBuyModal: false });
      // 延迟跳转到订单列表页面
      setTimeout(() => {
        wx.navigateTo({ url: '/user-pages/orders/orders?tab=pending' });
      }, 1500);
    })
    .catch(err => {
      wx.hideLoading();
      console.error('创建订单失败:', err);
      wx.showToast({ title: '创建订单失败', icon: 'none' });
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

