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
    cartCount: 0
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
  },

  onShow() {
    this.updateCartCount();
  },

  getGoodsDetail(goodsId) {
    wx.showLoading({ title: '加载中...' });

    api.request({
      url: `/api/DemoApi/goods/${goodsId}`,
      method: 'GET'
    })
      .then((data) => {
        this.setData({
          goods: {
            id: data.id || goodsId,
            name: data.name || '',
            price: Number(data.price || 0),
            image: data.image || '',
            detailImage: data.detailImage || data.image || '',
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
      url: '/api/AppCart/Appcart',
      method: 'POST',
      data: {
        cartList: nextCart
      }
    })
      .then((data) => {
        const cartList = (data.cartList || []).map(item => ({
          ...item,
          checked: !!item.checked
        }));

        wx.setStorageSync('cartList', cartList);
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
    wx.navigateTo({
      url: '../buy/buy',
      success: function(res) {
        console.log("跳转成功");
      },
      fail: function(res) {
        console.log("跳转失败", res);
      }
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