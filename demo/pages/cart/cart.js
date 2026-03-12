Page({
    data: {
      // 模拟商品数据
      cartList: [
        {
          id: 1,
          name: "【广东直邮】白糯玉米 800g",
          image: "/images/activity-active.png",
          tag: "包邮",
          price: 69.99,
          count: 1,
          checked: false
        },
        {
          id: 2,
          name: "黄花鱼鱼味鲜美，肉嫩滑且肉质呈蒜瓣状...",
          image: "/images/activity-active.png",
          tag: "顺丰包邮",
          price: 169.00,
          count: 1,
          checked: false
        },
        {
          id: 3,
          name: "清真农家散养三黄鸡新鲜生鸡整只...",
          image: "/images/activity-active.png",
          tag: "包邮",
          price: 219.00,
          count: 1,
          checked: false
        }
      ],
      totalPrice: "0.00",
      selectedCount: 0,
      showModal: false // 控制弹窗显示
    },
  
    onLoad() {
      this.calcTotal();
    },
    
    onShow() {
      // 页面显示时更新购物车角标
      this.calcTotal();
    },
  
    // 数量减
    handleMinus(e) {
      const id = e.currentTarget.dataset.id;
      const cartList = JSON.parse(JSON.stringify(this.data.cartList));
      const itemIndex = cartList.findIndex(function(i) {
        return i.id === id;
      });
      if (itemIndex !== -1 && cartList[itemIndex].count > 1) {
        cartList[itemIndex].count--;
        this.setData({ cartList: cartList });
        this.calcTotal();
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