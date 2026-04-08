const api = require('../../utils/api');

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
      storage: ''
    },
    loading: true,
    cartCount: 0,
    showBuyModal: false,
    addressList: [],
    selectedAddress: null
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
    // 重新获取地址列表，确保从地址编辑页面返回时能看到最新的地址
    this.getAddressList();
  },

  getGoodsDetail(goodsId) {
    wx.showLoading({ title: '加载中...' });

    api.request({
      url: `/api/goods/${goodsId}`,
      method: 'GET'
    })
      .then((data) => {
        // 将图片链接从HTTP改为HTTPS
        let image = data.image || '';
        let detailImage = data.detailImage || data.image || '';
        
        if (image.startsWith('http://')) {
          image = image.replace('http://', 'https://');
        }
        
        if (detailImage.startsWith('http://')) {
          detailImage = detailImage.replace('http://', 'https://');
        }
        
        this.setData({
          goods: {
            id: data.id || goodsId,
            name: data.name || '',
            price: Number(data.price || 0),
            image: image,
            detailImage: detailImage,
            description: data.description || '',
            weight: data.weight || '',
            storage: data.storage || ''
          },
          loading: false
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
    const existingIndex = nextCart.findIndex(item => String(item.id) === targetId);

    if (existingIndex > -1) {
      nextCart[existingIndex].count += 1;
      nextCart[existingIndex].checked = true;
    } else {
      nextCart.push({
        id: targetId,
        name: goods.name,
        price: Number(goods.price || 0),
        image: goods.image,
        tag: goods.tag || '',
        count: 1,
        checked: true
      });
    }

    wx.showLoading({ title: '加入中...' });

    api.request({
      url: '/api/cart',
      method: 'POST',
      data: {
        goodsId: goods.id,
        quantity: 1
      }
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
        // 即使 API 调用失败，也将商品添加到本地购物车
        wx.setStorageSync('cartList', nextCart);
        this.updateCartCount();
        wx.showToast({
          title: '已加入购物车',
          icon: 'success'
        });
      })
      .finally(() => {
        wx.hideLoading();
      });
  },

  buyNow() {
    this.setData({ showBuyModal: true });
  },

  hideBuyModal() {
    this.setData({ showBuyModal: false });
  },

  getAddressList() {
    api.request({
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
      const defaultAddress = processedAddressList.find(item => item.isDefault);
      const selectedAddressId = defaultAddress ? defaultAddress.id : (processedAddressList.length > 0 ? processedAddressList[0].id : null);
      
      this.setData({
        addressList: processedAddressList,
        selectedAddress: selectedAddressId
      });
    }).catch((err) => {
      console.error('获取地址列表失败:', err);
      // 使用默认地址
      this.setData({
        addressList: [
          {
            id: '1',
            name: '张三',
            phone: '13800138000',
            address: '北京市朝阳区某某街道123号',
            isDefault: true
          }
        ],
        selectedAddress: '1'
      });
    });
  },

  selectAddress(e) {
    const addressId = e.currentTarget.dataset.id;
    this.setData({ selectedAddress: addressId });
  },

  addAddress() {
    wx.navigateTo({
      url: '/subpkg/address-edit/address-edit'
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

    api.request({
      url: '/api/order/create',
      method: 'POST',
      data: {
        goodsId: this.data.goods.id,
        goodsName: this.data.goods.name,
        price: this.data.goods.price,
        quantity: 1,
        address: selectedAddressInfo
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
      totalCount += item.count || 0;
    });
    this.setData({ cartCount: totalCount });
  },

  goToCart() {
    wx.switchTab({
      url: '/pages/cart/cart'
    });
  }
});