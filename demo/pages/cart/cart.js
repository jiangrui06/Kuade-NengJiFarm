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
      let totalPrice = 0, selectedCount = 0;
      const cartList = this.data.cartList;
      
      cartList.forEach(function(item) {
        if (item.checked) {
          totalPrice += item.price * item.count;
          selectedCount += item.count;
        }
      });
      
      this.setData({ 
        totalPrice: totalPrice.toFixed(2), 
        selectedCount: selectedCount
      });
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
      wx.showToast({ title: "购买成功！", icon: "success" });
      this.setData({ showModal: false });
      
      // 👉 接下来你要添加的新功能：
      // 1. 跳转订单详情页
      // wx.navigateTo({ url: "/pages/order/order" });
      
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