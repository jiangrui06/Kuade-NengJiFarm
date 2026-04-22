const api = require('../../utils/api');

Page({
  data: {
    activeCategory: 'vegetables',
    categories: [],
    goodsList: {},
    pageMap: {},
    hasMoreMap: {},
    pageSize: 6,

    cart: {},
    cartItems: [],
    cartCount: 0,
    totalPrice: 0,

    loading: true,
    lazyLoading: false,
    showCartModal: false,

    tableNumber: null,
    showTableModal: false,
    tableList: []
  },

  onLoad(options) {
     // 检查是否通过扫码进入，处理URL参数
     if (options.tableId && options.secret) {
       // 绑定桌台号码
       const tableNumber = options.tableId;
       this.setData({ tableNumber });
       wx.setStorageSync('tableNumber', tableNumber);
       // 延迟显示提示，确保能够正常显示
       setTimeout(() => {
         wx.showToast({ title: `已绑定桌台 ${tableNumber}`, icon: 'success', duration: 2000 });
       }, 100);
     } else {
       // 恢复购物车
       const cart = wx.getStorageSync('orderCart') || {};
       this.restoreCart(cart);
       // 获取桌台
       const storedTableNumber = wx.getStorageSync('tableNumber');
       if (storedTableNumber) {
         this.setData({ tableNumber: storedTableNumber });
       }
     }
 
    try {
      const cart = wx.getStorageSync('orderCart') || {};
      this.restoreCart(cart);
    } catch (e) {}
    this.getTableList();
    // 延迟加载数据，确保提示能够先显示
    setTimeout(() => {
      this.getOrderData();
    }, 500);
  },

  onBackPress() {
    wx.switchTab({ url: '/pages/index/index' });
    return true;
  },

  onShow() {
    // 页面显示时更新购物车数据和桌台号码
    try {
      const cart = wx.getStorageSync('orderCart') || {};
      this.restoreCart(cart);
      const tableNumber = wx.getStorageSync('tableNumber');
      if (tableNumber) {
        this.setData({ tableNumber });
      }
      
      // 从购物车同步food类型商品数据
      this.syncFromCart();
    } catch (e) {}
  },

  syncFromCart() {
    try {
      const cartList = wx.getStorageSync('cartList') || [];
      const foodItems = cartList.filter(item => item.type === 'food');
      
      if (foodItems.length === 0) return;
      
      const newCart = { ...this.data.cart };
      
      foodItems.forEach(item => {
        const key = String(item.id);
        if (item.count > 0) {
          newCart[key] = {
            ...item,
            quantity: item.count,
            price: parseFloat(item.price)
          };
        } else {
          delete newCart[key];
        }
      });
      
      this.syncCartState(newCart);
    } catch (e) {
      console.error('从购物车同步数据失败:', e);
    }
  },

  getOrderData() {
    wx.showLoading({ title: '加载中...' })
    api.request({
      url: '/api/order',
      method: 'GET',
      data: {
        categoryId: this.data.activeCategory,
        page: 1,
        pageSize: this.data.pageSize
      }
    }).then(data => {
      console.log("后端返回:", data)
      const categories = data.categories || []
      const currentCategory = data.currentCategory || 'vegetables'
      let goods = data.goodsList || []

      // 为餐品添加图片URL
      goods = this.addImageUrlsToGoods(goods)

      this.setData({
        activeCategory: currentCategory,
        categories: categories,
        goodsList: {
          [currentCategory]: goods
        },
        pageMap: {
          [currentCategory]: 1
        },
        hasMoreMap: {
          [currentCategory]: !!data.hasMore
        },
        loading: false
      })
    }).catch(err => {
      console.error("加载失败", err)
      this.setData({ loading: false })
      wx.showToast({ title: '加载失败', icon: 'none' })
    }).finally(() => wx.hideLoading())
  },

  // 为餐品添加图片URL
  addImageUrlsToGoods(goods) {
    // 直接使用API返回的图片URL
    return goods.map((item) => {
      return {
        ...item,
        price: item.price ? item.price.toString().replace(/[¥￥]/g, '') : item.price
      }
    })
  },

  switchCategory(e) {
    const category = e.currentTarget.dataset.category
    if (category === this.data.activeCategory) return
    this.setData({ activeCategory: category })
    if (this.data.goodsList[category]) return
    this.loadCategoryGoods(category, false)
  },

  loadCategoryGoods(category, isLoadMore) {
    if (isLoadMore && this.data.lazyLoading) return
    const nextPage = isLoadMore ? (this.data.pageMap[category] || 0) + 1 : 1
    this.setData({ loading: !isLoadMore, lazyLoading: isLoadMore })

    setTimeout(() => {
      api.request({
        url: '/api/order',
        method: 'GET',
        data: { categoryId: category, page: nextPage, pageSize: this.data.pageSize }
      }).then(data => {
        let newGoods = data.goodsList || []
        
        // 为新餐品添加图片URL
        newGoods = this.addImageUrlsToGoods(newGoods)
        
        const oldGoods = isLoadMore ? (this.data.goodsList[category] || []) : []
        this.setData({
          [`goodsList.${category}`]: oldGoods.concat(newGoods),
          [`pageMap.${category}`]: nextPage,
          [`hasMoreMap.${category}`]: !!data.hasMore,
          loading: false,
          lazyLoading: false
        })
      }).catch(() => {
        this.setData({ loading: false, lazyLoading: false })
      })
    }, 200)
  },

  addToCart(e) {
    const { category, index } = e.currentTarget.dataset
    const goods = this.data.goodsList[category][index]
    if (!goods) return
    const key = String(goods.id)
    const newCart = { ...this.data.cart }

    if (newCart[key]) {
      if (newCart[key].quantity >= goods.stock) {
        wx.showToast({ title: '库存不足', icon: 'none' })
        return
      }
      newCart[key].quantity += 1
    } else {
      newCart[key] = { ...goods, quantity: 1 }
    }
    this.syncCartState(newCart)
  },

  increaseQuantity(e) {
    const id = e.currentTarget.dataset.id + ''
    const newCart = { ...this.data.cart }
    if (!newCart[id]) return
    if (newCart[id].quantity >= newCart[id].stock) {
      wx.showToast({ title: '库存不足', icon: 'none' })
      return
    }
    newCart[id].quantity += 1
    this.syncCartState(newCart)
  },

  decreaseQuantity(e) {
    const id = e.currentTarget.dataset.id + ''
    const newCart = { ...this.data.cart }
    if (!newCart[id]) return
    if (newCart[id].quantity <= 1) {
      delete newCart[id]
    } else {
      newCart[id].quantity -= 1
    }
    this.syncCartState(newCart)
  },

  syncCartState(newCart) {
    let count = 0, total = 0
    Object.values(newCart).forEach(item => {
      count += item.quantity
      total += item.price * item.quantity
    })
    this.setData({
      cart: newCart,
      cartItems: Object.values(newCart),
      cartCount: count,
      totalPrice: parseFloat(total.toFixed(2))
    })
    try { wx.setStorageSync('orderCart', newCart) } catch (e) {}
    
    // 同步更新cart页面的cartList
    const cartList = wx.getStorageSync('cartList') || [];
    const newCartList = [...cartList];
    
    // 更新或添加food类型商品到cartList
    Object.values(newCart).forEach(cartItem => {
      const existingIndex = newCartList.findIndex(
        item => String(item.id) === String(cartItem.id) && item.type === 'food'
      );
      
      if (existingIndex >= 0) {
        // 更新已存在的商品数量
        newCartList[existingIndex].count = cartItem.quantity;
        newCartList[existingIndex].price = parseFloat(cartItem.price);
        newCartList[existingIndex].name = cartItem.name;
        newCartList[existingIndex].image = cartItem.image;
        newCartList[existingIndex].stock = cartItem.stock;
      } else {
        // 添加新商品
        newCartList.push({
          id: String(cartItem.id),
          name: cartItem.name,
          price: parseFloat(cartItem.price),
          image: cartItem.image,
          count: cartItem.quantity,
          stock: cartItem.stock,
          type: 'food',
          checked: false
        });
      }
    });
    
    // 移除已删除的food类型商品
    const filteredCartList = newCartList.filter(item => {
      if (item.type !== 'food') return true;
      const cartKey = String(item.id);
      return newCart[cartKey] && newCart[cartKey].quantity > 0;
    });
    
    wx.setStorageSync('cartList', filteredCartList);
  },

  restoreCart(cart) {
    let count = 0, total = 0
    Object.values(cart || {}).forEach(item => {
      const qty = parseInt(item.quantity) || parseInt(item.count) || 0
      const price = parseFloat(item.price) || 0
      count += qty
      total += price * qty
    })
    this.setData({
      cart: cart || {},
      cartItems: Object.values(cart || {}),
      cartCount: count,
      totalPrice: parseFloat(total.toFixed(2))
    })
  },

  viewCart() {
    if (this.data.cartCount === 0) {
      wx.showToast({ title: '购物车为空', icon: 'none' })
      return
    }
    this.setData({ showCartModal: true })
  },

  hideCartModal() {
    this.setData({ showCartModal: false })
  },

  checkout() {
    if (this.data.cartCount === 0) {
      wx.showToast({ title: '购物车为空', icon: 'none' })
      return
    }
    if (!this.data.tableNumber) {
      wx.showToast({ title: '请选择桌台号码', icon: 'none' })
      return
    }
    wx.navigateTo({ url: '/subpkg/confirm-order/confirm-order' })
  },

  navigateToGoodsDetail(e) {
    const id = e.currentTarget.dataset.id
    const goods = this.findGoodsById(id)
    if (goods) {
      // 将商品数据传递到详情页，确保已售和库存数据一致
      const params = encodeURIComponent(JSON.stringify({
        id: goods.id,
        sold: goods.sold,
        stock: goods.stock
      }))
      wx.navigateTo({ url: `/subpkg/order-foods-detail/order-foods-detail?params=${params}` })
    }
  },

  findGoodsById(id) {
    const { goodsList, activeCategory } = this.data
    const goods = goodsList[activeCategory]
    if (goods) {
      return goods.find(item => String(item.id) === String(id))
    }
    return null
  },

  onReachBottom() {
    const cat = this.data.activeCategory
    if (this.data.lazyLoading || !this.data.hasMoreMap[cat]) return
    this.loadCategoryGoods(cat, true)
  },

  selectTable() {
    this.setData({ showTableModal: true })
  },

  hideTableModal() {
    this.setData({ showTableModal: false })
  },

  selectTableNumber(e) {
    const tableId = e.currentTarget.dataset.tableId
    this.setData({ tableNumber: tableId, showTableModal: false })
    wx.setStorageSync('tableNumber', tableId)
  },

  testScanCode() {
    wx.scanCode({
      success: (res) => {
        console.log('扫码成功，完整结果:', res)
        const result = res.result
        console.log('扫码内容:', result)
        const qrData = this.parseQRCode(result)
        console.log('解析后的数据:', qrData)
        
        if (qrData && qrData.tableId) {
          console.log('绑定桌台:', qrData.tableId, qrData.secret)
          this.bindTableWithSecret(qrData.tableId, qrData.secret)
        } else {
          console.log('无效的桌台二维码，qrData:', qrData)
          wx.showToast({ title: '无效的桌台二维码', icon: 'none' })
        }
      },
      fail: (err) => {
        console.error('扫码失败:', err)
        wx.showToast({ title: '扫码失败，请重试', icon: 'none' })
      }
    })
  },

  parseQRCode(result) {
    try {
      console.log('原始扫码结果:', result);
      
      // 处理微信小程序跳转链接
      if (result.includes('weixin://')) {
        console.log('检测到微信小程序链接');
        // 提取query参数
        const queryMatch = result.match(/&query=([^&]*)/);
        console.log('queryMatch:', queryMatch);
        if (queryMatch && queryMatch[1]) {
          const queryString = queryMatch[1];
          console.log('queryString:', queryString);
          const pairs = queryString.split('&');
          console.log('pairs:', pairs);
          const data = {};
          for (const pair of pairs) {
            const [key, value] = pair.split('=');
            data[key] = decodeURIComponent(value);
            console.log('解析参数:', key, value, decodeURIComponent(value));
          }
          console.log('解析结果:', data);
          return data;
        }
      }
      // 处理普通URL格式
      else if (result.includes('?')) {
        console.log('检测到普通URL格式');
        const params = result.split('?')[1];
        const pairs = params.split('&');
        const data = {};
        for (const pair of pairs) {
          const [key, value] = pair.split('=');
          data[key] = decodeURIComponent(value);
        }
        console.log('解析结果:', data);
        return data;
      }
      // 处理纯文本格式
      else {
        console.log('检测到纯文本格式');
        // 尝试直接解析为键值对
        if (result.includes('tableId=') && result.includes('secret=')) {
          const pairs = result.split('&');
          const data = {};
          for (const pair of pairs) {
            const [key, value] = pair.split('=');
            data[key] = decodeURIComponent(value);
          }
          console.log('解析结果:', data);
          return data;
        }
      }
      console.log('无法解析二维码内容');
      return null;
    } catch (e) {
      console.error('解析二维码失败:', e);
      return null;
    }
  },

  bindTableWithSecret(tableId, secret) {
    // 直接使用解析出的tableId作为桌台号码
    const tableNumber = tableId
    this.setData({ tableNumber })
    wx.setStorageSync('tableNumber', tableNumber)
    wx.showToast({ title: `绑定成功，桌台号码：${tableNumber}`, icon: 'success' })
  },

  getTableList() {
    this.setData({
      tableList: [
        { id: '1', name: '桌台1' },{ id: '2', name: '桌台2' },
        { id: '3', name: '桌台3' },{ id: '4', name: '桌台4' },
        { id: '5', name: '桌台5' },{ id: '6', name: '桌台6' },
        { id: '7', name: '桌台7' },{ id: '8', name: '桌台8' }
      ]
    })
  },

  stopAll() {
    return false
  },

  navigateToService() {
    wx.showModal({
      title: '能记家庭农场客服',
      content: '手机号：15876534944\n     微信号：njjtnc15876534944',
      showCancel: false
    });
  }
})