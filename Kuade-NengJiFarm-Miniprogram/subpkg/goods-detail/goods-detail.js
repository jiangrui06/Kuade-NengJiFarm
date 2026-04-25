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
    if (!imageUrl) return '';
    const cleaned = String(imageUrl).replace(/[`\s]/g, '');
    if (cleaned.startsWith('http://') || cleaned.startsWith('https://')) {
      return cleaned;
    }
    return 'http://192.168.101.47' + cleaned;
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
        // 视频处理：优先用后端返回的 videoUrl，没有则不显示
        let videoUrl = '';
        if (data.videoUrl) {
          videoUrl = String(data.videoUrl).startsWith('http') ? data.videoUrl : this.processImageUrl(data.videoUrl);
        }
        const hasVideo = !!videoUrl;
        
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

  addToCart() {
    const goods = this.data.goods;
    const currentCart = wx.getStorageSync('cartList') || [];
    const nextCart = currentCart.map(item => ({ ...item }));
    const targetId = String(goods.id);
    const existingIndex = nextCart.findIndex(item => String(item.id) === targetId || String(item.goodsId) === targetId);

    if (existingIndex > -1) {
      nextCart[existingIndex].count = (nextCart[existingIndex].count || 0) + 1;
      nextCart[existingIndex].quantity = (nextCart[existingIndex].quantity || 0) + 1;
      nextCart[existingIndex].checked = true;
    } else {
      nextCart.push({
        id: targetId,
        goodsId: targetId,
        name: goods.name,
        price: Number(goods.price || 0),
        image: goods.image,
        tag: goods.tag || '',
        count: 1,
        quantity: 1,
        checked: true
      });
    }

    wx.showLoading({ title: '加入中...' });

    api.cart.add({
      goodsId: goods.id,
      count: 1
    })
      .then((data) => {
        // 即使API返回的数据结构不同，也要更新本地购物车
        wx.setStorageSync('cartList', nextCart);
        this.updateCartCount();
        wx.showToast({
          title: '已加入购物车',
          icon: 'success'
        });
      })
      .catch((err) => {
        console.error('加入购物车失败:', err);
      })
      .finally(() => {
        wx.hideLoading();
      });
  },

  buyNow() {
    // 从本地存储读取购物车数据，获取当前商品的数量
    const cartList = wx.getStorageSync('cartList') || [];
    const goodsId = String(this.data.goods.id);
    const cartItem = cartList.find(item => String(item.id) === goodsId || String(item.goodsId) === goodsId);
    const quantity = cartItem ? (cartItem.quantity || cartItem.count || 1) : 1;
    
    const total = this.calculateTotalPrice(quantity);
    this.setData({ 
      showBuyModal: true,
      quantity: quantity,
      totalPrice: total
    });
  },

  increaseQuantity() {
    this.setData({ quantity: this.data.quantity + 1 }, () => {
      const total = this.calculateTotalPrice();
      this.setData({ totalPrice: total });
      // 更新购物车数据
      this.updateCartData();
    });
  },

  decreaseQuantity() {
    if (this.data.quantity > 1) {
      this.setData({ quantity: this.data.quantity - 1 }, () => {
        const total = this.calculateTotalPrice();
        this.setData({ totalPrice: total });
        // 更新购物车数据
        this.updateCartData();
      });
    }
  },

  onQuantityInput(e) {
    let val = parseInt(e.detail.value, 10);
    if (isNaN(val) || val < 1) val = 1;
    if (val > 9999) val = 9999;
    // 只更新显示值，不触发总价/购物车计算（等 blur 再算）
    this.setData({ quantity: val });
  },

  onQuantityBlur() {
    const qty = Math.max(1, this.data.quantity);
    this.setData({ quantity: qty }, () => {
      const total = this.calculateTotalPrice();
      this.setData({ totalPrice: total });
      this.updateCartData();
    });
  },

  updateCartData() {
    const cartList = wx.getStorageSync('cartList') || [];
    const goodsId = String(this.data.goods.id);
    const existingIndex = cartList.findIndex(item => String(item.id) === goodsId || String(item.goodsId) === goodsId);
    
    const updatedCartList = [...cartList];
    
    if (existingIndex > -1) {
      updatedCartList[existingIndex] = {
        ...updatedCartList[existingIndex],
        id: goodsId,
        goodsId: goodsId,
        name: this.data.goods.name,
        price: this.data.goods.price,
        image: this.data.goods.image,
        quantity: this.data.quantity,
        count: this.data.quantity
      };
    } else {
      updatedCartList.push({
        id: goodsId,
        goodsId: goodsId,
        name: this.data.goods.name,
        price: this.data.goods.price,
        image: this.data.goods.image,
        quantity: this.data.quantity,
        count: this.data.quantity,
        checked: true
      });
    }
    
    try {
      wx.setStorageSync('cartList', updatedCartList);
      // 更新购物车图标上的数字
      this.updateCartCount();
    } catch (e) {
      console.error('更新购物车失败:', e);
    }
  },

  calculateTotalPrice(quantity) {
    const qty = quantity || this.data.quantity;
    const total = this.data.goods.price * qty;
    let formatted = total.toFixed(3);
    // 移除末尾的0
    formatted = formatted.replace(/\.?0+$/, '');
    return formatted;
  },

  hideBuyModal() {
    this.setData({ showBuyModal: false });
  },

  getAddressList() {
    request({
      url: '/api/address/list',
      method: 'GET'
    }).then((data) => {
      // 处理响应数据 - 直接返回地址数组
      const addressList = Array.isArray(data) ? data : [];
      
      // 为每个地址添加id属性（如果不存在）并处理地址格式
      const processedAddressList = addressList.map((address, index) => ({
        id: address.id || String(index + 1),
        name: address.name || '',
        phone: address.phone || '',
        address: address.address || '',
        isDefault: address.isDefault || false
      }));
      
      // 找到默认地址
      const defaultAddress = processedAddressList.find(item => item.isDefault) || (processedAddressList.length > 0 ? processedAddressList[0] : null);
      const selectedAddressId = defaultAddress ? defaultAddress.id : null;
      
      this.setData({
        addressList: processedAddressList,
        selectedAddress: selectedAddressId,
        defaultAddress: defaultAddress
      });
    }).catch((err) => {
      console.error('获取地址列表失败:', err);
      // 不使用默认地址，显示空地址状态
      this.setData({
        addressList: [],
        selectedAddress: null,
        defaultAddress: null
      });
    });
  },

  selectAddress(e) {
    const addressId = e.currentTarget.dataset.id;
    this.setData({ selectedAddress: addressId });
    // 选择地址后，更新默认地址为选中的地址
    const selectedAddressInfo = this.data.addressList.find(item => item.id === addressId);
    if (selectedAddressInfo) {
      this.setData({ defaultAddress: selectedAddressInfo });
    }
    // 选择地址后，隐藏地址列表
    this.setData({ showAllAddresses: false });
  },

  toggleAddressList() {
    this.setData({ showAllAddresses: !this.data.showAllAddresses });
  },

  addAddress() {
    wx.navigateTo({
      url: '/subpkg/address/address?from=buy'
    });
  },

  editAddress(e) {
    const addressId = e.currentTarget.dataset.id;
    wx.navigateTo({
      url: `/subpkg/address-edit/address-edit?id=${addressId}`
    });
  },

  confirmBuy() {
    if (!this.data.selectedAddress) {
      wx.showToast({
        title: '请选择收货地址',
        icon: 'none'
      });
      return;
    }

    const selectedAddressInfo = this.data.addressList.find(
      item => item.id === this.data.selectedAddress
    );

    if (!selectedAddressInfo) {
      wx.showToast({
        title: '地址信息错误',
        icon: 'none'
      });
      return;
    }

    wx.showLoading({ title: '提交订单中...' });

    request({
      url: '/api/order/create',
      method: 'POST',
      data: {
        goodsId: this.data.goods.id,
        goodsName: this.data.goods.name,
        price: this.data.goods.price,
        quantity: this.data.quantity,
        addressId: this.data.selectedAddress
      }
    }).then((data) => {
      wx.hideLoading();
      this.setData({ showBuyModal: false });
      wx.showToast({
        title: '订单创建成功',
        icon: 'success'
      });
      setTimeout(() => {
        wx.navigateTo({
          url: '/subpkg/orders/orders'
        });
      }, 1500);
    }).catch((err) => {
      wx.hideLoading();
      console.error('创建订单失败:', err);
      wx.showToast({
        title: '订单创建失败',
        icon: 'none'
      });
    });
  },

  updateCartCount() {
    const cartList = wx.getStorageSync('cartList') || [];
    let totalCount = 0;
    cartList.forEach(item => {
      totalCount += item.count || item.quantity || 0;
    });
    this.setData({ cartCount: totalCount });
  },

  goToCart() {
    wx.switchTab({
      url: '/pages/cart/cart'
    });
  },

  navigateToService() {
    wx.showModal({
      title: '能记家庭农场客服',
      content: '手机号：15876534944\n     微信号：njjtnc15876534944',
      showCancel: false
    });
  },

  // 预览轮播图
  previewImage(e) {
    const url = e.currentTarget.dataset.url;
    if (url) {
      wx.previewImage({
        current: url,
        urls: [url]
      });
    }
  },

  // 预览详情图片列表
  previewDetailImages(e) {
    const { goods, swiperList } = this.data;
    // 组合详情图 URL 列表
    const imageList = [];
    if (goods.detailImage) imageList.push(goods.detailImage);
    if (goods.image && goods.image !== goods.detailImage) imageList.push(goods.image);

    if (imageList.length === 0 && swiperList.length > 0) {
      swiperList.forEach(item => { if (item.image) imageList.push(item.image); });
    }

    if (imageList.length === 0) return;

    const currentUrl = e.currentTarget.dataset.url || imageList[0];
    wx.previewImage({
      current: currentUrl,
      urls: imageList
    });
  }
});