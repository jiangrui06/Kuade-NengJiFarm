Page({
    data: {
      cartList: [],
      totalPrice: "0.00",
      selectedCount: 0,
      showModal: false // 控制弹窗显示
    },

    onLoad() {
      this.getCartList();
    },
    
    onShow() {
      // 页面显示时更新购物车数据和角标
      this.getCartList();
    },
    
    // 从API获取购物车数据
    getCartList() {
      const api = require('../../utils/api');
      api.request({
        url: '/api/DemoApi/cart',
        method: 'GET'
      })
      .then(res => {
        console.log('获取购物车数据成功:', res);
        this.setData({
          cartList: res.cartList
        });
        this.calcTotal();
        // 缓存购物车数据
        wx.setStorageSync('cartList', res.cartList);
      })
      .catch(err => {
        console.error('获取购物车数据失败:', err);
        // 从缓存获取数据
        const cachedCartList = wx.getStorageSync('cartList');
        if (cachedCartList && cachedCartList.length > 0) {
          this.setData({
            cartList: cachedCartList
          });
          this.calcTotal();
        }
      });
    },

    // 数量减
    handleMinus(e) {
      const id = e.currentTarget.dataset.id;
      const cartList = JSON.parse(JSON.stringify(this.data.cartList));
      const itemIndex = cartList.findIndex(function(i) {
        return i.id === id;
      });
      if (itemIndex !== -1) {
        if (cartList[itemIndex].count > 1) {
          cartList[itemIndex].count--;
        } else if (cartList[itemIndex].count === 1) {
          // 数量减到0时从购物车移除
          cartList.splice(itemIndex, 1);
        }
        this.setData({ cartList: cartList });
        this.calcTotal();
        // 更新缓存
        wx.setStorageSync('cartList', cartList);
      }
    },

    // 数量加
    handlePlus(e) {
      const id = e.currentTarget.dataset.id;
      const cartList = JSON.parse(JSON.stringify(this.data.cartList));
      const itemIndex = cartList.findIndex(function(i) {
        return i.id === id;
      });
      if (itemIndex !== -1) {
        cartList[itemIndex].count++;
        this.setData({ cartList: cartList });
        this.calcTotal();
        // 更新缓存
        wx.setStorageSync('cartList', cartList);
      }
    },

    // 单选
    toggleSelect(e) {
      const id = e.currentTarget.dataset.id;
      const cartList = JSON.parse(JSON.stringify(this.data.cartList));
      const itemIndex = cartList.findIndex(function(i) {
        return i.id === id;
      });
      if (itemIndex !== -1) {
        cartList[itemIndex].checked = !cartList[itemIndex].checked;
        this.setData({ cartList: cartList });
        this.calcTotal();
        // 更新缓存
        wx.setStorageSync('cartList', cartList);
      }
    },

    // 计算总价和数量
    calcTotal() {
      let totalPrice = 0, selectedCount = 0, totalCount = 0;
      const cartList = this.data.cartList;
      
      cartList.forEach(function(item) {
        totalCount += item.count;
        if (item.checked) {
          totalPrice += item.price * item.count;
          selectedCount += item.count;
        }
      });
      
      this.setData({ 
        totalPrice: totalPrice.toFixed(2), 
        selectedCount: selectedCount
      });
      
      // 更新底部导航栏购物车角标
      if (selectedCount > 0) {
        wx.setTabBarBadge({
          index: 2, // 购物车在tabBar中的索引（从0开始）
          text: selectedCount.toString()
        });
      } else {
        wx.removeTabBarBadge({
          index: 2
        });
      }
    },

    // 🌟 点击结算按钮
    handleSettle() {
      if (this.data.selectedCount === 0) {
        return wx.showToast({ title: "请先选择商品", icon: "none" });
      }
      this.setData({ showModal: true }); // 显示弹窗
    },

    // 🌟 弹窗点击确认购买
    handleConfirmPurchase() {
      // 这里执行购买逻辑（例如生成订单）
      this.setData({ showModal: false }, () => {
        // 跳转到支付页面
        wx.navigateTo({ 
          url: "../buy/buy",
          success: function(res) {
            console.log("跳转成功");
          },
          fail: function(res) {
            console.log("跳转失败", res);
          }
        });
      });
      
      // 2. 清空购物车或更新订单状态
    },

    // 关闭弹窗
    handleCancelModal() {
      this.setData({ showModal: false });
    },

    // TabBar跳转
    navTo(e) {
      const pageMap = { home:"/pages/index/index", activity:"/pages/activity/activity", cart:"/pages/cart/cart", mine:"/pages/profile/profile" };
      wx.switchTab({ url: pageMap[e.currentTarget.dataset.page] });
    }
  });