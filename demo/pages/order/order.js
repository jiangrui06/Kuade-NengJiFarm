Page({
  data: {
    // 当前激活的分类
    activeCategory: 'vegetables',
    // 分类列表
    categories: [],
    // 商品列表
    goodsList: {},
    // 购物车
    cart: {},
    // 购物车商品数组（用于显示）
    cartItems: [],
    // 购物车商品数量
    cartCount: 0,
    // 总价格
    totalPrice: 0,
    // 加载状态
    loading: true,
    // 购物车弹窗显示状态
    showCartModal: false
  },

  onLoad: function (options) {
    // 页面加载时获取点餐数据
    this.getOrderData();
  },

  // 获取点餐数据
  getOrderData: function() {
    wx.showLoading({ title: '加载中...' });
    
    // 模拟API调用
    setTimeout(() => {
      // 模拟API返回的数据
      const data = {
        categories: [
          { id: 'vegetables', name: '新鲜蔬菜' },
          { id: 'meat', name: '肉类产品' },
          { id: 'eggs', name: '禽蛋产品' },
          { id: 'dairy', name: '乳制品' },
          { id: 'staple', name: '主食' }
        ],
        goodsList: {
          vegetables: [
            {
              id: 1,
              name: '有机生菜',
              image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20organic%20lettuce&image_size=square',
              price: 30,
              sold: 150,
              stock: 30
            },
            {
              id: 2,
              name: '农家西红柿',
              image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20tomatoes&image_size=square',
              price: 30,
              sold: 200,
              stock: 30
            },
            {
              id: 3,
              name: '新鲜黄瓜',
              image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20cucumbers&image_size=square',
              price: 30,
              sold: 180,
              stock: 30
            }
          ],
          meat: [
            {
              id: 4,
              name: '土猪肉',
              image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20pork%20meat&image_size=square',
              price: 30,
              sold: 100,
              stock: 30
            },
            {
              id: 5,
              name: '农家土鸡',
              image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20chicken&image_size=square',
              price: 30,
              sold: 80,
              stock: 30
            }
          ],
          eggs: [
            {
              id: 6,
              name: '土鸡蛋',
              image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20eggs&image_size=square',
              price: 30,
              sold: 300,
              stock: 30
            },
            {
              id: 7,
              name: '鸭蛋',
              image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20duck%20eggs&image_size=square',
              price: 30,
              sold: 150,
              stock: 30
            },
            {
              id: 8,
              name: '鹅蛋',
              image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20goose%20eggs&image_size=square',
              price: 30,
              sold: 50,
              stock: 30
            }
          ],
          dairy: [
            {
              id: 9,
              name: '新鲜牛奶',
              image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20milk&image_size=square',
              price: 30,
              sold: 200,
              stock: 30
            },
            {
              id: 10,
              name: '农家酸奶',
              image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=homemade%20yogurt&image_size=square',
              price: 30,
              sold: 180,
              stock: 30
            }
          ],
          staple: [
            {
              id: 11,
              name: '农家大米',
              image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20rice&image_size=square',
              price: 30,
              sold: 250,
              stock: 30
            },
            {
              id: 12,
              name: '手工面条',
              image: 'https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=homemade%20noodles&image_size=square',
              price: 30,
              sold: 150,
              stock: 30
            }
          ]
        }
      };
      
      // 更新数据
      this.setData({
        categories: data.categories,
        goodsList: data.goodsList,
        loading: false
      });
      
      wx.hideLoading();
    }, 1000);
  },

  // 切换分类
  switchCategory: function(e) {
    const category = e.currentTarget.dataset.category;
    this.setData({
      activeCategory: category
    });
  },

  // 添加商品到购物车
  addToCart: function(e) {
    const { category, index } = e.currentTarget.dataset;
    const goods = this.data.goodsList[category][index];
    
    // 检查库存
    if (goods.stock <= 0) {
      wx.showToast({
        title: '商品已售罄',
        icon: 'none'
      });
      return;
    }
    
    // 更新库存
    const newGoodsList = JSON.parse(JSON.stringify(this.data.goodsList));
    newGoodsList[category][index].stock -= 1;
    newGoodsList[category][index].sold += 1;
    
    // 更新购物车
    const newCart = JSON.parse(JSON.stringify(this.data.cart));
    if (newCart[goods.id]) {
      newCart[goods.id].quantity += 1;
    } else {
      newCart[goods.id] = {
        ...goods,
        quantity: 1
      };
    }
    
    // 计算购物车数量和总价格
    let cartCount = 0;
    let totalPrice = 0;
    for (const item in newCart) {
      cartCount += newCart[item].quantity;
      totalPrice += newCart[item].price * newCart[item].quantity;
    }
    
    // 将购物车对象转换为数组（用于显示）
    const cartItems = Object.values(newCart);
    
    // 更新数据
    this.setData({
      goodsList: newGoodsList,
      cart: newCart,
      cartItems: cartItems,
      cartCount: cartCount,
      totalPrice: totalPrice
    });
  },

  // 查看购物车详情
  viewCart: function() {
    if (this.data.cartCount === 0) {
      wx.showToast({
        title: '购物车为空',
        icon: 'none'
      });
      return;
    }
    
    // 确保cartItems是最新的
    const cartItems = Object.values(this.data.cart);
    
    // 显示购物车详情弹窗
    this.setData({
      cartItems: cartItems,
      showCartModal: true
    });
  },

  // 隐藏购物车详情弹窗
  hideCartModal: function() {
    this.setData({
      showCartModal: false
    });
  },

  // 增加商品数量
  increaseQuantity: function(e) {
    const goodsId = e.currentTarget.dataset.id;
    const cart = this.data.cart;
    const goods = cart[goodsId];
    
    // 检查库存
    let stockAvailable = false;
    let categoryFound = '';
    let indexFound = -1;
    
    for (const category in this.data.goodsList) {
      const categoryGoods = this.data.goodsList[category];
      for (let i = 0; i < categoryGoods.length; i++) {
        if (categoryGoods[i].id === goodsId) {
          if (categoryGoods[i].stock > 0) {
            stockAvailable = true;
          }
          categoryFound = category;
          indexFound = i;
          break;
        }
      }
      if (stockAvailable) break;
    }
    
    if (!stockAvailable) {
      wx.showToast({
        title: '商品已售罄',
        icon: 'none'
      });
      return;
    }
    
    // 更新库存
    const newGoodsList = JSON.parse(JSON.stringify(this.data.goodsList));
    for (const category in newGoodsList) {
      const categoryGoods = newGoodsList[category];
      for (let i = 0; i < categoryGoods.length; i++) {
        if (categoryGoods[i].id === goodsId) {
          categoryGoods[i].stock -= 1;
          categoryGoods[i].sold += 1;
          break;
        }
      }
    }
    
    // 更新购物车
    const newCart = JSON.parse(JSON.stringify(this.data.cart));
    newCart[goodsId].quantity += 1;
    
    // 计算购物车数量和总价格
    let cartCount = 0;
    let totalPrice = 0;
    for (const item in newCart) {
      cartCount += newCart[item].quantity;
      totalPrice += newCart[item].price * newCart[item].quantity;
    }
    
    // 将购物车对象转换为数组（用于显示）
    const cartItems = Object.values(newCart);
    
    // 更新数据
    this.setData({
      goodsList: newGoodsList,
      cart: newCart,
      cartItems: cartItems,
      cartCount: cartCount,
      totalPrice: totalPrice
    });
  },

  // 减少商品数量
  decreaseQuantity: function(e) {
    const goodsId = e.currentTarget.dataset.id;
    const cart = this.data.cart;
    const goods = cart[goodsId];
    
    // 更新库存
    const newGoodsList = JSON.parse(JSON.stringify(this.data.goodsList));
    for (const category in newGoodsList) {
      const categoryGoods = newGoodsList[category];
      for (let i = 0; i < categoryGoods.length; i++) {
        if (categoryGoods[i].id === goodsId) {
          categoryGoods[i].stock += 1;
          categoryGoods[i].sold -= 1;
          break;
        }
      }
    }
    
    // 更新购物车
    const newCart = JSON.parse(JSON.stringify(this.data.cart));
    if (goods.quantity <= 1) {
      // 从购物车中移除
      delete newCart[goodsId];
    } else {
      // 减少数量
      newCart[goodsId].quantity -= 1;
    }
    
    // 计算购物车数量和总价格
    let cartCount = 0;
    let totalPrice = 0;
    for (const item in newCart) {
      cartCount += newCart[item].quantity;
      totalPrice += newCart[item].price * newCart[item].quantity;
    }
    
    // 将购物车对象转换为数组（用于显示）
    const cartItems = Object.values(newCart);
    
    // 更新数据
    this.setData({
      goodsList: newGoodsList,
      cart: newCart,
      cartItems: cartItems,
      cartCount: cartCount,
      totalPrice: totalPrice
    });
  },

  // 结算
  checkout: function() {
    if (this.data.cartCount === 0) {
      wx.showToast({
        title: '购物车为空',
        icon: 'none'
      });
      return;
    }
    
    // 跳转到购物车页面
    wx.switchTab({
      url: '/pages/cart/cart'
    });
  },

  // 跳转到商品详情页面
  navigateToGoodsDetail: function(e) {
    const goodsId = e.currentTarget.dataset.id;
    wx.navigateTo({
      url: `/pages/goods-detail/goods-detail?id=${goodsId}`
    });
  }
});